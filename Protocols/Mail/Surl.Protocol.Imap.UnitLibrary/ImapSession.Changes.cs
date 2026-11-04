using System.Globalization;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// The commands that change the mail store (ADR-0055, decisions 6 to 8): <c>APPEND</c>, the
/// mailbox changes <c>CREATE</c>, <c>DELETE</c>, <c>RENAME</c>, <c>SUBSCRIBE</c> and
/// <c>UNSUBSCRIBE</c>, and the message changes <c>STORE</c>, <c>COPY</c>, <c>MOVE</c> and
/// <c>EXPUNGE</c> with their <c>UID</c> forms.
/// </summary>
internal sealed partial class ImapSession
{
    // APPEND is checked before its "+" continuation, so a refusal costs the client no message
    // byte and the session goes on (ADR-0055, decision 8).
    private async ValueTask<bool> AnswerAppendAsync(ImapCommandReadResult read)
    {
        var arguments = new ImapArguments(read.Command!);
        tag = arguments.ReadTag()!;
        arguments.TryReadSpace();
        arguments.ReadAtom();
        if (await RefuseForStateAsync(ImapCommandState.Authenticated, "APPEND") is { } refusal)
        {
            return await RespondAsync(ImapResponse.Only(refusal));
        }

        var request = ImapAppendRequest.Read(arguments);
        var name = request is null ? null : ImapMailboxName.Decode(request.Mailbox);
        var appendRefusal = request is null ? ImapResponses.InvalidArguments : RefuseAppend(name, read.MessageLength);
        return appendRefusal is null
            ? await AppendAsync(name!, request!, read.MessageLength)
            : await RespondAsync(ImapResponse.Only(appendRefusal));
    }

    // The name valid, the mailbox there, and the message within --max-filesize, in that order.
    private string? RefuseAppend(string? name, long length)
    {
        if (!IsMailboxName(name))
        {
            return ImapResponses.InvalidMailboxName;
        }

        if (mailStore.ReadMailbox(view!, name!, out _) != MailStoreOutcome.Succeeded)
        {
            return ImapResponses.MailboxMissingTryCreate;
        }

        var limit = context.Limits.MaxUploadBytes;
        if (limit > 0 && length > limit)
        {
            context.Log.Note($"APPEND refused: {length.ToString(CultureInfo.InvariantCulture)} bytes is past --max-filesize");
            return ImapResponses.TooBig;
        }

        return null;
    }

    // "+", the message streamed into a pending message, the rest of the line, then the store.
    // A peer that closes inside the literal stores nothing.
    private async ValueTask<bool> AppendAsync(string name, ImapAppendRequest request, long length)
    {
        using var pending = mailStore.CreatePendingMessage();
        var rest = await commandReader.ReadAppendMessageAsync(length, pending.Body, CancellationToken);
        if (rest.Outcome != ImapCommandReadOutcome.CommandRead)
        {
            return rest.Outcome != ImapCommandReadOutcome.Closed && await CloseWithAsync(LimitResponse(rest with { Tag = tag }));
        }

        if (rest.Command!.Lines[0].Length > 0)
        {
            return await RespondAsync(InvalidArguments);
        }

        var outcome = mailStore.Append(view!, name, pending, request.Flags, request.InternalDate, out var stored);
        NoteAppend(outcome, name, length, pending.StorageFailure);
        await SaveMailStoreIfChangedAsync(outcome == MailStoreOutcome.Succeeded ? 1 : 0);
        return await RespondAsync(ImapResponse.Only(outcome == MailStoreOutcome.Succeeded
            ? $"OK [APPENDUID {stored.UidValidity.ToString(CultureInfo.InvariantCulture)} {stored.Uid.ToString(CultureInfo.InvariantCulture)}] APPEND completed"
            : ImapResponses.ForTargetOutcome(outcome, "APPEND")));
    }

    private void NoteAppend(MailStoreOutcome outcome, string name, long length, string? storageFailure)
    {
        var note = outcome switch
        {
            MailStoreOutcome.Succeeded => $"Message appended to {name}: {length.ToString(CultureInfo.InvariantCulture)} bytes",
            MailStoreOutcome.StoreFull => "Message refused: the mail store is full",
            MailStoreOutcome.StorageFailed => $"Mail store: {storageFailure}",
            _ => null,
        };
        if (note is not null)
        {
            context.Log.Note(note);
        }
    }

    // CREATE drops one trailing "/" (RFC 3501, section 6.3.3); its parents are not created.
    private ImapResponse CreateMailbox(ImapArguments arguments) =>
        ChangeMailboxes(ReadNames(arguments, 1)?.Select(DropTrailingDelimiter).ToArray(), names => ImapResponses.ForOutcome(mailStore.CreateMailbox(view!, names[0]), "CREATE"));

    private ImapResponse DeleteMailbox(ImapArguments arguments) =>
        ChangeMailboxes(ReadNames(arguments, 1), names => ImapResponses.ForOutcome(mailStore.DeleteMailbox(view!, names[0]), "DELETE"));

