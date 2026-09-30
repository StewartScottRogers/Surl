using System.Globalization;
using System.Text;
using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// One IMAP connection's session (ADR-0055): the greeting, then every command answered in order
/// until <c>LOGOUT</c>, the peer's close, or a limit.
/// </summary>
internal sealed class ImapSession
{
    // STATUS's items (ADR-0055, decision 5): RECENT is always 0, since \Recent is not kept.
    private static readonly Dictionary<string, Func<MailboxSnapshot, long>> StatusItems = new(StringComparer.Ordinal)
    {
        ["MESSAGES"] = snapshot => snapshot.Messages.Count,
        ["RECENT"] = _ => 0,
        ["UIDNEXT"] = snapshot => snapshot.NextUid,
        ["UIDVALIDITY"] = snapshot => snapshot.UidValidity,
        ["UNSEEN"] = snapshot => snapshot.Messages.Count(message => !message.Flags.HasFlag(MailFlags.Seen)),
    };

    private static readonly ImapResponse InvalidArguments = ImapResponse.Only(ImapResponses.InvalidArguments);
    private static readonly string LoginCompleted = ImapResponses.Completed("LOGIN");

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ImapCommandReader commandReader;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly IMailAuthenticationPolicy mailAuthenticationPolicy;
    private readonly MailboxStore mailStore;
    private readonly Dictionary<string, (ImapCommandState State, Func<ImapArguments, ValueTask<bool>> Answer)> commands;
    private MailView? view;
    private PasswordLoginVerdict? anonymousVerdict;
    private ImapSelectedMailbox? selected;
    private string tag = string.Empty;

    public ImapSession(
        IConnection connection,
        ExchangeContext context,
        CrlfLineReader lineReader,
        IAuthenticationPolicy authenticationPolicy,
        IMailAuthenticationPolicy mailAuthenticationPolicy,
        MailboxStore mailStore)
    {
        this.connection = connection;
        this.context = context;
        commandReader = new ImapCommandReader(lineReader, connection, context.Limits, context.TimeProvider);
        this.authenticationPolicy = authenticationPolicy;
        this.mailAuthenticationPolicy = mailAuthenticationPolicy;
        this.mailStore = mailStore;
        commands = new(StringComparer.Ordinal)
        {
            ["CAPABILITY"] = (ImapCommandState.Any, arguments => RespondAsync(Capability(arguments))),
            ["NOOP"] = (ImapCommandState.Any, arguments => RespondAsync(WithoutArguments(arguments, "NOOP"))),
            ["LOGOUT"] = (ImapCommandState.Any, AnswerLogoutAsync),
            ["ID"] = (ImapCommandState.Any, _ => RespondAsync(new ImapResponse([ImapResponses.Id], ImapResponses.Completed("ID")))),
            ["LOGIN"] = (ImapCommandState.NotAuthenticated, AnswerLoginAsync),
            ["SELECT"] = (ImapCommandState.Authenticated, arguments => RespondAsync(Select(arguments, isReadOnly: false))),
            ["EXAMINE"] = (ImapCommandState.Authenticated, arguments => RespondAsync(Select(arguments, isReadOnly: true))),
            ["LIST"] = (ImapCommandState.Authenticated, arguments => RespondAsync(List(arguments, "LIST"))),
            ["LSUB"] = (ImapCommandState.Authenticated, arguments => RespondAsync(List(arguments, "LSUB"))),
            ["STATUS"] = (ImapCommandState.Authenticated, arguments => RespondAsync(Status(arguments))),
            ["NAMESPACE"] = (ImapCommandState.Authenticated, arguments => RespondAsync(Namespace(arguments))),
            ["CHECK"] = (ImapCommandState.Selected, arguments => RespondAsync(WithoutArguments(arguments, "CHECK"))),
            ["CLOSE"] = (ImapCommandState.Selected, AnswerCloseAsync),
            ["UNSELECT"] = (ImapCommandState.Selected, arguments => RespondAsync(Unselect(arguments))),
            ["FETCH"] = (ImapCommandState.Selected, arguments => AnswerFetchAsync(arguments, isUid: false)),
            ["SEARCH"] = (ImapCommandState.Selected, arguments => AnswerSearchAsync(arguments, isUid: false)),
            ["UID"] = (ImapCommandState.Selected, AnswerUidAsync),
        };
    }

