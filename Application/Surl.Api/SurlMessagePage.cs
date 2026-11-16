namespace Surl.Api;

/// <summary>One page of a mailbox's messages, in UID order.</summary>
/// <param name="Messages">The messages on the page.</param>
/// <param name="HasMore">Whether more follow; ask again with the last UID as <c>afterUid</c>.</param>
public sealed record SurlMessagePage(IReadOnlyList<SurlMessageSummary> Messages, bool HasMore);
