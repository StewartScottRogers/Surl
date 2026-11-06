using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4): every recognized command the session's state
/// allows asks the exchange's fault plan at <see cref="FaultStage.Command"/>, and an
/// <see cref="FaultKind.Error"/> rule answers it with a tagged <c>NO</c> or <c>BAD</c> - the rule's
/// reply - in place of the command, before the mail store is touched.
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

    // The command's chosen error as a response, or null to answer it as usual. The point names
    // the verb (a UID command's own, FETCH for UID FETCH), the logged-in account, and the mailbox:
    // the one the command names, or else the selected one (ADR-0124, decision 4's table).
    private ValueTask<ImapResponse?> FindChosenErrorAsync(string name, ImapCommandText text)
    {
        var verb = name == "UID" ? UidVerbOf(text) ?? name : name;
        var mailbox = MailboxArgumentCommands.Contains(name) ? MailboxArgumentOf(text) : selected?.Name;
        return FindChosenErrorAsync(verb, mailbox);
    }

    private async ValueTask<ImapResponse?> FindChosenErrorAsync(string verb, string? mailbox)
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
        if (await context.Faults.FindAsync(point, CancellationToken) is not { Kind: FaultKind.Error } rule)
        {
            return null;
        }

        context.Log.Note($"{verb} answered with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, mailbox ?? verb));
        return ImapResponse.Only(rule.Reply ?? ImapResponses.FaultWithoutReply);
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
