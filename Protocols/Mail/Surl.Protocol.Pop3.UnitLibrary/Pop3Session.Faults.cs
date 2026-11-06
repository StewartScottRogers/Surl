using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The command-stage faults (ADR-0124, decision 4; ADR-0141): every recognized command asks the
/// exchange's fault plan at <see cref="FaultStage.Command"/> - a maildrop command once its maildrop
/// is open, so a fault never skips the login check. An <see cref="FaultKind.Error"/> rule answers it
/// with <c>-ERR</c> and the rule's text in place of the command, before the mail store is touched;
/// a <see cref="FaultKind.Delay"/> rule holds the answer for the rule's <c>ms</c> with the idle clock
/// paused; a <see cref="FaultKind.Drop"/> rule aborts the connection with no answer.
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

        return await CommandStageFaults.FindAndApplyAsync(context, connection, point, verb) switch
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