    private ImapResponse RenameMailbox(ImapArguments arguments) =>
        ChangeMailboxes(ReadNames(arguments, 2), names => ImapResponses.ForOutcome(mailStore.RenameMailbox(view!, names[0], names[1]), "RENAME"));

    // Every mailbox counts as subscribed, so SUBSCRIBE only checks the mailbox is there
    // (ADR-0055, decision 6).
    private ImapResponse Subscribe(ImapArguments arguments) =>
        ChangeMailboxes(ReadNames(arguments, 1), names =>
            mailStore.ReadMailbox(view!, names[0], out _) == MailStoreOutcome.Succeeded ? ImapResponses.Completed("SUBSCRIBE") : ImapResponses.MailboxMissing);

    // The change's answer once every name is a mailbox name.
    private static ImapResponse ChangeMailboxes(string?[]? names, Func<string[], string> change) =>
        names is null ? InvalidArguments
        : !names.All(IsMailboxName) ? ImapResponse.Only(ImapResponses.InvalidMailboxName)
        : ImapResponse.Only(change(Array.ConvertAll(names, name => name!)));

    private static string? DropTrailingDelimiter(string? name) =>
        name is not null && name.EndsWith(ImapMailboxName.Delimiter) ? name[..^1] : name;

    // UNSUBSCRIBE of any name changes nothing and succeeds.
    private static ImapResponse Unsubscribe(ImapArguments arguments) =>
        ReadNames(arguments, 1) is null ? InvalidArguments : ImapResponse.Only(ImapResponses.Completed("UNSUBSCRIBE"));

    // The command's arguments as exactly count names after spaces, each decoded, or null where
    // it does not decode; null when the arguments are not that.
    private static string?[]? ReadNames(ImapArguments arguments, int count)
    {
        var names = new string?[count];
        for (var index = 0; index < count; index++)
        {
            if (arguments.ReadSpacedAString() is not { } bytes)
            {
                return null;
            }

            names[index] = ImapMailboxName.Decode(bytes);
        }

        return arguments.IsAtEnd ? names : null;
    }

    private static bool IsMailboxName(string? name) => name is not null && ImapMailboxName.IsMailboxName(name);

    private async ValueTask<bool> RespondAfterSavingAsync(ImapResponse response)
    {
        await SaveMailStoreIfChangedAsync(1);
        return await RespondAsync(response);
    }

    // Each message's new flags as an untagged FETCH, unless .SILENT, for the messages whose flags
    // changed; no pending updates (ADR-0055, decision 7).
    private async ValueTask<bool> AnswerStoreAsync(ImapArguments arguments, bool isUid)
    {
        var request = ImapStoreRequest.Read(arguments);
        var messages = request is null ? null : selected!.Resolve(request.Set, isUid);
        if (RefuseStore(request, messages) is { } refusal)
        {
            return await ReplyAsync($"{tag} {refusal}");
        }

        var changed = ChangeFlags(messages!, request!);
        await SaveMailStoreIfChangedAsync(changed.Count);
        foreach (var line in request!.IsSilent ? [] : changed.Select(change => FlagsLine(change, isUid)))
        {
            await WriteLineAsync(line, CancellationToken);
        }

        return await ReplyAsync($"{tag} {ImapResponses.Completed("STORE")}");
    }

    private string? RefuseStore(ImapStoreRequest? request, IReadOnlyList<(int Number, uint Uid)>? messages) =>
        request is null ? ImapResponses.InvalidArguments
        : messages is null ? ImapResponses.InvalidSequenceNumber
        : selected!.IsReadOnly ? ImapResponses.ReadOnly
        : null;

    private static string FlagsLine((int Number, uint Uid, MailFlags Flags) change, bool isUid) =>
        $"* {change.Number} FETCH ({(isUid ? $"UID {change.Uid.ToString(CultureInfo.InvariantCulture)} " : string.Empty)}FLAGS ({ImapFetchResponse.FormatFlags(change.Flags)}))";

    // The messages whose flags the change altered, with their flags afterwards; a message another
    // session expunged is left alone.
    private List<(int Number, uint Uid, MailFlags Flags)> ChangeFlags(IReadOnlyList<(int Number, uint Uid)> messages, ImapStoreRequest request)
    {
        var summaries = ReadSummaries();
        List<(int Number, uint Uid, MailFlags Flags)> changed = [];
        foreach (var (number, uid) in messages)
        {
            // A message another session expunged is MessageMissing; one the store still holds was
            // there when the summaries were read, since UIDs are never given twice.
            var outcome = mailStore.ChangeFlags(view!, selected!.Name, uid, request.Change, request.Flags, out var flags);
            if (outcome == MailStoreOutcome.Succeeded && flags != summaries[uid].Flags)
            {
                changed.Add((number, uid, flags));
            }
        }

        return changed;
    }

    // COPY and MOVE: the set, then the target; a missing target is TRYCREATE (ADR-0055, decision 7).
    private async ValueTask<bool> AnswerCopyAsync(ImapArguments arguments, bool isUid, bool isMove)
    {
        var (messages, name, refusal) = ReadCopy(arguments, isUid, isMove);
        return refusal is null ? await CopyAsync(messages!, name!, isMove) : await ReplyAsync($"{tag} {refusal}");
    }

