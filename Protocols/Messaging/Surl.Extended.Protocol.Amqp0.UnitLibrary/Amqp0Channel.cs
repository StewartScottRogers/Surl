using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// One open channel of a connection (ADR-0112 decisions 0, 6 and 7): its consumers, its
/// unsettled deliveries, its quality of service and flow, and the content still arriving.
/// Deliveries arrive on the bus's thread, so the delivery state is changed under <see cref="Gate"/>.
/// </summary>
/// <param name="number">The channel number.</param>
internal sealed class Amqp0Channel(ushort number)
{
    public ushort Number { get; } = number;

    /// <summary>Guards <see cref="Outstanding"/>, <see cref="LastDeliveryTag"/>, <see cref="FlowActive"/> and <see cref="Held"/>.</summary>
    public Lock Gate { get; } = new();

    /// <summary>Whether the broker sent <c>channel.close</c> and awaits <c>close-ok</c>.</summary>
    public bool Closing { get; set; }

    /// <summary>The last queue declared on the channel, for the methods that name <c>""</c>.</summary>
    public string? LastQueue { get; set; }

    /// <summary>The publish whose content is arriving, if any.</summary>
    public Amqp0PendingPublish? Publish { get; set; }

    /// <summary><c>basic.qos</c>'s prefetch count; 0 for the broker's own bound.</summary>
    public ushort PrefetchCount { get; set; }

    public bool FlowActive { get; set; } = true;

    /// <summary>Deliveries written while the flow is stopped, sent when it resumes.</summary>
    public List<byte[]> Held { get; } = [];

    public Dictionary<string, Amqp0Consumer> Consumers { get; } = new(StringComparer.Ordinal);

    /// <summary>Consumers cancelled whose deliveries may still be outstanding.</summary>
    public List<BusQueueConsumer> Stopped { get; } = [];

    public SortedDictionary<ulong, Amqp0OutstandingDelivery> Outstanding { get; } = [];

    public ulong LastDeliveryTag { get; set; }

    /// <summary>The transaction's buffer once <c>tx.select</c> made the channel transacted; otherwise <see langword="null"/>.</summary>
    public Amqp0Transaction? Transaction { get; set; }

    /// <summary>Whether <c>confirm.select</c> put the channel in confirm mode.</summary>
    public bool Confirming { get; set; }

    /// <summary>The sequence number of the last publish confirmed; publishes count from 1 in confirm mode.</summary>
    public ulong LastPublishSequence { get; set; }
}
