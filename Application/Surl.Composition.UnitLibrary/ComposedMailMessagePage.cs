namespace Surl.Composition;

/// <summary>A page of a mailbox's messages in UID order.</summary>
/// <param name="Messages">At most the limit asked for, each with a UID above the cursor.</param>
/// <param name="HasMore">Whether messages beyond these remain.</param>
public sealed record ComposedMailMessagePage(IReadOnlyList<ComposedMailMessageSummary> Messages, bool HasMore);
