namespace Surl.Content;

/// <summary>
/// The content store: the served root, and the rules that map a request path onto a location
/// inside it without ever escaping it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MapRequestPath(string)"/> takes the path component of a request, still
/// percent-encoded and without its query. It must start with <c>/</c>; it is split on
/// <c>/</c>, and each segment is percent-decoded as UTF-8 and checked on its own, so an
/// encoded <c>%2F</c> or <c>%5C</c> can never become a separator. One trailing <c>/</c> is
/// allowed; any other empty segment is refused, which refuses <c>//server/share</c>.
/// </para>
/// <para>
/// Every dot segment is refused, including one that would stay inside the root such as
/// <c>/a/../b</c>. Upstream curl removes dot segments before it sends a request, so a raw or
/// percent-encoded <c>..</c> reaches Surl only when the client asked for it on purpose
/// (<c>--path-as-is</c>, or an encoding curl does not normalise); refusing it outright keeps
/// the rule one line long and leaves nothing to get wrong about where a <c>..</c> lands.
/// </para>
/// <para>
/// A decoded segment is also refused when it holds <c>/</c>, <c>\</c>, <c>:</c> (a drive
/// letter or an alternate data stream) or a control character, ends in <c>.</c> or a space
/// (which Windows strips), or is a Windows device name such as <c>CON</c> or <c>NUL</c>. These
/// rules apply on every platform, so a request is answered the same way wherever Surl runs.
/// </para>
/// <para>
/// A well-formed path is joined to the served root and resolved through
/// <see cref="IContentFileSystem.ResolveFinalPath(string)"/>; if a symbolic link along it
/// finally lands outside the root, itself resolved the same way, the path is refused. The
/// comparison is ordinal, so the seam must spell the root the same way in every path it
/// returns; a different spelling is refused, never let through. Mapping never opens anything:
/// the seam is asked only where a path resolves and what is there.
/// </para>
/// <para>
/// For a mapped location, <see cref="GetEntryKind(ContentPathMapping)"/> reports a file, a
/// directory or nothing; <see cref="GetFileStatus(ContentPathMapping)"/> reports a file's
/// length and UTC modification time; and
/// <see cref="CopyFileBytesAsync(ContentPathMapping, ContentByteRange, Stream, CancellationToken)"/>
/// copies the whole file or a <see cref="ContentByteRange"/> of it to a destination stream.
/// Every look and every read goes through the seam.
/// </para>
/// </remarks>
public sealed class ContentStore
{
    private const int CopyBufferSize = 81920;

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates a content store that serves <paramref name="servedRoot"/>.
    /// </summary>
    /// <param name="servedRoot">The full path of the directory being served.</param>
    /// <param name="fileSystem">The seam every look at the served root goes through.</param>
    public ContentStore(string servedRoot, IContentFileSystem fileSystem)
    {
        ArgumentException.ThrowIfNullOrEmpty(servedRoot);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ServedRoot = servedRoot;
        this.fileSystem = fileSystem;
    }

    /// <summary>
    /// The full path of the directory being served.
    /// </summary>
    public string ServedRoot { get; }

    /// <summary>
    /// Maps a request path onto a location inside the served root, or refuses it.
    /// </summary>
    /// <param name="requestPath">The path component of the request, still percent-encoded.</param>
    /// <returns>The resolved location inside the root and what is there, or the refusal and
    /// its reason. A refusal is a result, never an exception.</returns>
    public ContentPathMapping MapRequestPath(string requestPath)
    {
        ArgumentNullException.ThrowIfNull(requestPath);
        ContentPathRefusal refusal = RequestPathSegments.Split(requestPath, out string[] segments);
        if (refusal != ContentPathRefusal.None)
        {
            return ContentPathMapping.Refused(refusal);
        }

        string resolvedRoot = fileSystem.ResolveFinalPath(ServedRoot);
        string resolved = fileSystem.ResolveFinalPath(Path.Join([ServedRoot, .. segments]));
        if (!IsInsideOrAt(resolved, resolvedRoot))
        {
            return ContentPathMapping.Refused(ContentPathRefusal.ResolvesOutsideRoot);
        }

        return ContentPathMapping.Mapped(resolved, fileSystem.GetEntryKind(resolved));
    }

