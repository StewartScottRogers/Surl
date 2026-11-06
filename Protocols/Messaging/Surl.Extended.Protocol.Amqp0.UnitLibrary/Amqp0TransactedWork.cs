namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// One step a transaction buffered: a publish when <paramref name="Publish"/> is set, otherwise a
/// settlement of <paramref name="Settled"/>.
/// </summary>
/// <param name="Publish">The publish, whole; <see langword="null"/> for a settlement.</param>
/// <param name="Settled">The deliveries settled, by delivery tag; empty for a publish.</param>
/// <param name="Acknowledge">Whether the settlement acknowledges (<c>ack</c>) rather than rejects.</param>
/// <param name="Requeue">Whether a rejection puts the messages back.</param>
internal sealed record Amqp0TransactedWork(
    Amqp0PendingPublish? Publish, SortedDictionary<ulong, Amqp0OutstandingDelivery> Settled, bool Acknowledge, bool Requeue);
