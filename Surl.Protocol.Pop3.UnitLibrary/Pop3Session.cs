using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// One POP3 connection's session (ADR-0056): the greeting, then every command line answered in
/// order, in the AUTHORIZATION state until a login opens the maildrop and in the TRANSACTION
/// state after it, until <c>QUIT</c>, the peer's close, or a limit.
/// </summary>
internal sealed class Pop3Session
{
    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly CrlfLineReader reader;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly IMailAuthenticationPolicy mailAuthenticationPolicy;
    private readonly MailboxStore mailStore;
    private readonly bool isTlsUpgradeAvailable;
    private readonly RandomNumberGenerator timestampRandom;
    private readonly Dictionary<string, Func<Pop3CommandLine, ValueTask<bool>>> commands;
    private string? apopTimestamp;
    private string? pendingUserName;
    private PasswordLoginVerdict? anonymousVerdict;
    private Pop3Maildrop? maildrop;

    public Pop3Session(
        IConnection connection,
        ExchangeContext context,
        CrlfLineReader reader,
        IAuthenticationPolicy authenticationPolicy,
        IMailAuthenticationPolicy mailAuthenticationPolicy,
        MailboxStore mailStore,
        bool isTlsUpgradeAvailable,
        RandomNumberGenerator timestampRandom)
    {
        this.connection = connection;
        this.context = context;
        this.reader = reader;
        this.authenticationPolicy = authenticationPolicy;
        this.mailAuthenticationPolicy = mailAuthenticationPolicy;
        this.mailStore = mailStore;
        this.isTlsUpgradeAvailable = isTlsUpgradeAvailable;
        this.timestampRandom = timestampRandom;
        commands = new(StringComparer.Ordinal)
        {
            ["CAPA"] = AnswerCapabilitiesAsync,
            ["QUIT"] = AnswerQuitAsync,
            ["USER"] = AnswerUserAsync,
            ["PASS"] = AnswerPassAsync,
            ["STLS"] = AnswerStlsAsync,
            ["APOP"] = AnswerApopAsync,
            ["AUTH"] = AnswerAuthAsync,
            ["STAT"] = command => WithMaildropAsync(command, AnswerStatAsync),
            ["LIST"] = command => WithMaildropAsync(command, AnswerListAsync),
            ["UIDL"] = command => WithMaildropAsync(command, AnswerUniqueIdListAsync),
            ["RETR"] = command => WithMaildropAsync(command, AnswerRetrieveAsync),
            ["TOP"] = command => WithMaildropAsync(command, AnswerTopAsync),
            ["DELE"] = command => WithMaildropAsync(command, AnswerDeleteAsync),
            ["RSET"] = command => WithMaildropAsync(command, AnswerResetAsync),
            ["NOOP"] = command => WithMaildropAsync(command, (_, words) => words.Length == 0 ? ReplyAsync(Pop3Replies.Ok) : ReplyAsync(Pop3Replies.InvalidArguments)),
        };
    }

    private CancellationToken CancellationToken => context.CancellationToken;

    /// <summary>
    /// Sends the greeting, then answers every command line until the session ends, and releases
    /// the maildrop lock however it ends.
    /// </summary>
    /// <returns>A task that completes when the session is over.</returns>
    public async Task RunAsync()
    {
        try
        {
            await WriteLineAsync(Greeting(), CancellationToken);
            while (await AnswerNextLineAsync())
            {
            }
        }
        finally
        {
            maildrop?.Dispose();
        }
    }

    // The greeting carries an APOP timestamp only when APOP is offered as the connection opens;
    // a fresh one each connection, so a digest is never valid twice (ADR-0056, decision 2).
    private string Greeting()
    {
        if (!mailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession).IsApopOffered)
        {
            return Pop3Replies.Greeting;
        }

