using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The command-stage faults (ADR-0124, decision 4; ADR-0141): a recognized command asks the
/// exchange's fault plan at <see cref="FaultStage.Command"/> - a maildrop command only once its
/// maildrop is open, so a fault never skips the login check. Under an upstream test case, the
/// maildrop commands its replacement handlers answer (<c>STAT</c>, <c>LIST</c>, <c>UIDL</c>,
/// <c>RETR</c>, <c>TOP</c>, <c>DELE</c>, <c>RSET</c>, <c>NOOP</c>) ask no fault plan, and neither
/// does a <c>REPLY</c>'d line. An <see cref="FaultKind.Error"/> rule answers the command with
/// <c>-ERR</c> and the rule's text in its place; the command is not run. The maildrop command that
/// opens an anonymous session's maildrop - its first, unless an earlier one was refused - has
/// nevertheless already opened it (lock taken, view fixed) before the plan is asked, and so before
/// an <see cref="FaultKind.Error"/> rule answers it. A <see cref="FaultKind.Delay"/> rule holds the
/// answer for the rule's <c>ms</c> with the idle clock paused; a <see cref="FaultKind.Drop"/> rule
/// aborts the connection with no answer.
/// </summary>
internal sealed partial class Pop3Session
{
    // The commands answered in the AUTHORIZATION state, or in either; every other recognized
    // command needs the maildrop and asks the plan in WithMaildropAsync, which an upstream test
    // case's replacement handlers for those commands do not go through.
    private static readonly HashSet<string> CommandsWithoutMaildrop = new(StringComparer.Ordinal)
    {
        "CAPA", "QUIT", "USER", "PASS", "STLS", "APOP", "AUTH",
    };

    // The owner whose maildrop the session opened, for a rule's account scope; null before a
    // login and for the anonymous owner.
    private string? accountName;

    // Applies the command's fault, if any, then answers it: the chosen error in place of the
    // answer, the answer as usual (after a delay's hold), or nothing at all after a drop, which
    // ends the session. The point names the verb and the logged-in account; POP3 has no path
    // (ADR-0124, decision 4's table).
    private async ValueTask<bool> AnswerUnlessFaultedAsync(string verb, Func<ValueTask<bool>> answerAsUsual)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = accountName,
            Command = verb,
        };

        var result = await CommandStageFaults.FindAndApplyWithClientOutcomeAsync(context, connection, point, verb);
        if (result.ClientLeft != PeerWatchOutcome.NotWatched)
        {
            // The client left during a delay hold (ADR-0141, amendment 3): send nothing and end
            // the session; a reset aborts the connection, a half-close closes it gracefully.
            if (result.ClientLeft == PeerWatchOutcome.PeerReset)
            {
                connection.Abort();
            }

            return false;
        }

        return result.Rule switch
        {
            { Kind: FaultKind.Error } rule => await AnswerChosenErrorAsync(verb, rule),
            { Kind: FaultKind.Drop } => false,
            _ => await answerAsUsual(),
        };
    }

    private ValueTask<bool> AnswerChosenErrorAsync(string verb, FaultRule rule)
    {
        context.Log.Note($"{verb} answered with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, verb));
        return ReplyAsync("-ERR " + (rule.Reply ?? Pop3Replies.FaultWithoutReply));
    }
}
