namespace Surl.Content;

/// <summary>
/// The file-system seam the content store reads the served root through, so the content
/// store never touches the disk itself and its tests need none.
/// </summary>
/// <remarks>
/// Paths passed in and returned are full paths in the platform's own syntax, as
/// <see cref="Path.Join(string?, string?)"/> builds them from the served root.
/// </remarks>
public interface IContentFileSystem
{
    /// <summary>
    /// Reports what exists at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The full path to look at.</param>
    /// <returns>A file, a directory, or nothing.</returns>
    ContentEntryKind GetEntryKind(string path);

    /// <summary>
    /// Returns where <paramref name="path"/> finally resolves: every symbolic link along the
    /// longest part of the path that exists is followed to its final target, and the part
    /// that does not exist is appended unchanged.
    /// </summary>
    /// <param name="path">The full path to resolve.</param>
    /// <returns>The full, normalised path (no <c>.</c> or <c>..</c> segment) with no symbolic
    /// link left in its existing part; the path itself when it contains no symbolic
    /// link.</returns>
    string ResolveFinalPath(string path);
}