    /// <summary>
    /// Asks the file-system seam, now, what is at a mapped location.
    /// </summary>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <returns>A file, a directory, or nothing.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentEntryKind GetEntryKind(ContentPathMapping mapping) =>
        fileSystem.GetEntryKind(RequireLocation(mapping));

    /// <summary>
    /// Reports the length and last modification time of the file at a mapped location.
    /// </summary>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <returns>The file's status; <see langword="null"/> when the location is a directory
    /// or holds nothing.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentFileStatus? GetFileStatus(ContentPathMapping mapping)
    {
        string location = RequireLocation(mapping);
        if (fileSystem.GetEntryKind(location) != ContentEntryKind.File)
        {
            return null;
        }

        return new ContentFileStatus(
            fileSystem.GetFileLength(location),
            fileSystem.GetLastWriteTimeUtc(location).ToUniversalTime());
    }

    /// <summary>
    /// Copies the bytes of <paramref name="range"/> from the file at a mapped location to
    /// <paramref name="destination"/>, reading and writing asynchronously.
    /// </summary>
    /// <remarks>
    /// The read is a copy to a destination rather than a returned stream, so the file is
    /// opened and closed here and a caller cannot leak it. A protocol server selects the range
    /// first, from <see cref="GetFileStatus(ContentPathMapping)"/>, so it can announce the
    /// length before any byte is written. If the file shrank since, the copy stops at its
    /// end, and the count returned is less than <see cref="ContentByteRange.ByteCount"/>.
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set, for a file.</param>
    /// <param name="range">A satisfiable range, from <see cref="ContentByteRange.WholeFile(long)"/>
    /// or <see cref="ContentByteRange.Select(long, long, long)"/>.</param>
    /// <param name="destination">Where the bytes are written.</param>
    /// <param name="cancellationToken">Checked before the file is opened and before every
    /// read; cancellation throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>How many bytes were copied.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal, or
    /// <paramref name="range"/> is unsatisfiable.</exception>
    public async Task<long> CopyFileBytesAsync(
        ContentPathMapping mapping,
        ContentByteRange range,
        Stream destination,
        CancellationToken cancellationToken)
    {
        string location = RequireLocation(mapping);
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(destination);
        if (!range.IsSatisfiable)
        {
            throw new ArgumentException("An unsatisfiable range has no bytes to copy.", nameof(range));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using Stream source = fileSystem.OpenFileForAsyncRead(location);
        source.Position = range.FirstByte;
        return await CopyAtMostAsync(source, destination, range.ByteCount, cancellationToken);
    }

    private static async Task<long> CopyAtMostAsync(Stream source, Stream destination, long byteCount, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[(int)Math.Min(CopyBufferSize, Math.Max(byteCount, 1))];
        long copied = 0;
        while (copied < byteCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int wanted = (int)Math.Min(buffer.Length, byteCount - copied);
            int read = await source.ReadAsync(buffer.AsMemory(0, wanted), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
        }

        return copied;
    }

    private static string RequireLocation(ContentPathMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return mapping.Location ?? throw new ArgumentException("The request path was refused, so it has no location.", nameof(mapping));
    }

    private static bool IsInsideOrAt(string path, string root)
    {
        string trimmedRoot = Path.TrimEndingDirectorySeparator(root);
        if (string.Equals(Path.TrimEndingDirectorySeparator(path), trimmedRoot, StringComparison.Ordinal))
        {
            return true;
        }

        string prefix = Path.EndsInDirectorySeparator(trimmedRoot)
            ? trimmedRoot
            : trimmedRoot + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
