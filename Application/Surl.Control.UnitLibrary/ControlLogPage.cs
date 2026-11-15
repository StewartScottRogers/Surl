namespace Surl.Control;

/// <summary>A page of a topic log.</summary>
/// <param name="Records">The records.</param>
/// <param name="NextOffset">The offset to read next.</param>
/// <param name="HasMore">Whether more follow the last one.</param>
public sealed record ControlLogPage(IReadOnlyList<ControlLogRecord> Records, long NextOffset, bool HasMore);
