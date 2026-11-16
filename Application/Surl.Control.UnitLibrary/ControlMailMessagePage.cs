namespace Surl.Control;

/// <summary>A page of a mailbox's messages in UID order.</summary>
/// <param name="Messages">The messages.</param>
/// <param name="HasMore">Whether more follow the last one.</param>
public sealed record ControlMailMessagePage(IReadOnlyList<ControlMailMessageSummary> Messages, bool HasMore);
