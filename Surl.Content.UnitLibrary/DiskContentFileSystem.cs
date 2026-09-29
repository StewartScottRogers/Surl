using System.Diagnostics.CodeAnalysis;

namespace Surl.Content;

/// <summary>
/// <see cref="IContentFileSystem"/> over the local disk, through <c>System.IO</c>: the one real
/// implementation of the content store's file-system seam.
/// </summary>
/// <remarks>
/// Each member is a direct call into <see cref="File"/>, <see cref="Directory"/>,
/// <see cref="FileInfo"/>, <see cref="DirectoryInfo"/>, <see cref="FileSystemInfo"/> or <see cref="FileStream"/>, except
/// <see cref="ResolveFinalPath(string)"/>, which walks the path one segment at a time because
/// the seam contract asks for every symbolic link along it to be followed.
/// </remarks>
public sealed class DiskContentFileSystem : IContentFileSystem
{
    private const int FileStreamBufferSize = 4096;

    /// <inheritdoc/>
    // Excluded from coverage: it asks the disk, and the fast tests run without one by design.
    // The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public ContentEntryKind GetEntryKind(string path)
    {
        if (File.Exists(path))
        {
            return ContentEntryKind.File;
        }

        return Directory.Exists(path) ? ContentEntryKind.Directory : ContentEntryKind.None;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The path is made full and normalised with <see cref="Path.GetFullPath(string)"/>, then
    /// walked from its root one segment at a time. A segment that exists, or is a symbolic link
    /// whose target does not, is replaced by
    /// <see cref="FileSystemInfo.ResolveLinkTarget(bool)"/> with <c>returnFinalTarget</c> set
    /// when it is a link; the first segment that does not exist ends the walk, and it and the
    /// rest are appended unchanged.
    /// </remarks>
    // Excluded from coverage: it asks the disk, and the fast tests run without one by design.
    // The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public string ResolveFinalPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)!;
        string[] segments = fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        string resolved = root;
        for (int index = 0; index < segments.Length; index++)
        {
            var entry = new FileInfo(Path.Join(resolved, segments[index]));
            if (!entry.Exists && !Directory.Exists(entry.FullName) && entry.LinkTarget is null)
            {
                return Path.Join([resolved, .. segments[index..]]);
            }

            resolved = entry.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? entry.FullName;
        }

        return resolved;
    }

    /// <inheritdoc/>
    // Excluded from coverage: it asks the disk, and the fast tests run without one by design.
    // The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public long GetFileLength(string path) => new FileInfo(path).Length;

    /// <inheritdoc/>
    // Excluded from coverage: it asks the disk, and the fast tests run without one by design.
    // The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public DateTimeOffset GetLastWriteTimeUtc(string path) =>
        new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);

    /// <inheritdoc/>
    /// <remarks>
    /// The file is shared for reading, writing and deletion, so serving it never stops its
    /// owner from changing it; the content store stops at the end of a file that shrank.
    /// </remarks>
    // Excluded from coverage: it opens a file on disk, and the fast tests run without one by
    // design. The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public Stream OpenFileForAsyncRead(string path) =>
        new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <inheritdoc/>
    /// <remarks>
    /// No entry is skipped for its attributes, so a Windows hidden or system entry is
    /// enumerated as it is on Linux and macOS; an entry the process may not read is skipped.
    /// </remarks>
    // Excluded from coverage: it asks the disk, and the fast tests run without one by design.
    // The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) =>
        new DirectoryInfo(path)
            .EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = true })
            .Select(entry => entry.Name);

    /// <inheritdoc/>
    /// <remarks>
    /// The file is shared with nobody while it is written, so no reader sees half an upload
    /// through Surl's own seam.
    /// </remarks>
    // Excluded from coverage: it creates a file on disk, and the fast tests run without one by
    // design. The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public Stream CreateFileForAsyncWrite(string path) =>
        new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <inheritdoc/>
    // Excluded from coverage: it deletes a file on disk, and the fast tests run without one by
    // design. The Integration tests in Surl.Content.UnitTests (DiskContentFileSystemTests) cover it.
    [ExcludeFromCodeCoverage(Justification = "Covered by the Integration tests in Surl.Content.UnitTests.")]
    public void DeleteFile(string path) => File.Delete(path);
}
