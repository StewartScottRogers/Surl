using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): an <c>attach</c> whose node has
/// resolved, and each complete message a client sender transfers, is asked about at a
/// <see cref="FaultStage.Command"/> point - the performative (<c>attach</c>, <c>transfer</c>) as its
/// command, the node's canonical address (<c>/topic/&lt;t&gt;</c>, <c>/queue/&lt;q&gt;</c>) as its
/// path, the account SASL logged in - before the bus is touched. A matching
/// <see cref="FaultKind.Error"/> rule's error condition and description refuse it as Part 2 says an
/// error travels: an <c>attach</c> answered with a null terminus and a closing <c>detach</c>
/// carrying the error, a message with a <c>rejected</c> disposition carrying it (a pre-settled one is
/// dropped and noted).
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    /// <summary>
    /// Reads a rule's reply as an AMQP 1.0 error: a symbol of printable ASCII and a description,
    /// separated by the first space.
    /// </summary>
    /// <param name="reply">The rule's reply.</param>
    /// <returns>The error, or <see langword="null"/> when the reply is not one.</returns>
    internal static Amqp1Error? ReadChosenError(string? reply)
    {
        var space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        if (space <= 0 || space == reply!.Length - 1 || reply.AsSpan(0, space).ContainsAnyExceptInRange('!', '~'))
        {
            return null;
        }

        return Error(new Amqp1Symbol(reply[..space]), reply[(space + 1)..]);
    }

    // The error a matching rule chooses for the operation, or null to answer it as usual.
    private async ValueTask<Amqp1Error?> FindChosenErrorAsync(string performative, string nodeAddress)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = accountName,
            Path = nodeAddress,
            Command = performative,
        };
        if (await context.Faults.FindAsync(point, CancellationToken) is not { } rule)
        {
            return null;
        }

        var target = $"{performative} {nodeAddress}";
        if (rule.Kind != FaultKind.Error || ReadChosenError(rule.Reply) is not { } error)
        {
            context.Log.Note($"{target}: fault rule {rule.Id} is not an error with an AMQP 1.0 condition and description; not applied.");
            return null;
        }

        context.Log.Note($"{target} answered by fault rule {rule.Id} with {error.Condition.Value}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));
        return error;
    }
}
