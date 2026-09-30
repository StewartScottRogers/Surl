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

    /// <summary>
    /// Returns the length in bytes of the file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The full path of an existing file.</param>
    /// <returns>The file's length in bytes.</returns>
    long GetFileLength(string path);

    /// <summary>
    /// Returns when the file or directory at <paramref name="path"/> was last written, in UTC.
    /// </summary>
    /// <param name="path">The full path of an existing file or directory.</param>
    /// <returns>The last write time, with a zero offset.</returns>
    DateTimeOffset GetLastWriteTimeUtc(string path);

    /// <summary>
    /// Opens the file at <paramref name="path"/> for asynchronous, read-only access.
    /// </summary>
    /// <param name="path">The full path of an existing file.</param>
    /// <returns>A readable, seekable stream positioned at the start of the file. The caller
    /// disposes it.</returns>
    Stream OpenFileForAsyncRead(string path);

    /// <summary>
    /// Enumerates the names of the entries - files, directories and symbolic links - directly
    /// inside the directory at <paramref name="path"/>, in no particular order.
    /// </summary>
    /// <param name="path">The full path of an existing directory.</param>
    /// <returns>Each entry's name, without its directory and without <c>.</c> or
    /// <c>..</c>; the sequence may be read lazily, once.</returns>
    IEnumerable<string> EnumerateDirectoryEntryNames(string path);

    /// <summary>
    /// Creates the file at <paramref name="path"/> for asynchronous, write-only access,
    /// replacing any file already there.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>, so the store writes no upload through it.
    /// </remarks>
    /// <param name="path">The full path of a file whose directory exists.</param>
    /// <returns>A writable stream positioned at the start of an empty file. The caller
    /// disposes it.</returns>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    Stream CreateFileForAsyncWrite(string path) =>
        throw new NotSupportedException("This content file system is read-only.");

    /// <summary>
    /// Deletes the file at <paramref name="path"/>; does nothing when no file is there.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <param name="path">The full path of the file to delete.</param>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    void DeleteFile(string path) =>
        throw new NotSupportedException("This content file system is read-only.");

    /// <summary>
    /// Renames the file at <paramref name="source"/> to <paramref name="destination"/>,
    /// replacing any file already there.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <param name="source">The full path of an existing file.</param>
    /// <param name="destination">The full path the file is renamed to, in an existing
    /// directory of the same file system.</param>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    void MoveFileReplacing(string source, string destination) =>
        throw new NotSupportedException("This content file system is read-only.");

    /// <summary>
    /// Renames the file at <paramref name="source"/> to <paramref name="destination"/>, where
    /// nothing may be: the check and the rename are one step, so an entry that appears at the
    /// destination meanwhile is never replaced.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <param name="source">The full path of an existing file.</param>
    /// <param name="destination">The full path the file is renamed to, in an existing
    /// directory of the same file system.</param>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    void MoveFileWithoutReplacing(string source, string destination) =>
        throw new NotSupportedException("This content file system is read-only.");

    /// <summary>
    /// Sets when the file or directory at <paramref name="path"/> was last written.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <param name="path">The full path of an existing file or directory.</param>
    /// <param name="lastWriteTimeUtc">The last write time to set.</param>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    void SetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc) =>
        throw new NotSupportedException("This content file system is read-only.");

    /// <summary>
    /// Renames the directory at <paramref name="source"/>, with everything inside it, to
    /// <paramref name="destination"/>, where nothing may be.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <param name="source">The full path of an existing directory.</param>
    /// <param name="destination">The full path the directory is renamed to, in an existing
    /// directory of the same file system and not inside <paramref name="source"/>.</param>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    void MoveDirectory(string source, string destination) =>
        throw new NotSupportedException("This content file system is read-only.");

    /// <summary>
    /// Creates the directory at <paramref name="path"/> and every missing directory above it;
    /// does nothing when the directory is already there.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <param name="path">The full path of the directory to create.</param>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    void CreateDirectory(string path) =>
        throw new NotSupportedException("This content file system is read-only.");

    /// <summary>
    /// Removes the empty directory at <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// A read-only seam need not implement it: the default throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <param name="path">The full path of an existing directory with no entry inside it.</param>
    /// <exception cref="NotSupportedException">The seam is read-only.</exception>
    void RemoveEmptyDirectory(string path) =>
        throw new NotSupportedException("This content file system is read-only.");
}
