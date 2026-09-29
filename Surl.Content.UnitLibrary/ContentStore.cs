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
/// copies the whole file or a <see cref="ContentByteRange"/> of it to a destination stream;
/// <see cref="ListDirectory(ContentPathMapping, CancellationToken)"/> lists a directory's
/// entries in ordinal order of their names; and
/// <see cref="WriteUploadAsync(ContentPathMapping, Stream, CancellationToken)"/> writes an
/// upload, within <see cref="ContentExposureOptions.MaxUploadBytes"/>. Every look, every read
/// and every write goes through the seam.
/// </para>
/// <para>
/// <see cref="ExposureOptions"/> apply ADR-0006 section 2 for every protocol server: a
/// dot-file, a path through a symbolic link that is not followed, and a listing that is off
/// are all answered exactly as a path that does not exist; an upload that is off is
/// <see cref="ContentUploadResult.NotPermitted"/>.
/// </para>
/// </remarks>
public sealed class ContentStore
{
    private const int CopyBufferSize = 81920;

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates a content store that serves <paramref name="servedRoot"/> with
    /// <see cref="ContentExposureOptions.ServeEverythingInsideTheRoot"/>, as the store served
    /// before it took exposure options.
    /// </summary>
    /// <param name="servedRoot">The full path of the directory being served.</param>
    /// <param name="fileSystem">The seam every look at the served root goes through.</param>
    public ContentStore(string servedRoot, IContentFileSystem fileSystem)
        : this(servedRoot, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot)
    {
    }

    /// <summary>
    /// Creates a content store that serves <paramref name="servedRoot"/>, exposing what
    /// <paramref name="exposureOptions"/> allow.
    /// </summary>
    /// <param name="servedRoot">The full path of the directory being served.</param>
    /// <param name="fileSystem">The seam every look at the served root goes through.</param>
    /// <param name="exposureOptions">What the store exposes and the largest upload it accepts;
    /// <c>new ContentExposureOptions()</c> is ADR-0006's defaults.</param>
    public ContentStore(string servedRoot, IContentFileSystem fileSystem, ContentExposureOptions exposureOptions)
    {
        ArgumentException.ThrowIfNullOrEmpty(servedRoot);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(exposureOptions);
        ServedRoot = servedRoot;
        this.fileSystem = fileSystem;
        ExposureOptions = exposureOptions;
    }

    /// <summary>
    /// What the store exposes and the largest upload it accepts.
    /// </summary>
    public ContentExposureOptions ExposureOptions { get; }

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

        string unresolved = Path.Join([resolvedRoot, .. segments]);
        if (IsHiddenByExposureOptions(segments, resolved, unresolved))
        {
            return ContentPathMapping.AnsweredAsAbsent(unresolved);
        }

