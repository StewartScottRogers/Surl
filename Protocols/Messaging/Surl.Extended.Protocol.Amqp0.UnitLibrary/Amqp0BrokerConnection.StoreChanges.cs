using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// What a client's change to the exchange table publishes and the <see cref="FaultKind.OutOfSpace"/> rule that
/// refuses it (ADR-0166 decisions 1 to 3): <see cref="StoreChange.Created"/>, <see cref="StoreChange.Deleted"/>,
/// <see cref="StoreChange.Bound"/> and <see cref="StoreChange.Unbound"/> on <see cref="StoreKind.Exchanges"/>,
/// each named by the exchange (a binding as <c>&lt;exchange&gt;/&lt;queue&gt;/&lt;routing key&gt;</c>) and
/// truncated at 256 characters. A redeclare, a repeated bind, an unbind of nothing and a bind on
/// <c>amq.topic</c> (the bus's) publish nothing.
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    private const int MaxStoreChangeNameLength = 256;

    private static string BindingNameOf(Amqp0Exchange exchange, Amqp0Binding binding) =>
        $"{exchange.Name}/{binding.Queue}/{binding.Key}";

    private void PublishStoreChange(StoreChange change, string name) =>
        context.Events.Publish(new StoreChangedEvent(
            StoreKind.Exchanges,
            change,
            name.Length > MaxStoreChangeNameLength ? name[..MaxStoreChangeNameLength] : name,
            ByteCount: null,
            context.ExchangeId));

    // Before a method that would add to the exchange table the plan is asked at FaultStage.StoreWrite with the
    // exchange as the path; a matching out-of-space rule answers as the table's own bound does, connection
    // exception 506, and nothing is written (ADR-0166 decision 3).
    private async ValueTask RefuseWhenOutOfSpaceAsync(Amqp0Frame frame)
    {
        if (FaultMethodOf(frame) is not { } faulted || ExchangeThatWouldGrow(faulted.Channel, faulted.Method) is not { } exchangeName)
        {
            return;
        }

        var point = new FaultPoint(FaultStage.StoreWrite)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = AccountName,
            Path = exchangeName,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { Kind: FaultKind.OutOfSpace } rule)
        {
            return;
        }

        context.Log.Note($"A write to exchange {exchangeName} was refused as out of space by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.OutOfSpace, FaultStage.StoreWrite, context.ExchangeId, exchangeName));
        throw new Amqp0ConnectionException(Amqp0ReplyCode.ResourceError, "RESOURCE_ERROR - out of space", faulted.Method.ClassId, faulted.Method.MethodId);
    }

    // The exchange a creating declare or an adding bind names; null for any other method, and for one the
    // table would refuse or keep unchanged, which is answered as usual.
    private string? ExchangeThatWouldGrow(Amqp0Channel channel, Amqp0MethodPayload method)
    {
        var arguments = new Amqp0WireReader(method.Arguments);
        return (method.ClassId, method.MethodId) switch
        {
            (Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.Declare) => ExchangeADeclareWouldCreate(arguments),
            (Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Bind) => ExchangeABindWouldGrow(channel, arguments),
            _ => null,
        };
    }

    private string? ExchangeADeclareWouldCreate(Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var name = arguments.ReadShortString();
        arguments.ReadShortString();
        var passive = arguments.ReadBits(5)[0];
        return !passive && name.Length != 0 && IsValidName(name) && exchanges.Find(name) is null ? name : null;
    }

    private string? ExchangeABindWouldGrow(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var queue = arguments.ReadShortString();
        var exchangeName = arguments.ReadShortString();
        var key = arguments.ReadShortString();
        arguments.ReadBits(1);
        var table = arguments.ReadFieldTable();
        if ((queue.Length != 0 ? queue : channel.LastQueue) is not { } resolved
            || exchanges.Find(exchangeName) is not { } exchange
            || exchange.Name == Amqp0ExchangeTable.TopicSpace
            || (exchange.Type == Amqp0ExchangeType.Headers && !Amqp0HeadersMatcher.IsValidBinding(table))
            || Bus.InspectQueue(resolved).Outcome != MessageBusOutcome.Succeeded)
        {
            return null;
        }

        var binding = BindingOf(exchange, resolved, key, table);
        return exchange.Bindings.Any(binding.IsSameAs) ? null : exchangeName;
    }
}
