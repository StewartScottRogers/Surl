namespace Surl.Api;

/// <summary>One ready message of a queue, as <see cref="SurlServerMessages.BrowseQueueAsync"/> reads it.</summary>
/// <param name="Position">Its 0-based place in delivery order.</param>
/// <param name="Payload">Its payload bytes.</param>
/// <param name="Headers">The headers or properties it carries, in order.</param>
public sealed record SurlQueueMessage(int Position, ReadOnlyMemory<byte> Payload, IReadOnlyList<KeyValuePair<string, string>> Headers);
