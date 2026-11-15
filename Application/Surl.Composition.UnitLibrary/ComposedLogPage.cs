using Surl.MessageBus;

namespace Surl.Composition;

/// <summary>A page of a topic log's records in offset order, read without moving any subscription's offset (ADR-0164, decision 1).</summary>
/// <param name="Records">The records from the asked offset on, at most the limit asked for.</param>
/// <param name="NextOffset">The offset to pass to read the next page: one past the last record returned, or the asked offset when none was.</param>
/// <param name="HighWatermark">The offset the next message published on the topic will get.</param>
/// <param name="HasMore">Whether records beyond these remain.</param>
public sealed record ComposedLogPage(IReadOnlyList<BusLogRecord> Records, long NextOffset, long HighWatermark, bool HasMore);
