namespace Surl.Control;

/// <summary>One record of a topic log.</summary>
/// <param name="Offset">Its offset.</param>
/// <param name="Timestamp">When it was published.</param>
/// <param name="Key">Its key, or <see langword="null"/>.</param>
/// <param name="Payload">Its payload.</param>
public sealed record ControlLogRecord(long Offset, DateTimeOffset Timestamp, ReadOnlyMemory<byte>? Key, ReadOnlyMemory<byte> Payload);
