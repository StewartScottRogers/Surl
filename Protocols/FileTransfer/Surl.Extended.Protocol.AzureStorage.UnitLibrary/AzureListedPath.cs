using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One file or directory a Path - List walk found (ADR-0109, decision 5.4).
/// </summary>
/// <param name="Name">The path from the filesystem root, segments joined by <c>/</c>.</param>
/// <param name="IsDirectory">Whether it is a directory.</param>
/// <param name="Mapping">Its mapped location.</param>
/// <param name="LastModifiedUtc">When it was last written.</param>
internal sealed record AzureListedPath(string Name, bool IsDirectory, ContentPathMapping Mapping, DateTimeOffset LastModifiedUtc)
{
    /// <summary>
    /// The key the walk is ordered by and a continuation resumes after: the name, with a
    /// directory's followed by <c>/</c> so it sorts just before what it holds.
    /// </summary>
    public string SortKey => IsDirectory ? $"{Name}/" : Name;
}
