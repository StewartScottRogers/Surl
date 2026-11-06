using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// The command-stage faults (ADR-0124, decision 4; ADR-0140): every recognized command the
/// session's state allows asks the exchange's fault plan at <see cref="FaultStage.Command"/>. An
/// <see cref="FaultKind.Error"/> rule answers it with a tagged <c>NO</c> or <c>BAD</c> - the rule's
/// reply - in place of the command, before the mail store is touched; a
/// <see cref="FaultKind.Delay"/> rule holds the answer for the rule's <c>ms</c> with the idle clock
/// paused; a <see cref="FaultKind.Drop"/> rule aborts the connection with no answer.
/// </summary>
internal sealed partial class ImapSession
{
    // The commands whose first argument is a mailbox; the point's path is that mailbox.
    private static readonly HashSet<string> MailboxArgumentCommands = new(StringComparer.Ordinal)
    {
        "SELECT", "EXAMINE", "STATUS", "CREATE", "DELETE", "RENAME", "SUBSCRIBE", "UNSUBSCRIBE",
    };

    // The account logged in with LOGIN or AUTHENTICATE, for a rule's account scope; null before a
    // login and for the anonymous owner.
    private string? accountName;

    // Applies the command's fault, if any, then answers it: the chosen error in place of the
    // answer, the answer as usual (after a delay's hold), or nothing at all after a drop, which
    // ends the session. The point names the verb (a UID command's own, FETCH for UID FETCH), the
    // logged-in account, and the mailbox: the one the command names, or else the selected one
    // (ADR-0124, decision 4's table).
    private ValueTask<bool> AnswerUnlessFaultedAsync(string name, ImapCommandText text, Func<ValueTask<bool>> answerAsUsual)
    {
        var verb = name == "UID" ? UidVerbOf(text) ?? name : name;
        var mailbox = MailboxArgumentCommands.Contains(name) ? MailboxArgumentOf(text) : selected?.Name;
        return AnswerUnlessFaultedAsync(verb, mailbox, answerAsUsual);
    }

    private async ValueTask<bool> AnswerUnlessFaultedAsync(string verb, string? mailbox, Func<ValueTask<bool>> answerAsUsual)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = accountName,
            Path = mailbox,
            Command = verb,
        };

        return await CommandStageFaults.FindAndApplyAsync(context, connection, point, mailbox ?? verb) switch
        {
            { Kind: FaultKind.Error } rule => await AnswerChosenErrorAsync(verb, mailbox, rule),
            { Kind: FaultKind.Drop } => false,
            _ => await answerAsUsual(),
        };
    }

    private ValueTask<bool> AnswerChosenErrorAsync(string verb, string? mailbox, FaultRule rule)
    {
        context.Log.Note($"{verb} answered with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, mailbox ?? verb));
        return RespondAsync(ImapResponse.Only(rule.Reply ?? ImapResponses.FaultWithoutReply));
    }

    private static ImapArguments AfterCommandName(ImapCommandText text)
    {
        var arguments = new ImapArguments(text);
        arguments.ReadTag();
        arguments.TryReadSpace();
        arguments.ReadAtom();
        return arguments;
    }

    private static string? UidVerbOf(ImapCommandText text)
    {
        var arguments = AfterCommandName(text);
        return arguments.TryReadSpace() ? arguments.ReadAtom() : null;
    }

    private static string? MailboxArgumentOf(ImapCommandText text) =>
        AfterCommandName(text).ReadSpacedAString() is { } mailbox ? ImapMailboxName.Decode(mailbox) : null;
}
