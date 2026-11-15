namespace Surl.Api;

/// <summary>One page of a topic's log, read without moving any subscription's offset.</summary>
/// <param name="Records">The records on the page.</param>
/// <param name="NextOffset">The offset to ask for next.</param>
/// <param name="HasMore">Whether more records follow.</param>
public sealed record SurlLogPage(IReadOnlyList<SurlLogRecord> Records, long NextOffset, bool HasMore);
