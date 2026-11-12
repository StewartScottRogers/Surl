using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): each <c>PUB</c> and <c>HPUB</c>
/// whose payload has been read, and each <c>SUB</c>, is asked about at a
/// <see cref="FaultStage.Command"/> point - the operation as its command, the subject as the client
/// sent it as its path, the account the login proved - before the bus is touched. A matching
/// <see cref="FaultKind.Error"/> rule's text is answered <c>-ERR '&lt;text&gt;'</c>, nothing is
/// published or subscribed, and the connection goes on, as a permissions violation does.
/// </summary>
internal sealed partial class NatsServerConnection
{
    // Whether a matching error rule answered the operation; null rules and other kinds answer nothing.
    private async ValueTask<bool> IsAnsweredByAFaultAsync(string operation, string subject)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FaultListenUrl.Text(context.ListenUrl),
            Scheme = context.Scheme,
            Account = faultAccount,
            Path = subject,
            Command = operation,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return false;
        }

        var target = $"{operation} {subject}";
        if (rule.Kind != FaultKind.Error || !IsErrorText(rule.Reply))
        {
            context.Log.Note($"{target}: fault rule {rule.Id} is not an error with -ERR text; not applied.");
            return false;
        }

        context.Log.Note($"{target} answered by fault rule {rule.Id} with -ERR '{rule.Reply}'.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));
        outbox.Enqueue(NatsErrors.Line(rule.Reply!));
        return true;
    }

    // Text the -ERR line can quote: not empty, and with no quote or line break to end it early.
    private static bool IsErrorText(string? reply) =>
        !string.IsNullOrEmpty(reply) && reply.IndexOfAny(['\'', '\r', '\n']) < 0;
}
