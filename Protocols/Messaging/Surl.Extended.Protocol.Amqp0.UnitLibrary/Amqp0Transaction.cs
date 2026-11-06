namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A transacted channel's buffer (ADR-0112 decision 6; XML class <c>tx</c>): the publishes and
/// settlements made since the last <c>select</c>, <c>commit</c> or <c>rollback</c>, in order.
/// </summary>
internal sealed class Amqp0Transaction
{
    private readonly List<Amqp0TransactedWork> work = [];

    /// <summary>The body bytes of the publishes buffered, which count toward <c>--max-filesize</c>.</summary>
    public long PublishedBytes { get; private set; }

    /// <summary>Buffers a publish whose content is whole.</summary>
    public void AddPublish(Amqp0PendingPublish publish)
    {
        work.Add(new(publish, [], Acknowledge: false, Requeue: false));
        PublishedBytes += publish.Body.Length;
    }

    /// <summary>Buffers an <c>ack</c>, <c>reject</c> or <c>nack</c> of deliveries taken off the channel.</summary>
    public void AddSettlement(SortedDictionary<ulong, Amqp0OutstandingDelivery> deliveries, bool acknowledge, bool requeue) =>
        work.Add(new(null, deliveries, acknowledge, requeue));

    /// <summary>Hands back the work buffered, in order, and empties the buffer.</summary>
    public List<Amqp0TransactedWork> TakeWork()
    {
        var taken = work.ToList();
        work.Clear();
        PublishedBytes = 0;
        return taken;
    }
}
