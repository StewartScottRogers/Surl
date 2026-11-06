using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4): every recognized command asks the exchange's
/// fault plan at <see cref="FaultStage.Command"/> - a maildrop command once its maildrop is open,
/// so a fault never skips the login check - and an <see cref="FaultKind.Error"/> rule answers it
/// with <c>-ERR</c> and the rule's text in place of the command, before the mail store is touched.
/// </summary>
internal sealed partial class Pop3Session
{
    // The commands answered in the AUTHORIZATION state, or in either; every other recognized
    // command needs the maildrop and asks the plan in WithMaildropAsync.
    private static readonly HashSet<string> CommandsWithoutMaildrop = new(StringComparer.Ordinal)
    {
        "CAPA", "QUIT", "USER", "PASS", "STLS", "APOP", "AUTH",
    };

    // The owner whose maildrop the session opened, for a rule's account scope; null before a
    // login and for the anonymous owner.
    private string? accountName;

    // The command's chosen error, or null to answer it as usual. The point names the verb and
    // the logged-in account; POP3 has no path (ADR-0124, decision 4's table).
    private async ValueTask<string?> FindChosenErrorAsync(string verb)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = accountName,
            Command = verb,
        };
        if (await context.Faults.FindAsync(point, CancellationToken) is not { Kind: FaultKind.Error } rule)
        {
            return null;
        }

        context.Log.Note($"{verb} answered with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, verb));
        return "-ERR " + (rule.Reply ?? Pop3Replies.FaultWithoutReply);
    }
}