    private (IReadOnlyList<(int Number, uint Uid)>? Messages, string? Name, string? Refusal) ReadCopy(ImapArguments arguments, bool isUid, bool isMove)
    {
        var set = ImapSequenceSet.ReadSpaced(arguments);
        var mailbox = set is null ? null : arguments.ReadSpacedAString();
        if (mailbox is null || !arguments.IsAtEnd)
        {
            return (null, null, ImapResponses.InvalidArguments);
        }

        var messages = selected!.Resolve(set!, isUid);
        var name = ImapMailboxName.Decode(mailbox);
        return (messages, name, RefuseCopy(messages, name, isMove));
    }

    private string? RefuseCopy(IReadOnlyList<(int Number, uint Uid)>? messages, string? name, bool isMove) =>
        messages is null ? ImapResponses.InvalidSequenceNumber
        : !IsMailboxName(name) ? ImapResponses.InvalidMailboxName
        : isMove && selected!.IsReadOnly ? ImapResponses.ReadOnly
        : null;

    private async ValueTask<bool> CopyAsync(IReadOnlyList<(int Number, uint Uid)> messages, string name, bool isMove)
    {
        var outcome = CopyOrMove(messages, name, isMove, out var copied);
        if (outcome != MailStoreOutcome.Succeeded)
        {
            return await ReplyAsync($"{tag} {ImapResponses.ForTargetOutcome(outcome, isMove ? "MOVE" : "COPY")}");
        }

        await SaveMailStoreIfChangedAsync(copied!.CopyUids.Count);
        var copyUid = CopyUid(copied);
        return isMove ? await CompleteMoveAsync(copyUid, copied.SourceUids) : await ReplyAsync($"{tag} {CopyCompletion(copyUid)}");
    }

    private MailStoreOutcome CopyOrMove(IReadOnlyList<(int Number, uint Uid)> messages, string name, bool isMove, out MailCopyResult? copied)
    {
        var uids = messages.Select(message => message.Uid).ToList();
        return isMove
            ? mailStore.Move(view!, selected!.Name, uids, name, out copied)
            : mailStore.Copy(view!, selected!.Name, uids, name, out copied);
    }

    // RFC 4315's COPYUID response code, when at least one message was copied.
    private static string? CopyUid(MailCopyResult copied) =>
        copied.CopyUids.Count == 0
            ? null
            : $"[COPYUID {copied.DestinationUidValidity.ToString(CultureInfo.InvariantCulture)} {ImapSequenceSet.Format(copied.SourceUids)} {ImapSequenceSet.Format(copied.CopyUids)}]";

    private static string CopyCompletion(string? copyUid) => copyUid is null ? ImapResponses.Completed("COPY") : $"OK {copyUid} COPY completed";

    // "* OK [COPYUID ...] Moved", one EXPUNGE per source, highest number first (RFC 6851).
    private async ValueTask<bool> CompleteMoveAsync(string? copyUid, IReadOnlyList<uint> sourceUids)
    {
        var lines = selected!.Remove(sourceUids);
        foreach (var line in copyUid is null ? lines : lines.Prepend($"* OK {copyUid} Moved"))
        {
            await WriteLineAsync(line, CancellationToken);
        }

        return await ReplyAsync($"{tag} {ImapResponses.Completed("MOVE")}");
    }

    // EXPUNGE, and UID EXPUNGE of a set (RFC 4315): the EXPUNGE lines are the pending updates
    // RespondAsync sends, highest number first (ADR-0055, decision 7).
    private async ValueTask<bool> AnswerExpungeAsync(ImapArguments arguments, bool isUid)
    {
        var refusal = ReadExpunge(arguments, isUid, out var set);
        if (refusal is null)
        {
            await SaveMailStoreIfChangedAsync(Expunge(set).Count);
        }

        return await RespondAsync(ImapResponse.Only(refusal ?? ImapResponses.Completed("EXPUNGE")));
    }

    // UID EXPUNGE's set; the refusal when the arguments do not parse or the mailbox is read-only.
    private string? ReadExpunge(ImapArguments arguments, bool isUid, out ImapSequenceSet? set)
    {
        set = isUid ? ImapSequenceSet.ReadSpaced(arguments) : null;
        var isRead = (!isUid || set is not null) && arguments.IsAtEnd;
        return !isRead ? ImapResponses.InvalidArguments : selected!.IsReadOnly ? ImapResponses.ReadOnly : null;
    }

    // Every \Deleted message, or those the UID set names.
    private IReadOnlyList<uint> Expunge(ImapSequenceSet? set)
    {
        IReadOnlyList<uint> expunged;
        _ = set is null
            ? mailStore.Expunge(view!, selected!.Name, out expunged)
            : mailStore.Expunge(view!, selected!.Name, selected.Resolve(set, isUidSet: true)!.Select(message => message.Uid), out expunged);
        return expunged;
    }
}
