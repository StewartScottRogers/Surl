namespace Surl.Extended.Protocol.Kafka;

/// <summary>How a SyncGroup is answered.</summary>
/// <param name="Error">The error code; <see cref="KafkaErrorCode.None"/> when the assignment is handed out.</param>
/// <param name="Assignment">The member's assignment, the leader's bytes verbatim; empty on an error.</param>
internal sealed record KafkaSyncGroupResult(KafkaErrorCode Error, ReadOnlyMemory<byte> Assignment);
