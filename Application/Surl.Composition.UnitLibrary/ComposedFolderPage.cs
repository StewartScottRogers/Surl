namespace Surl.Composition;

/// <summary>A page of a folder's children in ordinal order of name.</summary>
/// <param name="Entries">At most the limit asked for, each with a name after the cursor.</param>
/// <param name="HasMore">Whether children beyond these remain.</param>
public sealed record ComposedFolderPage(IReadOnlyList<ComposedFolderEntry> Entries, bool HasMore);
