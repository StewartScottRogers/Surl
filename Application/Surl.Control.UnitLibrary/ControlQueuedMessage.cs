namespace Surl.Control;

/// <summary>One ready message of a queue, browsed without consuming it.</summary>
/// <param name="Position">Its 0-based position in delivery order.</param>
/// <param name="Payload">Its payload.</param>
/// <param name="Headers">The headers it was seeded with, as name and value pairs.</param>
public sealed record ControlQueuedMessage(int Position, ReadOnlyMemory<byte> Payload, IReadOnlyList<KeyValuePair<string, string>> Headers);
