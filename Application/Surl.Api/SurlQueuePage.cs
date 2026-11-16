namespace Surl.Api;

/// <summary>One page of a queue's ready messages, read without consuming any.</summary>
/// <param name="Messages">The messages on the page.</param>
/// <param name="ReadyCount">How many messages are ready in all.</param>
/// <param name="InFlightCount">How many were delivered and are not yet acknowledged; they are not listed.</param>
/// <param name="HasMore">Whether more follow; ask again with the last position as <c>afterPosition</c>.</param>
public sealed record SurlQueuePage(IReadOnlyList<SurlQueueMessage> Messages, int ReadyCount, int InFlightCount, bool HasMore);
