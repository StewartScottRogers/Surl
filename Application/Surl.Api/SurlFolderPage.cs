namespace Surl.Api;

/// <summary>One page of a folder's children, in ordinal order of name.</summary>
/// <param name="Entries">The children on the page.</param>
/// <param name="HasMore">Whether more follow; ask again with the last name as <c>afterName</c>.</param>
public sealed record SurlFolderPage(IReadOnlyList<SurlFolderEntry> Entries, bool HasMore);
