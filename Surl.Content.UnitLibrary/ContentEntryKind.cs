namespace Surl.Content;

/// <summary>
/// What an <see cref="IContentFileSystem"/> finds at a path.
/// </summary>
public enum ContentEntryKind
{
    /// <summary>
    /// Nothing exists at the path.
    /// </summary>
    None = 0,

    /// <summary>
    /// A file exists at the path.
    /// </summary>
    File = 1,

    /// <summary>
    /// A directory exists at the path.
    /// </summary>
    Directory = 2,
}