        return ContentPathMapping.Mapped(resolved, fileSystem.GetEntryKind(resolved));
    }

    /// <summary>
    /// Asks the file-system seam, now, what is at a mapped location.
    /// </summary>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <returns>A file, a directory, or nothing; nothing, without asking, when the exposure
    /// options hide the location.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentEntryKind GetEntryKind(ContentPathMapping mapping) =>
        CurrentEntryKind(mapping, RequireLocation(mapping));

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
        if (CurrentEntryKind(mapping, location) != ContentEntryKind.File)
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
    /// <exception cref="FileNotFoundException">The exposure options hide the location, so it
    /// is answered as a file that does not exist.</exception>
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

        if (mapping.IsAnsweredAsAbsent)
        {
            throw new FileNotFoundException("Nothing is served at the mapped location.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using Stream source = fileSystem.OpenFileForAsyncRead(location);
        source.Position = range.FirstByte;
        return await CopyAtMostAsync(source, destination, range.ByteCount, cancellationToken);
    }

    /// <summary>
    /// Lists the entries of the directory at a mapped location.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each entry carries its name, whether it is a file or a directory, a file's length and
    /// its UTC modification time. Entries are in ordinal order of their names (UTF-16 code
    /// unit by code unit, case-sensitive), so a listing reads the same on Windows, Linux and
    /// macOS whatever order the file system returns them in.
    /// </para>
    /// <para>
    /// An entry is left out when its name is one <see cref="MapRequestPath(string)"/> would
    /// refuse as a segment (so every listed name can be asked for), when it is a symbolic
    /// link whose final target resolves outside the served root, or when nothing is there
    /// by the time it is looked at (a dangling link, or an entry deleted meanwhile). A link
    /// whose final target is inside the root is listed under its own name, with its
    /// target's kind, length and modification time.
    /// </para>
    /// <para>
    /// The <see cref="ExposureOptions"/> apply too. When
    /// <see cref="ContentExposureOptions.ListDirectories"/> is off, every listing is answered
    /// as absent: <see cref="ContentDirectoryListing.LocationKind"/> is
    /// <see cref="ContentEntryKind.None"/>, as for a path that does not exist, and the seam is
    /// not asked. A dot-file is left out unless
    /// <see cref="ContentExposureOptions.ServeDotFiles"/> is on, and a symbolic link inside the
    /// root unless <see cref="ContentExposureOptions.FollowSymbolicLinks"/> is on.
    /// </para>
    /// <para>
    /// Protocol-neutral: no listing format is produced here; each protocol server formats
    /// the entries its own way.
    /// </para>
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <param name="cancellationToken">Checked before the directory is read and before every
    /// entry; cancellation throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>The directory's entries; when the location is a file or holds nothing, a
    /// result with <see cref="ContentDirectoryListing.IsListed"/> clear and
    /// <see cref="ContentDirectoryListing.LocationKind"/> saying which.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentDirectoryListing ListDirectory(ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        string location = RequireLocation(mapping);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ExposureOptions.ListDirectories)
        {
            return ContentDirectoryListing.NotADirectory(ContentEntryKind.None);
        }

        ContentEntryKind locationKind = CurrentEntryKind(mapping, location);
        if (locationKind != ContentEntryKind.Directory)
        {
            return ContentDirectoryListing.NotADirectory(locationKind);
        }

        string resolvedRoot = fileSystem.ResolveFinalPath(ServedRoot);
        var entries = new List<ContentDirectoryEntry>();
        foreach (string name in fileSystem.EnumerateDirectoryEntryNames(location))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContentDirectoryEntry? entry = DescribeDirectoryEntry(location, name, resolvedRoot);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        entries.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        return ContentDirectoryListing.Listed(entries);
    }

    /// <summary>
    /// Writes an upload read from <paramref name="source"/> to the file at a mapped location,
    /// creating it or replacing what it held, within
    /// <see cref="ContentExposureOptions.MaxUploadBytes"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing is written, and <see cref="ContentUploadResult.NotPermitted"/> returned, when
    /// <see cref="ContentExposureOptions.AllowUploads"/> is off, when the exposure options hide
    /// the location (a dot-file, or a path through a symbolic link that is not followed), when
    /// the location is a directory, or when it is not directly inside an existing directory.
    /// </para>
    /// <para>
    /// Bytes are counted as they are read. Each read asks for no more than one byte past the
    /// limit, so reading stops at most one byte past it: the upload is then
    /// <see cref="ContentUploadResult.TooLarge"/>, and the partial file is deleted through the
    /// seam. An upload of exactly the limit is written. A write that throws, cancellation
    /// included, deletes the partial file too before the exception goes on. Either way a file
    /// the upload replaced is gone.
    /// </para>
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <param name="source">The upload's bytes, read to its end.</param>
    /// <param name="cancellationToken">Checked before the file is created and before every
    /// read; cancellation throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>Whether the upload was written, not permitted, or too large.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public async Task<ContentUploadResult> WriteUploadAsync(ContentPathMapping mapping, Stream source, CancellationToken cancellationToken)
    {
        string location = RequireLocation(mapping);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsUploadPermitted(mapping, location))
        {
            return ContentUploadResult.NotPermitted;
        }

        bool isWithinLimit;
        Stream destination = fileSystem.CreateFileForAsyncWrite(location);
        try
        {
            await using (destination)
            {
                isWithinLimit = await CopyWithinUploadLimitAsync(source, destination, cancellationToken);
            }
        }
        catch (Exception)
        {
            fileSystem.DeleteFile(location);
            throw;
        }

        if (isWithinLimit)
        {
            return ContentUploadResult.Written;
        }

        fileSystem.DeleteFile(location);
        return ContentUploadResult.TooLarge;
    }

    private bool IsUploadPermitted(ContentPathMapping mapping, string location) =>
        ExposureOptions.AllowUploads
        && !mapping.IsAnsweredAsAbsent
        && fileSystem.GetEntryKind(location) != ContentEntryKind.Directory
        && fileSystem.GetEntryKind(ParentDirectoryOf(location)) == ContentEntryKind.Directory;

    // The span overload of GetDirectoryName keeps the path spelled as the seam spelled it; the
    // string overload rewrites every separator on Windows, and the seam compares ordinally.
    private static string ParentDirectoryOf(string location)
    {
        ReadOnlySpan<char> parent = Path.GetDirectoryName(location.AsSpan());
        return parent.IsEmpty ? location : parent.ToString();
    }

    private async Task<bool> CopyWithinUploadLimitAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        long limit = ExposureOptions.MaxUploadBytes;
        byte[] buffer = new byte[CopyBufferSize];
        long received = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int wanted = limit == 0 ? buffer.Length : (int)Math.Min(buffer.Length, limit - received + 1);
            int read = await source.ReadAsync(buffer.AsMemory(0, wanted), cancellationToken);
            if (read == 0)
            {
                return true;
            }

            received += read;
            if (limit != 0 && received > limit)
            {
                return false;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private ContentEntryKind CurrentEntryKind(ContentPathMapping mapping, string location) =>
        mapping.IsAnsweredAsAbsent ? ContentEntryKind.None : fileSystem.GetEntryKind(location);

    private bool IsHiddenByExposureOptions(string[] segments, string resolved, string unresolved) =>
        (!ExposureOptions.ServeDotFiles && Array.Exists(segments, IsDotFileName))
        || (!ExposureOptions.FollowSymbolicLinks && !IsSamePath(resolved, unresolved));

    private bool IsEntryHiddenByExposureOptions(string name, string resolved, string unresolved) =>
        (!ExposureOptions.ServeDotFiles && IsDotFileName(name))
        || (!ExposureOptions.FollowSymbolicLinks && !IsSamePath(resolved, unresolved));

    private static bool IsDotFileName(string name) => name.StartsWith('.');

    private static bool IsSamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.Ordinal);

    private ContentDirectoryEntry? DescribeDirectoryEntry(string directory, string name, string resolvedRoot)
    {
        if (RequestPathSegments.CheckSegment(name) != ContentPathRefusal.None)
        {
            return null;
        }

        string unresolved = Path.Join(directory, name);
        string resolved = fileSystem.ResolveFinalPath(unresolved);
        if (!IsInsideOrAt(resolved, resolvedRoot) || IsEntryHiddenByExposureOptions(name, resolved, unresolved))
        {
            return null;
        }

        return fileSystem.GetEntryKind(resolved) switch
        {
            ContentEntryKind.File => new ContentDirectoryEntry(
                name,
                ContentEntryKind.File,
                fileSystem.GetFileLength(resolved),
                fileSystem.GetLastWriteTimeUtc(resolved).ToUniversalTime()),
            ContentEntryKind.Directory => new ContentDirectoryEntry(
                name,
                ContentEntryKind.Directory,
                null,
                fileSystem.GetLastWriteTimeUtc(resolved).ToUniversalTime()),
            _ => null,
        };
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
