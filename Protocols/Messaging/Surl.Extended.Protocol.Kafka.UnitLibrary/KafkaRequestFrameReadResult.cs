namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The outcome of reading one request, and its bytes when one was read.
/// </summary>
/// <param name="Outcome">What was found.</param>
/// <param name="Size">The size prefix, when one was read; otherwise 0.</param>
/// <param name="Request">The request's bytes after its size prefix; empty unless one was read.</param>
public sealed record KafkaRequestFrameReadResult(KafkaRequestFrameReadOutcome Outcome, int Size, ReadOnlyMemory<byte> Request);
