namespace Surl.Extended.Protocol.Kafka;

/// <summary>An opaque copy of one <see cref="KafkaBrokerState"/>; only the broker that took it restores it.</summary>
public sealed class KafkaBrokerStateSnapshot
{
    internal KafkaBrokerStateSnapshot(KafkaBrokerState owner, KafkaProducerStatesSnapshot producerStates)
    {
        Owner = owner;
        ProducerStates = producerStates;
    }

    /// <summary>The bytes the copy holds.</summary>
    public long ByteCount => ProducerStates.ByteCount;

    internal KafkaBrokerState Owner { get; }

    internal KafkaProducerStatesSnapshot ProducerStates { get; }
}
