using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4): every recognized command asks the exchange's
/// fault plan at <see cref="FaultStage.Command"/>, and an <see cref="FaultKind.Error"/> rule answers
/// it with the rule's reply in place of the command, before the mail store is touched.
/// </summary>
internal sealed partial class SmtpSession
{
    // The account logged in with AUTH, for a rule's account scope; null before a login and for
    // the anonymous owner.
    private string? accountName;

    // The command's chosen error, or null to answer it as usual. The point names the verb, the
    // logged-in account, and at RCPT the recipient address as sent (ADR-0124, decision 4's table).
    private async ValueTask<string?> FindChosenErrorAsync(SmtpCommandLine command)
    {
        var path = RecipientOf(command);
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = accountName,
            Path = path,
            Command = command.Verb,
        };
        if (await context.Faults.FindAsync(point, CancellationToken) is not { Kind: FaultKind.Error } rule)
        {
            return null;
        }

        context.Log.Note($"{command.Verb} answered with the chosen error of fault rule {rule.Id}; nothing was stored.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path ?? command.Verb));
        return rule.Reply ?? SmtpReplies.FaultWithoutReply;
    }

    private static string? RecipientOf(SmtpCommandLine command) =>
        command.Verb == "RCPT" && SmtpPathArgument.TryRead(command.Argument, "TO:", out var to) ? to!.Path : null;
}
