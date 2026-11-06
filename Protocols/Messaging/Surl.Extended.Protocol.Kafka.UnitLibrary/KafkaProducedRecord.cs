using Surl.MessageBus;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// One record a producer sent, read from a record batch (the Kafka "Record" section): what
/// becomes a bus message (ADR-0105 decision 3). The producer's offset and timestamp deltas and
/// attributes are not kept: the broker assigns offsets and the bus assigns times.
/// </summary>
/// <param name="Key">The record's key; null when its length is negative.</param>
/// <param name="Value">The record's value; null when its length is negative.</param>
/// <param name="Headers">The headers whose key and value are both well-formed UTF-8, in order;
/// the others, and those with a null value, are dropped (ADR-0100 decision 3.4).</param>
public sealed record KafkaProducedRecord(ReadOnlyMemory<byte>? Key, ReadOnlyMemory<byte>? Value, IReadOnlyList<BusMessageHeader> Headers)
{
    /// <summary>The bytes the record's key and value hold together, which the bus's
    /// <c>MaxMessageBytes</c> bounds (ADR-0105 decision 7, <c>--max-filesize</c>).</summary>
    public long KeyAndValueBytes => (Key?.Length ?? 0L) + (Value?.Length ?? 0L);
}