        var random = new byte[8];
        timestampRandom.GetBytes(random);
        apopTimestamp = Invariant($"<{Convert.ToHexStringLower(random)}.{context.TimeProvider.GetUtcNow().ToUnixTimeSeconds()}@surl>");
        return Pop3Replies.GreetingWith(apopTimestamp);
    }

    private async ValueTask<bool> AnswerNextLineAsync()
    {
        var read = await reader.ReadLineAsync(CancellationToken);
        if (read.Line is not { } line)
        {
            return await AnswerNoLineAsync(read.Outcome);
        }

        return Pop3CommandLine.TryParse(line, out var command) && commands.TryGetValue(command!.Verb, out var answer)
            ? await answer(command)
            : await ReplyAsync(Pop3Replies.NotRecognized);
    }

    private async ValueTask<bool> AnswerNoLineAsync(CrlfLineReadOutcome outcome)
    {
        if (outcome == CrlfLineReadOutcome.LineTooLong)
        {
            context.Log.Note($"A command line was longer than {context.Limits.MaxLineBytes} bytes; answered -ERR and closed.");
            await WriteLimitReplyAsync(Pop3Replies.LineTooLong);
        }
        else if (outcome == CrlfLineReadOutcome.HeadTimedOut)
        {
            context.Log.Note("A command line was not complete within the head timeout; answered -ERR and closed.");
            await WriteLimitReplyAsync(Pop3Replies.HeadTimedOut);
        }

        return false;
    }

    private ValueTask<bool> AnswerCapabilitiesAsync(Pop3CommandLine command) =>
        command.Argument is null
            ? WriteMultiLineAsync(Pop3Replies.CapabilityListFollows, CapabilityLines())
            : ReplyAsync(Pop3Replies.InvalidArguments);

    // ADR-0056 decision 3's list, asked afresh each time, since it changes with TLS and the
    // login: USER, SASL and STLS only before a login, each only when it may be used.
    private List<string> CapabilityLines()
    {
        List<string> capabilities = [.. Pop3Replies.FixedCapabilities];
        if (maildrop is not null)
        {
            return capabilities;
        }

        var offer = LoginOffer;
        if (offer.IsClearPasswordLoginOffered)
        {
            capabilities.Add("USER");
        }

        if (offer.SaslMechanisms.Count > 0)
        {
            capabilities.Add("SASL " + string.Join(' ', offer.SaslMechanisms));
        }

        if (CanUpgrade)
        {
            capabilities.Add("STLS");
        }

        return capabilities;
    }

    private MailLoginOffer LoginOffer => mailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession);

    private bool IsClearPasswordOffered => LoginOffer.IsClearPasswordLoginOffered;

    private bool CanUpgrade => isTlsUpgradeAvailable && connection.TlsSession is null;

    private async ValueTask<bool> AnswerQuitAsync(Pop3CommandLine command)
    {
        if (command.Argument is not null)
        {
            return await ReplyAsync(Pop3Replies.InvalidArguments);
        }

        if (maildrop is not null)
        {
            await RemoveDeletedMessagesAsync(maildrop);
        }

        await WriteLineAsync(Pop3Replies.SigningOff, CancellationToken);
        await connection.CompleteWritesAsync(CancellationToken);
        return false;
    }

    // The UPDATE state (RFC 1939, section 6): the marked messages leave the store in one
    // operation. A store that cannot be written keeps the change in memory, and the next save
    // writes it (ADR-0050, decision 7).
    private async Task RemoveDeletedMessagesAsync(Pop3Maildrop session)
    {
        var removed = session.RemoveDeleted();
        if (removed == 0)
        {
            return;
        }

        context.Log.Note($"{removed} messages removed from the maildrop");
        try
        {
            await mailStore.SaveChangesAsync(CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Log.Note($"Mail store: {exception.Message}");
        }
    }

    // Any name is accepted, so no reply tells whether an account exists (ADR-0032, section 8);
    // without a clear password on offer, USER is refused so the password is never sent.
    private ValueTask<bool> AnswerUserAsync(Pop3CommandLine command)
    {
        if (maildrop is not null)
        {
            return ReplyAsync(Pop3Replies.AlreadyLoggedIn);
        }

        pendingUserName = null;
        if (!command.TrySplitArguments(out var words) || words.Length != 1)
        {
            return ReplyAsync(Pop3Replies.InvalidArguments);
        }

        if (!IsClearPasswordOffered)
        {
            return ReplyAsync(Pop3Replies.EncryptionRequired);
        }

        pendingUserName = words[0];
        return ReplyAsync(Pop3Replies.UserAccepted);
    }

    // PASS takes the whole rest of the line, spaces included (RFC 1939, section 7), and spends
    // the USER whatever the outcome.
    private async ValueTask<bool> AnswerPassAsync(Pop3CommandLine command)
    {
        if (maildrop is not null)
        {
            return await ReplyAsync(Pop3Replies.AlreadyLoggedIn);
        }

        var userName = pendingUserName;
        pendingUserName = null;
        var refusal = userName is null ? Pop3Replies.SendUserFirst
            : command.Argument is null ? Pop3Replies.InvalidArguments
            : null;
        return await ReplyAsync(refusal ?? await LogInAsync(new PasswordLogin(context.Scheme, userName, command.Argument, connection.TlsSession)));
    }

    private async ValueTask<string> LogInAsync(PasswordLogin login)
    {
        var verdict = await authenticationPolicy.CheckPasswordLoginAsync(login, CancellationToken);

        // Only a checked login says what the credentials were worth (ADR-0038); the note names
        // the user, never the password.
        if (verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.RefusedCredentials)
        {
            context.Log.Note(new CheckedLogin(login.Scheme, login.UserName, verdict == PasswordLoginVerdict.Accepted).Note);
        }

        return verdict switch
        {
            PasswordLoginVerdict.Accepted => OpenMaildrop(login.UserName) ?? Pop3Replies.LoggedIn,
            PasswordLoginVerdict.AcceptedUnchecked => OpenMaildrop(null) ?? Pop3Replies.LoggedIn,
            PasswordLoginVerdict.RefusedPlaintext => Pop3Replies.EncryptionRequired,
            _ => Pop3Replies.AuthenticationFailed,
        };
    }

    // Takes the owner's maildrop lock and fixes the session's view (ADR-0056, decision 6).
    // Returns the refusal, or null once the session is in the TRANSACTION state.
    private string? OpenMaildrop(string? accountName)
    {
        var view = mailStore.ViewFor(accountName);
        if (mailStore.LockMaildrop(view, out var maildropLock) == MailStoreOutcome.MaildropLocked)
        {
            context.Log.Note("Maildrop locked by another session");
            return Pop3Replies.MaildropLocked;
        }

        // An empty view has no INBOX, and so no message whose unique-id would need its UIDVALIDITY.
        mailStore.ReadMailbox(view, "INBOX", out var inbox);
        maildrop = new Pop3Maildrop(maildropLock!, inbox?.UidValidity ?? 0);
        pendingUserName = null;
        context.Log.Note(string.Create(CultureInfo.InvariantCulture, $"Maildrop opened: {maildrop.Count} messages, {maildrop.Octets} octets"));
        return null;
    }

    private async ValueTask<bool> AnswerStlsAsync(Pop3CommandLine command)
    {
        var refusal = maildrop is not null ? Pop3Replies.AlreadyLoggedIn
            : command.Argument is not null ? Pop3Replies.InvalidArguments
            : connection.TlsSession is not null ? Pop3Replies.AlreadyUsingTls
            : isTlsUpgradeAvailable ? null
            : Pop3Replies.StlsNotAvailable;
        return refusal is null ? await UpgradeToTlsAsync() : await ReplyAsync(refusal);
    }

    // +OK, then every byte pipelined after the STLS line thrown away unrun, then the handshake;
    // the session starts over in AUTHORIZATION with the USER forgotten, and keeps the greeting's
    // timestamp (RFC 2595 section 4, ADR-0056 decision 8). A failed handshake throws
    // TlsHandshakeException, which the engine notes.
    private async ValueTask<bool> UpgradeToTlsAsync()
    {
        await WriteLineAsync(Pop3Replies.BeginTls, CancellationToken);
        var discarded = reader.DiscardBuffered();
        if (discarded > 0)
        {
            context.Log.Note($"Discarded {discarded} bytes sent after STLS");
        }

        await connection.UpgradeToTlsAsync(CancellationToken);
        pendingUserName = null;
        anonymousVerdict = null;
        return true;
    }

    // APOP <name> <digest> (RFC 1939, section 7): checked by the policy against the timestamp
    // this connection's greeting carried; without one, or once APOP is no longer offered, the
    // mechanism is unsupported (ADR-0056, decision 4).
    private async ValueTask<bool> AnswerApopAsync(Pop3CommandLine command)
    {
        if (maildrop is not null)
        {
            return await ReplyAsync(Pop3Replies.AlreadyLoggedIn);
        }

        if (!command.TrySplitArguments(out var words) || words.Length != 2)
        {
            return await ReplyAsync(Pop3Replies.InvalidArguments);
        }

        if (apopTimestamp is null || !LoginOffer.IsApopOffered)
        {
            return await ReplyAsync(Pop3Replies.UnsupportedMechanism);
        }

        var step = await mailAuthenticationPolicy.CheckApopLoginAsync(
            new ApopLogin(context.Scheme, words[0], apopTimestamp, words[1], connection.TlsSession), CancellationToken);
        return await AnswerLoginEndedAsync(step);
    }

    // AUTH with no mechanism is RFC 1734's listing of what is offered; with one, the SASL
    // exchange (RFC 5034), its initial response "=" for an empty one.
    private ValueTask<bool> AnswerAuthAsync(Pop3CommandLine command) =>
        maildrop is not null ? ReplyAsync(Pop3Replies.AlreadyLoggedIn)
        : command.Argument is null ? WriteMultiLineAsync(Pop3Replies.SaslMechanismsFollow, LoginOffer.SaslMechanisms)
        : AnswerAuthMechanismAsync(command);

    private ValueTask<bool> AnswerAuthMechanismAsync(Pop3CommandLine command)
    {
        if (!command.TrySplitArguments(out var words) || words.Length > 2)
        {
            return ReplyAsync(Pop3Replies.InvalidArguments);
        }

        if (words.Length == 1)
        {
            return RunSaslExchangeAsync(words[0], null);
        }

        return DecodeInitialResponse(words[1]) is { } initialResponse
            ? RunSaslExchangeAsync(words[0], initialResponse)
            : ReplyAsync(Pop3Replies.CannotDecodeResponse);
    }

    private static byte[]? DecodeInitialResponse(string word) =>
        word == "=" ? [] : SaslContinuationLine.Classify(Encoding.ASCII.GetBytes(word)).Response;

    // The server frames the exchange - "+ " continuations, the client's base64 responses, "*" -
    // and the policy decides every step (ADR-0049, section 6).
    private async ValueTask<bool> RunSaslExchangeAsync(string mechanism, byte[]? initialResponse)
    {
        // A null array, or a bare null beside a memory, converts to an empty memory, which means
        // "=", not "none sent".
        ReadOnlyMemory<byte>? sent = default;
        if (initialResponse is not null)
        {
            sent = initialResponse;
        }

        var exchange = mailAuthenticationPolicy.StartSaslExchange(
            new SaslExchangeStart(context.Scheme, mechanism, sent, connection.TlsSession));
        var step = await exchange.BeginAsync(CancellationToken);
        while (step.Outcome == SaslLoginOutcome.Challenge)
        {
            NoteCheckedLogin(step);
            await WriteLineAsync(Pop3Replies.Continuation(step.Challenge.Span), CancellationToken);
            var read = await reader.ReadSaslContinuationAsync(CancellationToken);
            if (read.Response is null)
            {
                return await AnswerNoResponseAsync(read.Outcome);
            }

            step = await exchange.ContinueAsync(read.Response, CancellationToken);
        }

        return await AnswerLoginEndedAsync(step);
    }

    // A cancel or an undecodable response ends the exchange and the session goes on; a limit or
    // the peer's close ends the session as a command line's would.
    private ValueTask<bool> AnswerNoResponseAsync(SaslContinuationOutcome outcome) =>
        Pop3Replies.SaslExchangeAbandoned(outcome) is { } reply ? ReplyAsync(reply) : AnswerNoLineAsync(AsLineReadOutcome(outcome));

    /// <summary>
    /// The command-line read outcome that ends the session as a continuation read's does.
    /// </summary>
    /// <param name="outcome">How the continuation read ended.</param>
    /// <returns><see cref="CrlfLineReadOutcome.LineTooLong"/> or <see cref="CrlfLineReadOutcome.HeadTimedOut"/>
    /// for those limits, and <see cref="CrlfLineReadOutcome.Closed"/> for anything else.</returns>
    internal static CrlfLineReadOutcome AsLineReadOutcome(SaslContinuationOutcome outcome) => outcome switch
    {
        SaslContinuationOutcome.LineTooLong => CrlfLineReadOutcome.LineTooLong,
        SaslContinuationOutcome.HeadTimedOut => CrlfLineReadOutcome.HeadTimedOut,
        _ => CrlfLineReadOutcome.Closed,
    };

    // The note is written before the reply (ADR-0038); an accepted login takes the maildrop lock
    // before it is answered (ADR-0056, decision 6).
    private ValueTask<bool> AnswerLoginEndedAsync(SaslLoginStep step)
    {
        NoteCheckedLogin(step);
        return ReplyAsync(step.Outcome is SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked
            ? OpenMaildrop(step.AccountName) ?? Pop3Replies.AuthenticationSuccessful
            : Pop3Replies.LoginRefused(step.Outcome));
    }

    private void NoteCheckedLogin(SaslLoginStep step)
    {
        if (step.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        if (step.RefusalNote is { } refusalNote)
        {
            context.Log.Note(refusalNote);
        }
    }

    // A maildrop command before any login asks the policy once about the login with no
    // credentials, and only --allow-anonymous opens the anonymous owner's maildrop (ADR-0056,
    // decision 7).
    private async ValueTask<bool> WithMaildropAsync(Pop3CommandLine command, Func<Pop3Maildrop, string[], ValueTask<bool>> answer)
    {
        if (maildrop is null && await OpenAnonymousMaildropAsync(command.Verb) is { } refusal)
        {
            return await ReplyAsync(refusal);
        }

        return command.TrySplitArguments(out var words)
            ? await answer(maildrop!, words)
            : await ReplyAsync(Pop3Replies.InvalidArguments);
    }

    private async ValueTask<string?> OpenAnonymousMaildropAsync(string verb)
    {
        anonymousVerdict ??= await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, null, null, connection.TlsSession), CancellationToken);
        if (anonymousVerdict != PasswordLoginVerdict.AcceptedUnchecked)
        {
            context.Log.Note($"{verb} refused: log in first, or give --allow-anonymous");
            return Pop3Replies.AuthenticationRequired;
        }

        return OpenMaildrop(null);
    }

    private ValueTask<bool> AnswerStatAsync(Pop3Maildrop session, string[] words) =>
        ReplyAsync(words.Length == 0 ? Pop3Replies.Status(session.Count, session.Octets) : Pop3Replies.InvalidArguments);

    private ValueTask<bool> AnswerListAsync(Pop3Maildrop session, string[] words) =>
        AnswerListingAsync(session, words, Pop3Replies.ListingFollows(session.Count, session.Octets), message => Invariant($"{message.Number} {message.Size}"));

    private ValueTask<bool> AnswerUniqueIdListAsync(Pop3Maildrop session, string[] words) =>
        AnswerListingAsync(session, words, Pop3Replies.UniqueIdListingFollows, message => Invariant($"{message.Number} {session.UniqueId(message)}"));

    // LIST and UIDL: every message not deleted with no argument, one message with one.
    private ValueTask<bool> AnswerListingAsync(Pop3Maildrop session, string[] words, string status, Func<MaildropMessage, string> line) => words.Length switch
    {
        0 => WriteMultiLineAsync(status, session.Remaining.Select(line)),
        1 => ReplyAsync(session.FindMessage(words[0], out var message) ?? Pop3Replies.OneLine(line(message))),
        _ => ReplyAsync(Pop3Replies.InvalidArguments),
    };

    private ValueTask<bool> AnswerRetrieveAsync(Pop3Maildrop session, string[] words) =>
        WithOneMessageAsync(session, words, words.Length == 1, message =>
            WriteMessageAsync(Pop3Replies.MessageFollows(message.Size), session.Read(message)));

    // TOP's line count is 0 when left out: curl -X TOP pop3://h/1 sends TOP 1 (ADR-0056, decision 5).
    private ValueTask<bool> AnswerTopAsync(Pop3Maildrop session, string[] words)
    {
        var bodyLines = 0;
        var isValid = words.Length == 1 || (words.Length == 2 && TryReadLineCount(words[1], out bodyLines));
        return WithOneMessageAsync(session, words, isValid, message =>
            WriteMessageAsync(Pop3Replies.TopFollows, Pop3TopSection.Slice(session.Read(message), bodyLines)));
    }

    private ValueTask<bool> AnswerDeleteAsync(Pop3Maildrop session, string[] words) =>
        WithOneMessageAsync(session, words, words.Length == 1, message =>
        {
            session.Delete(message);
            return ReplyAsync(Pop3Replies.MessageDeleted);
        });

    private ValueTask<bool> AnswerResetAsync(Pop3Maildrop session, string[] words)
    {
        if (words.Length > 0)
        {
            return ReplyAsync(Pop3Replies.InvalidArguments);
        }

        session.Reset();
        return ReplyAsync(Pop3Replies.MaildropHas(session.Count, session.Octets));
    }

    private ValueTask<bool> WithOneMessageAsync(Pop3Maildrop session, string[] words, bool isValid, Func<MaildropMessage, ValueTask<bool>> answer)
    {
        if (!isValid)
        {
            return ReplyAsync(Pop3Replies.InvalidArguments);
        }

        return session.FindMessage(words[0], out var message) is { } refusal ? ReplyAsync(refusal) : answer(message);
    }

    private static bool TryReadLineCount(string word, out int count) =>
        int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out count);

    private async ValueTask<bool> WriteMessageAsync(string status, ReadOnlyMemory<byte> message)
    {
        await WriteLineAsync(status, CancellationToken);
        await DotStuffedBodyWriter.WriteAsync(connection, message, CancellationToken);
        return true;
    }

    // A multi-line reply built only from numbers and fixed words, written whole: the status
    // line, each content line, then the lone "." (RFC 1939, section 3).
    private async ValueTask<bool> WriteMultiLineAsync(string status, IEnumerable<string> lines)
    {
        var reply = new StringBuilder(status).Append("\r\n");
        foreach (var line in lines)
        {
            reply.Append(line).Append("\r\n");
        }

        reply.Append(".\r\n");
        await connection.WriteAsync(Encoding.ASCII.GetBytes(reply.ToString()), CancellationToken);
        return true;
    }

    private async ValueTask<bool> ReplyAsync(string line)
    {
        await WriteLineAsync(line, CancellationToken);
        return true;
    }

    private ValueTask WriteLineAsync(string line, CancellationToken cancellationToken) =>
        ReplyLineWriter.WriteAsync(connection, line, cancellationToken);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    // A limit's reply gets Pop3ProtocolServer.LimitReplyWriteDeadline to be written, and then
    // writes are completed; a peer that does not read it in time is closed all the same, never
    // aborted (ADR-0006, section 5).
    private async Task WriteLimitReplyAsync(string line)
    {
        using var deadline = new CancellationTokenSource(Pop3ProtocolServer.LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, deadline.Token);
        try
        {
            await WriteLineAsync(line, cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The reply was not written within the one-second write deadline; the connection was closed.");
        }
    }
}
