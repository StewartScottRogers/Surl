namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// An opaque deep copy of the exchanges one <see cref="Amqp0Broker"/> holds, with their bindings (ADR-0159
/// decision 2). Only the broker that took it restores it.
/// </summary>
public sealed class Amqp0ExchangeSnapshot
{
    internal Amqp0ExchangeSnapshot(object owner, IReadOnlyList<Amqp0ExchangeCopy> exchanges)
    {
        Owner = owner;
        Exchanges = exchanges;
        ByteCount = exchanges.Sum(exchange => exchange.ByteCount);
    }

    /// <summary>The bytes the copy holds: names, arguments and binding keys, queues and arguments.</summary>
    public long ByteCount { get; }

    internal object Owner { get; }

    internal IReadOnlyList<Amqp0ExchangeCopy> Exchanges { get; }
}
