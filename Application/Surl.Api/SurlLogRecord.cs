namespace Surl.Api;

/// <summary>One record of a topic's log, as <see cref="SurlServerMessages.ReadLogAsync"/> reads it.</summary>
/// <param name="Offset">The record's place in the log.</param>
/// <param name="Timestamp">When it was published.</param>
/// <param name="Key">Its key; <see langword="null"/> when it has none.</param>
/// <param name="Payload">Its payload bytes.</param>
public sealed record SurlLogRecord(long Offset, DateTimeOffset Timestamp, ReadOnlyMemory<byte>? Key, ReadOnlyMemory<byte> Payload);
