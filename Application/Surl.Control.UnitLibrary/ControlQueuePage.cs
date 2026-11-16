namespace Surl.Control;

/// <summary>A page of a queue's ready messages.</summary>
/// <param name="Messages">The messages.</param>
/// <param name="ReadyCount">How many messages are ready.</param>
/// <param name="InFlightCount">How many are delivered and unacknowledged.</param>
/// <param name="HasMore">Whether more follow the last one.</param>
public sealed record ControlQueuePage(IReadOnlyList<ControlQueuedMessage> Messages, int ReadyCount, int InFlightCount, bool HasMore);
