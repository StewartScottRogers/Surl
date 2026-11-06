using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The command-stage faults (ADR-0124, decision 4; ADR-0140): every recognized command asks the
/// exchange's fault plan at <see cref="FaultStage.Command"/>. An <see cref="FaultKind.Error"/> rule
/// answers it with the rule's reply in place of the command, before the mail store is touched; a
/// <see cref="FaultKind.Delay"/> rule holds the answer for the rule's <c>ms</c> with the idle clock
/// paused; a <see cref="FaultKind.Drop"/> rule aborts the connection with no answer.
/// </summary>
internal sealed partial class SmtpSession
{
    // The account logged in with AUTH, for a rule's account scope; null before a login and for
    // the anonymous owner.
    private string? accountName;

    // Applies the command's fault, if any, then answers it: the chosen error in place of the
    // answer, the answer as usual (after a delay's hold), or nothing at all after a drop, which
    // ends the session. The point names the verb, the logged-in account, and at RCPT the
    // recipient address as sent (ADR-0124, decision 4's table).
    private async ValueTask<bool> AnswerUnlessFaultedAsync(SmtpCommandLine command, Func<byte[]?, ValueTask<bool>> answer)
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

        return await CommandStageFaults.FindAndApplyAsync(context, connection, point, path ?? command.Verb) switch
        {
            { Kind: FaultKind.Error } rule => await AnswerChosenErrorAsync(command.Verb, path, rule),
            { Kind: FaultKind.Drop } => false,
            _ => await answer(command.Argument),
        };
    }

    private ValueTask<bool> AnswerChosenErrorAsync(string verb, string? path, FaultRule rule)
    {
        context.Log.Note($"{verb} answered with the chosen error of fault rule {rule.Id}; nothing was stored.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path ?? verb));
        return ReplyAsync(rule.Reply ?? SmtpReplies.FaultWithoutReply);
    }

    private static string? RecipientOf(SmtpCommandLine command) =>
        command.Verb == "RCPT" && SmtpPathArgument.TryRead(command.Argument, "TO:", out var to) ? to!.Path : null;
}