    private CancellationToken CancellationToken => context.CancellationToken;

    // Asked afresh each time: it changes with the login and with TLS (ADR-0055, decision 2).
    private string Capabilities =>
        ImapCapabilities.List(view is null ? mailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession) : null, context.Limits.MaxUploadBytes);

    /// <summary>
    /// Sends the greeting, then answers every command until the session ends.
    /// </summary>
    /// <returns>A task that completes when the session is over.</returns>
    public async Task RunAsync()
    {
        await WriteLineAsync($"* OK [CAPABILITY {Capabilities}] {ImapResponses.Greeting}", CancellationToken);
        while (await AnswerNextCommandAsync())
        {
        }
    }

    private async ValueTask<bool> AnswerNextCommandAsync()
    {
        var read = await commandReader.ReadCommandAsync(CancellationToken);
        if (read.Outcome == ImapCommandReadOutcome.CommandRead)
        {
            return await AnswerCommandAsync(read.Command!);
        }

        if (read.Outcome == ImapCommandReadOutcome.LiteralTooLong)
        {
            return await ReplyAsync($"{read.Tag} {ImapResponses.LiteralTooLong}");
        }

        return read.Outcome != ImapCommandReadOutcome.Closed && await CloseWithAsync(LimitResponse(read));
    }

    // The response and the note that end the session at a limit (ADR-0055, decisions 9 and 12).
    private (string Line, string Note) LimitResponse(ImapCommandReadResult read) => read.Outcome switch
    {
        ImapCommandReadOutcome.NonSynchronizingLiteral => (
            $"{read.Tag} {ImapResponses.NonSynchronizingLiteral}", "A non-synchronizing literal was refused; the connection was closed."),
        ImapCommandReadOutcome.LineTooLong => (
            read.Tag is null ? ImapResponses.LineTooLongBye : $"{read.Tag} {ImapResponses.LineTooLong}",
            $"A command was longer than {context.Limits.MaxLineBytes} bytes; answered and closed."),
        _ => (ImapResponses.HeadTimedOut, "A command was not complete within the head timeout; answered * BYE and closed."),
    };

    private async ValueTask<bool> AnswerCommandAsync(ImapCommandText text)
    {
        var arguments = new ImapArguments(text);
        if (arguments.ReadTag() is not { } commandTag)
        {
            return await ReplyAsync(ImapResponses.InvalidTag);
        }

        tag = commandTag;
        var name = arguments.TryReadSpace() ? arguments.ReadAtom() : null;
        if (name is null || !commands.TryGetValue(name, out var command))
        {
            return await RespondAsync(ImapResponse.Only(ImapResponses.NotRecognized));
        }

        var refusal = await RefuseForStateAsync(command.State, name);
        return refusal is null ? await command.Answer(arguments) : await RespondAsync(ImapResponse.Only(refusal));
    }

    private async ValueTask<string?> RefuseForStateAsync(ImapCommandState state, string name) => state switch
    {
        ImapCommandState.NotAuthenticated => view is null ? null : ImapResponses.AlreadyAuthenticated,
        ImapCommandState.Authenticated => await IsLoggedInOrAnonymousAsync(name) ? null : ImapResponses.LoginRequired,
        ImapCommandState.Selected => selected is null ? ImapResponses.NoMailboxSelected : null,
        _ => null,
    };

    // Before any login, asks the policy once about the login that carries no credentials; only
    // AcceptedUnchecked (--allow-anonymous) logs the session in (ADR-0055, decision 10).
    private async ValueTask<bool> IsLoggedInOrAnonymousAsync(string name)
    {
        if (view is not null)
        {
            return true;
        }

        anonymousVerdict ??= await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, null, null, connection.TlsSession), CancellationToken);
        if (anonymousVerdict == PasswordLoginVerdict.AcceptedUnchecked)
        {
            view = mailStore.ViewFor(null);
            return true;
        }

        context.Log.Note($"{name} refused: log in first, or give --allow-anonymous");
        return false;
    }

    private ImapResponse Capability(ImapArguments arguments) =>
        arguments.IsAtEnd ? new ImapResponse(["* CAPABILITY " + Capabilities], ImapResponses.Completed("CAPABILITY")) : InvalidArguments;

    private static ImapResponse WithoutArguments(ImapArguments arguments, string command) =>
        arguments.IsAtEnd ? ImapResponse.Only(ImapResponses.Completed(command)) : InvalidArguments;

    private static ImapResponse Namespace(ImapArguments arguments) =>
        arguments.IsAtEnd ? new ImapResponse([ImapResponses.Namespace], ImapResponses.Completed("NAMESPACE")) : InvalidArguments;

    private async ValueTask<bool> AnswerLogoutAsync(ImapArguments arguments)
    {
        if (!arguments.IsAtEnd)
        {
            return await RespondAsync(InvalidArguments);
        }

        await WriteLineAsync(ImapResponses.Bye, CancellationToken);
        await WriteLineAsync($"{tag} {ImapResponses.Completed("LOGOUT")}", CancellationToken);
        await connection.CompleteWritesAsync(CancellationToken);
        return false;
    }

    private async ValueTask<bool> AnswerLoginAsync(ImapArguments arguments) =>
        await RespondAsync(ImapResponse.Only(await LogInAsync(arguments)));

    private async ValueTask<string> LogInAsync(ImapArguments arguments)
    {
        var user = arguments.ReadSpacedAString();
        var password = user is null ? null : arguments.ReadSpacedAString();
        if (password is null || !arguments.IsAtEnd)
        {
            return ImapResponses.InvalidArguments;
        }

        // LOGINDISABLED was advertised, so the password is not even looked at (ADR-0055, decision 10).
        if (!mailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession).IsClearPasswordLoginOffered)
        {
            return ImapResponses.EncryptionRequired;
        }

        var userName = Encoding.UTF8.GetString(user!);
        var verdict = await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, userName, password, connection.TlsSession), CancellationToken);
        return AcceptLogin(userName, verdict);
    }

    // Only a checked login says what the credentials were worth, before the response (ADR-0038;
    // ADR-0049, section 7); the method is the scheme. The note names the user, never the password.
    private string AcceptLogin(string userName, PasswordLoginVerdict verdict)
    {
        if (verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.RefusedCredentials)
        {
            context.Log.Note(new CheckedLogin(context.Scheme, userName, verdict == PasswordLoginVerdict.Accepted).Note);
        }

        var response = LoginResponse(verdict);
        view = response == LoginCompleted ? mailStore.ViewFor(userName) : null;
        return response;
    }

    private static string LoginResponse(PasswordLoginVerdict verdict) => verdict switch
    {
        PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked => LoginCompleted,
        PasswordLoginVerdict.RefusedPlaintext => ImapResponses.EncryptionRequired,
        _ => ImapResponses.LoginFailed,
    };

    private ImapResponse Select(ImapArguments arguments, bool isReadOnly)
    {
        var mailbox = arguments.ReadSpacedAString();
        if (mailbox is null || !arguments.IsAtEnd)
        {
            return InvalidArguments;
        }

        // A SELECT or EXAMINE that fails leaves the authenticated state (RFC 3501, section 6.3.1).
        selected = null;
        if (ReadMailbox(mailbox, out var snapshot) is { } refusal)
        {
            return refusal;
        }

        selected = ImapSelectedMailbox.Open(snapshot!, isReadOnly, out var lines);
        return new ImapResponse(lines, isReadOnly ? "OK [READ-ONLY] EXAMINE completed" : "OK [READ-WRITE] SELECT completed");
    }

    private ImapResponse List(ImapArguments arguments, string command)
    {
        var reference = arguments.ReadSpacedAString();
        var pattern = reference is null ? null : arguments.ReadSpacedListMailbox();
        if (pattern is null || !arguments.IsAtEnd)
        {
            return InvalidArguments;
        }

        var decodedReference = ImapMailboxName.Decode(reference!);
        var decodedPattern = ImapMailboxName.Decode(pattern);
        return decodedReference is null || decodedPattern is null
            ? ImapResponse.Only(ImapResponses.InvalidMailboxName)
            : new ImapResponse(ImapMailboxList.Lines(command, mailStore.ListMailboxes(view!), decodedReference + decodedPattern), ImapResponses.Completed(command));
    }

    private ImapResponse Status(ImapArguments arguments)
    {
        var mailbox = arguments.ReadSpacedAString();
        var items = mailbox is null ? null : arguments.ReadSpacedAtomList();
        if (items is null || !arguments.IsAtEnd || !items.All(StatusItems.ContainsKey))
        {
            return InvalidArguments;
        }

        if (ReadMailbox(mailbox!, out var snapshot) is { } refusal)
        {
            return refusal;
        }

        var values = items.Select(item => item + " " + StatusItems[item](snapshot!).ToString(CultureInfo.InvariantCulture));
        return new ImapResponse([$"* STATUS {ImapMailboxName.ToWire(snapshot!.Name)} ({string.Join(' ', values)})"], ImapResponses.Completed("STATUS"));
    }

    // The mailbox a SELECT, EXAMINE or STATUS names, or the response refusing it.
    private ImapResponse? ReadMailbox(byte[] mailbox, out MailboxSnapshot? snapshot)
    {
        snapshot = null;
        var name = ImapMailboxName.Decode(mailbox);
        if (name is null || !ImapMailboxName.IsMailboxName(name))
        {
            return ImapResponse.Only(ImapResponses.InvalidMailboxName);
        }

        return mailStore.ReadMailbox(view!, name, out snapshot) == MailStoreOutcome.Succeeded ? null : ImapResponse.Only(ImapResponses.MailboxMissing);
    }

    // CLOSE expunges a read-write mailbox's \Deleted messages with no untagged EXPUNGE (RFC 3501,
    // section 6.4.2), and an EXAMINEd one's not at all.
    private async ValueTask<bool> AnswerCloseAsync(ImapArguments arguments)
    {
        if (!arguments.IsAtEnd)
        {
            return await RespondAsync(InvalidArguments);
        }

        var closing = selected!;
        selected = null;
        IReadOnlyList<uint> expunged = [];
        if (!closing.IsReadOnly)
        {
            mailStore.Expunge(view!, closing.Name, out expunged);
        }

        await SaveMailStoreIfChangedAsync(expunged.Count);
        return await RespondAsync(ImapResponse.Only(ImapResponses.Completed("CLOSE")));
    }

    private ImapResponse Unselect(ImapArguments arguments)
    {
        if (!arguments.IsAtEnd)
        {
            return InvalidArguments;
        }

        selected = null;
        return ImapResponse.Only(ImapResponses.Completed("UNSELECT"));
    }

    // UID FETCH and UID SEARCH; the other UID commands are BL-203's.
    private ValueTask<bool> AnswerUidAsync(ImapArguments arguments) => (arguments.TryReadSpace() ? arguments.ReadAtom() : null) switch
    {
        "FETCH" => AnswerFetchAsync(arguments, isUid: true),
        "SEARCH" => AnswerSearchAsync(arguments, isUid: true),
        _ => RespondAsync(ImapResponse.Only(ImapResponses.NotRecognized)),
    };

    // One untagged FETCH per message, then the tagged completion, with no pending updates
    // between them (ADR-0055, decisions 4 and 5).
    private async ValueTask<bool> AnswerFetchAsync(ImapArguments arguments, bool isUid)
    {
        var request = ImapFetchRequest.Read(arguments);
        var messages = request is null ? null : selected!.Resolve(request.Set, isUid);
        if (messages is null)
        {
            return await ReplyAsync($"{tag} {(request is null ? ImapResponses.InvalidArguments : ImapResponses.InvalidSequenceNumber)}");
        }

        return await ReplyAsync($"{tag} {await FetchEachAsync(messages, request!.Items, isUid)}");
    }

    // Writes each message's FETCH response and saves the \Seen flags set; returns the tagged
    // completion, NO once a message cannot be read.
    private async ValueTask<string> FetchEachAsync(IReadOnlyList<(int Number, uint Uid)> messages, IReadOnlyList<ImapFetchItem> items, bool isUid)
    {
        var summaries = ReadSummaries();
        var seenCount = 0;
        var completion = ImapResponses.Completed("FETCH");
        foreach (var (number, uid) in messages)
        {
            if (TryFetch(number, uid, summaries, items) is not { } fetched)
            {
                completion = ImapResponses.ReadFailed;
                break;
            }

            seenCount += fetched.IsSeenSet ? 1 : 0;
            await connection.WriteAsync(ImapFetchResponse.Write(fetched, items, isUid), CancellationToken);
        }

        await SaveMailStoreIfChangedAsync(seenCount);
        return completion;
    }

    // The selected mailbox's messages as they are now; none when another session deleted it.
    private Dictionary<uint, MailMessageSummary> ReadSummaries()
    {
        mailStore.ReadMailbox(view!, selected!.Name, out var snapshot);
        return snapshot?.Messages.ToDictionary(message => message.Uid) ?? [];
    }

    // The message to answer, \Seen set first when an item asks for it; null when its bytes
    // could not be read.
    private ImapFetchedMessage? TryFetch(int number, uint uid, Dictionary<uint, MailMessageSummary> summaries, IReadOnlyList<ImapFetchItem> items)
    {
        if (!summaries.TryGetValue(uid, out var summary))
        {
            context.Log.Note($"Message {uid} in {selected!.Name} was expunged by another session");
            return new ImapFetchedMessage(number, uid, MailFlags.None, false, DateTimeOffset.UnixEpoch, 0, ImapBodyPart.ReadMessage(ReadOnlyMemory<byte>.Empty));
        }

        if (TryReadMessage(uid, items.Any(item => item.ReadsMessage)) is not { } message)
        {
            return null;
        }

        var isSeenSet = MarkSeen(uid, summary.Flags, items);
        return new ImapFetchedMessage(number, uid, summary.Flags | (isSeenSet ? MailFlags.Seen : MailFlags.None), isSeenSet, summary.InternalDate, summary.Size, message);
    }

    // Sets \Seen when a non-peek body item asks for it in a read-write mailbox and it is not set;
    // returns whether it did.
    private bool MarkSeen(uint uid, MailFlags flags, IReadOnlyList<ImapFetchItem> items)
    {
        var isSeenSet = !selected!.IsReadOnly && items.Any(item => item.SetsSeen) && !flags.HasFlag(MailFlags.Seen);
        if (isSeenSet)
        {
            mailStore.ChangeFlags(view!, selected.Name, uid, MailFlagChange.Add, MailFlags.Seen, out _);
        }

        return isSeenSet;
    }

    // The message read from its bytes (empty when it is not needed); null, with the store's
    // exception in a note, when they cannot be read.
    private ImapBodyPart? TryReadMessage(uint uid, bool isNeeded)
    {
        try
        {
            return isNeeded ? ReadMessage(uid) : ImapBodyPart.ReadMessage(ReadOnlyMemory<byte>.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Log.Note($"Mail store: {exception.Message}");
            return null;
        }
    }

    // A message another session expunged meanwhile reads as no bytes.
    private ImapBodyPart ReadMessage(uint uid)
    {
        mailStore.FetchMessage(view!, selected!.Name, uid, out var bytes);
        return ImapBodyPart.ReadMessage(bytes);
    }

    // "* SEARCH" and the numbers (or UIDs) that match in ascending order, then the tagged
    // completion; a message another session expunged matches nothing (ADR-0055, decision 7).
    private async ValueTask<bool> AnswerSearchAsync(ImapArguments arguments, bool isUid)
    {
        var parse = new ImapSearchParser(arguments, (uint)selected!.Uids.Count, selected.HighestUid).Read(out var criteria);
        if (parse != ImapSearchParse.Read)
        {
            return await ReplyAsync($"{tag} {(parse == ImapSearchParse.Invalid ? ImapResponses.InvalidArguments : ImapResponses.BadCharset)}");
        }

        var summaries = ReadSummaries();
        var candidates = selected.Uids
            .Select((uid, index) => (Uid: uid, Number: index + 1))
            .Where(message => summaries.ContainsKey(message.Uid))
            .Select(message => new ImapSearchCandidate(message.Number, summaries[message.Uid], () => ReadMessage(message.Uid)));
        if (TrySearch(candidates, criteria!, isUid) is not { } found)
        {
            return await ReplyAsync($"{tag} {ImapResponses.ReadFailed}");
        }

        await WriteLineAsync("* SEARCH" + found, CancellationToken);
        return await ReplyAsync($"{tag} {ImapResponses.Completed("SEARCH")}");
    }

    private string? TrySearch(IEnumerable<ImapSearchCandidate> candidates, Func<ImapSearchCandidate, bool> criteria, bool isUid)
    {
        try
        {
            return string.Concat(candidates.Where(criteria).Select(candidate => " " + (isUid ? candidate.Summary.Uid : (uint)candidate.Number).ToString(CultureInfo.InvariantCulture)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Log.Note($"Mail store: {exception.Message}");
            return null;
        }
    }

    // A store that cannot be written keeps the change in memory; the next save writes it (ADR-0050, decision 7).
    private async Task SaveMailStoreIfChangedAsync(int changeCount)
    {
        if (changeCount == 0)
        {
            return;
        }

        try
        {
            await mailStore.SaveChangesAsync(CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Log.Note($"Mail store: {exception.Message}");
        }
    }

    // The untagged lines, then the selected mailbox's expunges and additions by other sessions,
    // then the tagged completion (ADR-0055, decision 5).
    private async ValueTask<bool> RespondAsync(ImapResponse response)
    {
        IReadOnlyList<string> updates = [];
        if (selected is not null)
        {
            // A mailbox another session deleted has no snapshot, which is every message expunged.
            mailStore.ReadMailbox(view!, selected.Name, out var snapshot);
            updates = selected.Update(snapshot);
        }

        foreach (var line in response.Untagged.Concat(updates))
        {
            await WriteLineAsync(line, CancellationToken);
        }

        return await ReplyAsync($"{tag} {response.Completion}");
    }

    private async ValueTask<bool> ReplyAsync(string line)
    {
        await WriteLineAsync(line, CancellationToken);
        return true;
    }

    private ValueTask WriteLineAsync(string line, CancellationToken cancellationToken) =>
        ReplyLineWriter.WriteAsync(connection, line, cancellationToken);

    // A limit's response gets ImapProtocolServer.LimitReplyWriteDeadline to be written, and then
    // writes are completed; a peer that does not read it in time is closed all the same, never
    // aborted (ADR-0006, section 5).
    private async ValueTask<bool> CloseWithAsync((string Line, string Note) limit)
    {
        context.Log.Note(limit.Note);
        using var deadline = new CancellationTokenSource(ImapProtocolServer.LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, deadline.Token);
        try
        {
            await WriteLineAsync(limit.Line, cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The response was not written within the one-second write deadline; the connection was closed.");
        }

        return false;
    }
}
