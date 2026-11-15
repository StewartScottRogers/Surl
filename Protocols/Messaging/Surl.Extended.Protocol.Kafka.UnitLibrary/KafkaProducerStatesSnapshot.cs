namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// An opaque copy of one <see cref="KafkaProducerStates"/>: its sequence windows and the next producer id
/// (ADR-0159 decision 2). Only the instance that took it restores it.
/// </summary>
public sealed class KafkaProducerStatesSnapshot
{
    internal KafkaProducerStatesSnapshot(object owner, long byteCount, long? nextProducerId, object windows)
    {
        Owner = owner;
        ByteCount = byteCount;
        NextProducerId = nextProducerId;
        Windows = windows;
    }

    /// <summary>The bytes the copy holds: sixteen for each batch kept in a window (two sequences and a base offset).</summary>
    public long ByteCount { get; }

    internal object Owner { get; }

    internal long? NextProducerId { get; }

    internal object Windows { get; }
}
