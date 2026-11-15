using Surl.MessageBus;

namespace Surl.Composition;

/// <summary>A page of a queue's ready messages in delivery order, read without consuming any (ADR-0164, decision 1).</summary>
/// <param name="Messages">The ready messages after the cursor, at most the limit asked for, each with its 0-based position.</param>
/// <param name="ReadyCount">How many messages wait for a consumer.</param>
/// <param name="InFlightCount">How many messages a consumer holds unacknowledged.</param>
/// <param name="HasMore">Whether ready messages beyond these remain.</param>
public sealed record ComposedQueuePage(IReadOnlyList<BusBrowsedMessage> Messages, int ReadyCount, int InFlightCount, bool HasMore);
