namespace Surl.Control;

/// <summary>A page of a folder's children in ordinal order of name.</summary>
/// <param name="Entries">The children.</param>
/// <param name="HasMore">Whether more follow the last one.</param>
public sealed record ControlFolderPage(IReadOnlyList<ControlFolderEntry> Entries, bool HasMore);
