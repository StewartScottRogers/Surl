namespace Surl.Content;

/// <summary>
/// A hand-written in-memory <see cref="IContentFileSystem"/>: files, directories and symbolic
/// links keyed by full path, and a record of every question the content store asked.
/// </summary>
internal sealed class InMemoryContentFileSystem : IContentFileSystem
{
    private const int MaximumLinksFollowed = 40;

    private readonly Dictionary<string, ContentEntryKind> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> symbolicLinks = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];

    public InMemoryContentFileSystem AddFile(string path)
    {
        entries[path] = ContentEntryKind.File;
        return this;
    }

    public InMemoryContentFileSystem AddDirectory(string path)
    {
        entries[path] = ContentEntryKind.Directory;
        return this;
    }

    public InMemoryContentFileSystem AddSymbolicLink(string path, string target)
    {
        symbolicLinks[path] = target;
        return this;
    }

    public ContentEntryKind GetEntryKind(string path)
    {
        Calls.Add($"{nameof(GetEntryKind)}({path})");
        return entries.GetValueOrDefault(path);
    }

    public string ResolveFinalPath(string path)
    {
        Calls.Add($"{nameof(ResolveFinalPath)}({path})");
        for (int followed = 0; followed < MaximumLinksFollowed; followed++)
        {
            string? link = symbolicLinks.Keys.FirstOrDefault(
                key => path == key || path.StartsWith(key + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            if (link is null)
            {
                return path;
            }

            path = symbolicLinks[link] + path[link.Length..];
        }

        throw new IOException("Too many levels of symbolic links.");
    }
}
