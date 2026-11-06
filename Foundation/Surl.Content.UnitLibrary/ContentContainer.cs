namespace Surl.Content;

/// <summary>
/// One top-level place a request path can land in: the served root, or one share. Each has its
/// own full path, its own file-system seam and whether it takes writes, so a share on disk can sit
/// beside a served root in memory (ADR-0102 decision 2.1).
/// </summary>
internal sealed class ContentContainer
{
    internal ContentContainer(string root, IContentFileSystem fileSystem, bool isReadOnly, string publishedPrefix)
    {
        Root = root;
        FileSystem = fileSystem;
        IsReadOnly = isReadOnly;
        PublishedPrefix = publishedPrefix;
    }

    /// <summary>
    /// What a published name inside the container starts with: empty for the served root, and
    /// <c>/</c> and the share's name for a share, so a name reads as the request path that reaches it.
    /// </summary>
    internal string PublishedPrefix { get; }

    /// <summary>The full path of the container's top directory.</summary>
    internal string Root { get; }

    /// <summary>The seam every look inside the container goes through.</summary>
    internal IContentFileSystem FileSystem { get; }

    /// <summary>Whether every write inside the container is refused (a <c>readonly</c> share).</summary>
    internal bool IsReadOnly { get; }

    /// <summary>The container's top directory with every symbolic link resolved.</summary>
    internal string ResolvedRoot => FileSystem.ResolveFinalPath(Root);
}
