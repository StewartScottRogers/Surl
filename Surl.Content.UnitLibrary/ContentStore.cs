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
/// A trailing <c>/</c> names a directory. Upstream curl sends <c>/file.txt/</c> as written,
/// so when a file is at that location it maps with <see cref="ContentPathMapping.EntryKind"/>
/// <see cref="ContentEntryKind.None"/>, answered exactly as a path that does not exist, as a
/// POSIX file system refuses it with <c>ENOTDIR</c>; every later look at the mapping answers a
/// file there the same way, and an upload to it is
/// <see cref="ContentUploadResult.NotPermitted"/> (ADR-0018). A directory answers with or
/// without the trailing <c>/</c>.
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
/// upload, within <see cref="ContentExposureOptions.MaxUploadBytes"/>, and
/// <see cref="AppendUploadAsync(ContentPathMapping, Stream, CancellationToken)"/> appends one;
/// <see cref="DeleteFile(ContentPathMapping)"/>,
/// <see cref="RenameEntry(ContentPathMapping, ContentPathMapping)"/>,
/// <see cref="CreateDirectory(ContentPathMapping)"/> and
/// <see cref="RemoveEmptyDirectory(ContentPathMapping)"/> change the root's entries, each
/// answering a <see cref="ContentChangeResult"/>, and each needing
/// <see cref="ContentExposureOptions.AllowUploads"/> as every write does. Every look, every
/// read and every write goes through the seam.
/// </para>
/// <para>
/// <see cref="ExposureOptions"/> apply ADR-0006 section 2 for every protocol server: a
/// dot-file, a path through a symbolic link that is not followed, and a listing that is off
/// are all answered exactly as a path that does not exist; an upload that is off is
/// <see cref="ContentUploadResult.NotPermitted"/>.
/// </para>
/// <para>
/// The <c>.surl</c> folder at the top of the served root holds Surl's own service state, and
/// is never served, whatever the exposure options say (ADR-0031 decision 5). A path whose
/// first segment is <c>.surl</c>, compared ignoring case on every platform, is answered
/// exactly as a path that does not exist, and an upload to it is
/// <see cref="ContentUploadResult.NotPermitted"/>; so is a path whose symbolic links finally
/// resolve into that folder. A listing of the root leaves it out. Only the whole first segment
/// is reserved: <c>/.surlx</c>, <c>/.surl-upload-</c>... and <c>/sub/.surl</c> are ordinary
/// dot-files.
/// </para>
/// </remarks>
public sealed class ContentStore
{
    private const int CopyBufferSize = 81920;

    private const string ServiceStateFolderName = ".surl";

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates a content store that serves <paramref name="servedRoot"/>, exposing what
    /// <paramref name="exposureOptions"/> allow.
    /// </summary>
    /// <param name="servedRoot">The full path of the directory being served.</param>
    /// <param name="fileSystem">The seam every look at the served root goes through.</param>
    /// <param name="exposureOptions">What the store exposes and the largest upload it accepts;
    /// <c>new ContentExposureOptions()</c> is ADR-0006's defaults.</param>
    /// <exception cref="ArgumentException"><paramref name="servedRoot"/> is empty, or is
    /// relative to the current directory (see <see cref="ServedRoot"/>).</exception>
    public ContentStore(string servedRoot, IContentFileSystem fileSystem, ContentExposureOptions exposureOptions)
    {
        ArgumentException.ThrowIfNullOrEmpty(servedRoot);
        if (!IsIndependentOfTheCurrentDirectory(servedRoot))
        {
            throw new ArgumentException("The served root must be a full path, not one relative to the current directory.", nameof(servedRoot));
        }

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
    /// <remarks>
    /// The constructor refuses, with <see cref="ArgumentException"/>, a root whose meaning
    /// depends on the current directory: a relative path such as <c>srv</c>, and on Windows a
    /// drive-relative one such as <c>C:</c> or <c>C:srv</c>. It accepts a fully qualified path
    /// and a path that starts with a directory separator, such as <c>/srv/www</c>, which on
    /// Windows is rooted on the current drive; the root is kept as given, never rewritten
    /// (ADR-0018).
    /// </remarks>
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
        if (IsInServiceStateFolder(resolved, unresolved, resolvedRoot)
            || IsHiddenByExposureOptions(segments, resolved, unresolved))
        {
            return ContentPathMapping.AnsweredAsAbsent(unresolved);
        }

        bool namesADirectory = requestPath.EndsWith('/');
        return ContentPathMapping.Mapped(
            resolved,
            EntryKindAnswering(namesADirectory, fileSystem.GetEntryKind(resolved)),
            namesADirectory);
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
    /// <exception cref="FileNotFoundException">The exposure options hide the location, or the
    /// request path ended in <c>/</c>, so it is answered as a file that does not exist.</exception>
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

        if (mapping.IsAnsweredAsAbsent || mapping.NamesADirectory)
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

        return ListEntriesAt(mapping, location, cancellationToken);
    }

    /// <summary>
    /// Lists the entries of the directory at a mapped location whatever
    /// <see cref="ContentExposureOptions.ListDirectories"/> says, for a protocol's own lookup
    /// that is not a directory listing, such as DICT's <c>MATCH</c> (ADR-0011, section 4).
    /// </summary>
    /// <remarks>
    /// Every rule of <see cref="ListDirectory"/> but the listing switch applies: a dot-file is
    /// left out unless <see cref="ContentExposureOptions.ServeDotFiles"/> is on, a symbolic
    /// link inside the root unless <see cref="ContentExposureOptions.FollowSymbolicLinks"/> is
    /// on, and a link out of the root always.
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <param name="cancellationToken">Checked before the directory is read and before every
    /// entry; cancellation throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>The directory's entries; when the location is a file or holds nothing, a
    /// result with <see cref="ContentDirectoryListing.IsListed"/> clear and
    /// <see cref="ContentDirectoryListing.LocationKind"/> saying which.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentDirectoryListing ListDirectoryWhateverTheListingSwitchSays(ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        string location = RequireLocation(mapping);
        cancellationToken.ThrowIfCancellationRequested();
        return ListEntriesAt(mapping, location, cancellationToken);
    }

    private ContentDirectoryListing ListEntriesAt(ContentPathMapping mapping, string location, CancellationToken cancellationToken)
    {
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
    /// the request path ended in <c>/</c>, when the location is a directory, or when it is not directly inside an existing directory.
    /// </para>
    /// <para>
    /// The upload is written to a temporary dot-file beside the target, named
    /// <c>.surl-upload-</c> and a fresh GUID, and renamed over the target through
    /// <see cref="IContentFileSystem.MoveFileReplacing(string, string)"/> only once all of it
    /// is written. Bytes are counted as they are read. Each read asks for no more than one byte
    /// past the limit, so reading stops at most one byte past it: the upload is then
    /// <see cref="ContentUploadResult.TooLarge"/>, and the temporary file is deleted through the
    /// seam. An upload of exactly the limit is written. A write or rename that throws,
    /// cancellation included, deletes the temporary file too before the exception goes on.
    /// Either way a file the upload would replace is kept, byte for byte, until the upload is
    /// written.
    /// </para>
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <param name="source">The upload's bytes, read to its end.</param>
    /// <param name="cancellationToken">Checked before the temporary file is created and before
    /// every read; cancellation throws <see cref="OperationCanceledException"/>.</param>
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

        return await WriteThroughTemporaryFileAsync(location, source, appending: false, cancellationToken);
    }

    /// <summary>
    /// Appends an upload read from <paramref name="source"/> to the file at a mapped location,
    /// creating it when nothing is there, within <see cref="ContentExposureOptions.MaxUploadBytes"/>
    /// counted over the existing length and the appended bytes together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every rule of <see cref="WriteUploadAsync(ContentPathMapping, Stream, CancellationToken)"/>
    /// applies: the same locations are <see cref="ContentUploadResult.NotPermitted"/>, and the
    /// result is written to a temporary dot-file beside the target and renamed over it only once
    /// all of it is written. The temporary file is first given a copy of the existing file's
    /// bytes, then the upload's.
    /// </para>
    /// <para>
    /// When the existing length and the bytes read pass the limit (ADR-0006 section 5), the
    /// append is <see cref="ContentUploadResult.TooLarge"/>: the temporary file is deleted and
    /// the existing file is kept, byte for byte. An existing file already past the limit makes
    /// every append too large, without a byte of the upload read.
    /// </para>
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <param name="source">The appended bytes, read to its end.</param>
    /// <param name="cancellationToken">Checked before the temporary file is created and before
    /// every read; cancellation throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>Whether the append was written, not permitted, or too large.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public async Task<ContentUploadResult> AppendUploadAsync(ContentPathMapping mapping, Stream source, CancellationToken cancellationToken)
    {
        string location = RequireLocation(mapping);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsUploadPermitted(mapping, location))
        {
            return ContentUploadResult.NotPermitted;
        }

        return await WriteThroughTemporaryFileAsync(location, source, appending: true, cancellationToken);
    }

    /// <summary>
    /// Deletes the file at a mapped location.
    /// </summary>
    /// <remarks>
    /// <see cref="ContentChangeResult.NotPermitted"/> when
    /// <see cref="ContentExposureOptions.AllowUploads"/> is off; otherwise
    /// <see cref="ContentChangeResult.Absent"/> for a location that is hidden, holds a directory
    /// or nothing, or is asked for with a trailing <c>/</c>. A symbolic link that is followed is
    /// resolved, so its final target is deleted.
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <returns><see cref="ContentChangeResult.Done"/>, <see cref="ContentChangeResult.Absent"/>
    /// or <see cref="ContentChangeResult.NotPermitted"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentChangeResult DeleteFile(ContentPathMapping mapping)
    {
        string location = RequireLocation(mapping);
        if (!ExposureOptions.AllowUploads)
        {
            return ContentChangeResult.NotPermitted;
        }

        if (CurrentEntryKind(mapping, location) != ContentEntryKind.File)
        {
            return ContentChangeResult.Absent;
        }

        fileSystem.DeleteFile(location);
        return ContentChangeResult.Done;
    }

    /// <summary>
    /// Renames the file or directory at one mapped location to another, replacing a file there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ContentChangeResult.NotPermitted"/> when
    /// <see cref="ContentExposureOptions.AllowUploads"/> is off. Otherwise, in this order:
    /// <see cref="ContentChangeResult.Absent"/> when the source is hidden or holds nothing (or a
    /// file asked for with a trailing <c>/</c>); <see cref="ContentChangeResult.NotPermitted"/>
    /// when the source or the destination is the served root, the destination is hidden or
    /// under <c>/.surl</c>, a file's destination ends in <c>/</c>, or a directory's destination
    /// is inside it;
    /// <see cref="ContentChangeResult.Done"/>, changing nothing, when both name one location;
    /// <see cref="ContentChangeResult.NoSuchDirectory"/> when the destination is not directly
    /// inside an existing directory; and <see cref="ContentChangeResult.Exists"/> when a
    /// directory is at the destination, or a file is and the source is a directory.
    /// </para>
    /// <para>
    /// A file is renamed through <see cref="IContentFileSystem.MoveFileReplacing(string, string)"/>
    /// and a directory, with everything inside it, through
    /// <see cref="IContentFileSystem.MoveDirectory(string, string)"/>. Symbolic links that are
    /// followed are resolved, so the source's final target is what moves.
    /// </para>
    /// </remarks>
    /// <param name="source">The mapping of the entry to rename.</param>
    /// <param name="destination">The mapping of its new name.</param>
    /// <returns>What became of the rename.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> or
    /// <paramref name="destination"/> is a refusal.</exception>
    public ContentChangeResult RenameEntry(ContentPathMapping source, ContentPathMapping destination)
    {
        string from = RequireLocation(source);
        string to = RequireLocation(destination);
        if (!ExposureOptions.AllowUploads)
        {
            return ContentChangeResult.NotPermitted;
        }

        ContentEntryKind sourceKind = CurrentEntryKind(source, from);
        if (sourceKind == ContentEntryKind.None)
        {
            return ContentChangeResult.Absent;
        }

        ContentChangeResult refusal = RenameRefusal(sourceKind, from, destination, to);
        if (refusal != ContentChangeResult.Done || IsSamePath(from, to))
        {
            return refusal;
        }

        MoveEntry(sourceKind, from, to);
        return ContentChangeResult.Done;
    }

    /// <summary>
    /// Creates a directory at a mapped location, directly inside an existing directory.
    /// </summary>
    /// <remarks>
    /// <see cref="ContentChangeResult.NotPermitted"/> when
    /// <see cref="ContentExposureOptions.AllowUploads"/> is off or the location is hidden or
    /// under <c>/.surl</c>; <see cref="ContentChangeResult.Exists"/> when a file or directory is
    /// already there; <see cref="ContentChangeResult.NoSuchDirectory"/> when the directory above
    /// it does not exist. A directory above is never created.
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <returns>What became of the creation.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentChangeResult CreateDirectory(ContentPathMapping mapping)
    {
        string location = RequireLocation(mapping);
        if (!ExposureOptions.AllowUploads || mapping.IsAnsweredAsAbsent)
        {
            return ContentChangeResult.NotPermitted;
        }

        if (fileSystem.GetEntryKind(location) != ContentEntryKind.None)
        {
            return ContentChangeResult.Exists;
        }

        if (!IsDirectlyInsideADirectory(location))
        {
            return ContentChangeResult.NoSuchDirectory;
        }

        fileSystem.CreateDirectory(location);
        return ContentChangeResult.Done;
    }

    /// <summary>
    /// Removes the empty directory at a mapped location.
    /// </summary>
    /// <remarks>
    /// <see cref="ContentChangeResult.NotPermitted"/> when
    /// <see cref="ContentExposureOptions.AllowUploads"/> is off or the location is the served
    /// root; <see cref="ContentChangeResult.Absent"/> when it is hidden or holds a file or
    /// nothing; <see cref="ContentChangeResult.NotEmpty"/> when any entry is inside it, one the
    /// exposure options hide included, so nothing hidden is ever removed with it.
    /// </remarks>
    /// <param name="mapping">A mapping this content store returned with
    /// <see cref="ContentPathMapping.IsMapped"/> set.</param>
    /// <returns>What became of the removal.</returns>
    /// <exception cref="ArgumentException"><paramref name="mapping"/> is a refusal.</exception>
    public ContentChangeResult RemoveEmptyDirectory(ContentPathMapping mapping)
    {
        string location = RequireLocation(mapping);
        if (!ExposureOptions.AllowUploads || IsServedRoot(location))
        {
            return ContentChangeResult.NotPermitted;
        }

        if (CurrentEntryKind(mapping, location) != ContentEntryKind.Directory)
        {
            return ContentChangeResult.Absent;
        }

        if (fileSystem.EnumerateDirectoryEntryNames(location).Any())
        {
            return ContentChangeResult.NotEmpty;
        }

        fileSystem.RemoveEmptyDirectory(location);
        return ContentChangeResult.Done;
    }

    // Done means nothing refuses the rename.
    private ContentChangeResult RenameRefusal(ContentEntryKind sourceKind, string from, ContentPathMapping destination, string to)
    {
        bool isForbidden = IsServedRoot(from)
            || IsServedRoot(to)
            || destination.IsAnsweredAsAbsent
            || IsForbiddenDestinationFor(sourceKind, from, destination, to);
        return isForbidden ? ContentChangeResult.NotPermitted : RenameDestinationRefusal(sourceKind, from, to);
    }

    // A file cannot take a name that ends in '/', and a directory cannot move inside itself.
    private static bool IsForbiddenDestinationFor(ContentEntryKind sourceKind, string from, ContentPathMapping destination, string to) =>
        sourceKind == ContentEntryKind.File
            ? destination.NamesADirectory
            : !IsSamePath(from, to) && IsInsideOrAt(to, from);

    private ContentChangeResult RenameDestinationRefusal(ContentEntryKind sourceKind, string from, string to)
    {
        if (IsSamePath(from, to))
        {
            return ContentChangeResult.Done;
        }

        if (!IsDirectlyInsideADirectory(to))
        {
            return ContentChangeResult.NoSuchDirectory;
        }

        ContentEntryKind destinationKind = fileSystem.GetEntryKind(to);
        bool isInTheWay = destinationKind == ContentEntryKind.Directory
            || (destinationKind == ContentEntryKind.File && sourceKind == ContentEntryKind.Directory);
        return isInTheWay ? ContentChangeResult.Exists : ContentChangeResult.Done;
    }

    private void MoveEntry(ContentEntryKind sourceKind, string from, string to)
    {
        if (sourceKind == ContentEntryKind.Directory)
        {
            fileSystem.MoveDirectory(from, to);
        }
        else
        {
            fileSystem.MoveFileReplacing(from, to);
        }
    }

    private bool IsServedRoot(string location) =>
        IsSamePath(location, fileSystem.ResolveFinalPath(ServedRoot));

    private bool IsDirectlyInsideADirectory(string location) =>
        !IsSamePath(ParentDirectoryOf(location), location)
        && fileSystem.GetEntryKind(ParentDirectoryOf(location)) == ContentEntryKind.Directory;

    private async Task<ContentUploadResult> WriteThroughTemporaryFileAsync(string location, Stream source, bool appending, CancellationToken cancellationToken)
    {
        string temporaryLocation = TemporaryUploadLocationBeside(location);
        try
        {
            if (!await WriteWithinUploadLimitAsync(temporaryLocation, location, appending, source, cancellationToken))
            {
                fileSystem.DeleteFile(temporaryLocation);
                return ContentUploadResult.TooLarge;
            }

            fileSystem.MoveFileReplacing(temporaryLocation, location);
            return ContentUploadResult.Written;
        }
        catch (Exception)
        {
            fileSystem.DeleteFile(temporaryLocation);
            throw;
        }
    }

    private async Task<bool> WriteWithinUploadLimitAsync(string temporaryLocation, string location, bool appending, Stream source, CancellationToken cancellationToken)
    {
        await using Stream destination = fileSystem.CreateFileForAsyncWrite(temporaryLocation);
        long existingLength = appending && fileSystem.GetEntryKind(location) == ContentEntryKind.File
            ? await CopyExistingFileAsync(location, destination, cancellationToken)
            : 0;
        return await CopyWithinUploadLimitAsync(source, destination, existingLength, cancellationToken);
    }

    private async Task<long> CopyExistingFileAsync(string location, Stream destination, CancellationToken cancellationToken)
    {
        await using Stream existing = fileSystem.OpenFileForAsyncRead(location);
        return await CopyAtMostAsync(existing, destination, long.MaxValue, cancellationToken);
    }

    // A dot-file beside the target, so the store neither serves nor lists it by default, and the
    // rename stays inside one directory. The name leaves out the target's own, so it is never
    // longer than a file name may be, and a fresh GUID keeps two uploads to one file apart.
    private static string TemporaryUploadLocationBeside(string location) =>
        Path.Join(ParentDirectoryOf(location), $".surl-upload-{Guid.NewGuid():N}");

    private bool IsUploadPermitted(ContentPathMapping mapping, string location) =>
        ExposureOptions.AllowUploads
        && !mapping.IsAnsweredAsAbsent
        && !mapping.NamesADirectory
        && fileSystem.GetEntryKind(location) != ContentEntryKind.Directory
        && fileSystem.GetEntryKind(ParentDirectoryOf(location)) == ContentEntryKind.Directory;

    // The span overload of GetDirectoryName keeps the path spelled as the seam spelled it; the
    // string overload rewrites every separator on Windows, and the seam compares ordinally.
    private static string ParentDirectoryOf(string location)
    {
        ReadOnlySpan<char> parent = Path.GetDirectoryName(location.AsSpan());
        return parent.IsEmpty ? location : parent.ToString();
    }

    // Counts from alreadyWritten, the bytes an append copied from the existing file, so the limit
    // covers the whole file; a count already past it reads nothing.
    private async Task<bool> CopyWithinUploadLimitAsync(Stream source, Stream destination, long alreadyWritten, CancellationToken cancellationToken)
    {
        long limit = ExposureOptions.MaxUploadBytes;
        byte[] buffer = new byte[CopyBufferSize];
        long received = alreadyWritten;
        while (IsWithinUploadLimit(received))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int wanted = limit == 0 ? buffer.Length : (int)Math.Min(buffer.Length, limit - received + 1);
            int read = await source.ReadAsync(buffer.AsMemory(0, wanted), cancellationToken);
            if (read == 0)
            {
                return true;
            }

            received += read;
            if (IsWithinUploadLimit(received))
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }

        return false;
    }

    private bool IsWithinUploadLimit(long byteCount) =>
        ExposureOptions.MaxUploadBytes == 0 || byteCount <= ExposureOptions.MaxUploadBytes;

    private ContentEntryKind CurrentEntryKind(ContentPathMapping mapping, string location) =>
        mapping.IsAnsweredAsAbsent
            ? ContentEntryKind.None
            : EntryKindAnswering(mapping.NamesADirectory, fileSystem.GetEntryKind(location));

    // A request path that ends in '/' names a directory, so a file there answers it as nothing,
    // as a POSIX file system answers "file.txt/" with ENOTDIR (ADR-0018).
    private static ContentEntryKind EntryKindAnswering(bool namesADirectory, ContentEntryKind kind) =>
        namesADirectory && kind == ContentEntryKind.File ? ContentEntryKind.None : kind;

    private bool IsHiddenByExposureOptions(string[] segments, string resolved, string unresolved) =>
        (!ExposureOptions.ServeDotFiles && Array.Exists(segments, IsDotFileName))
        || (!ExposureOptions.FollowSymbolicLinks && !IsSamePath(resolved, unresolved));

    private bool IsEntryHiddenByExposureOptions(string name, string resolved, string unresolved) =>
        (!ExposureOptions.ServeDotFiles && IsDotFileName(name))
        || (!ExposureOptions.FollowSymbolicLinks && !IsSamePath(resolved, unresolved));

    private static bool IsDotFileName(string name) => name.StartsWith('.');

    // ADR-0031 decision 5: the path as asked for and the path its links resolve to are both
    // checked, so neither a request for /.SURL/lock nor a link into <root>/.surl reaches it.
    private static bool IsInServiceStateFolder(string resolved, string unresolved, string resolvedRoot) =>
        HasServiceStateFolderAsFirstSegment(resolved, resolvedRoot)
        || HasServiceStateFolderAsFirstSegment(unresolved, resolvedRoot);

    // The path is inside or at the root, spelled from its start as the root is (IsInsideOrAt).
    private static bool HasServiceStateFolderAsFirstSegment(string path, string resolvedRoot)
    {
        string trimmedRoot = Path.TrimEndingDirectorySeparator(resolvedRoot);
        ReadOnlySpan<char> relative = path.AsSpan(trimmedRoot.Length)
            .TrimStart([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        int separator = relative.IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        ReadOnlySpan<char> firstSegment = separator < 0 ? relative : relative[..separator];
        return firstSegment.Equals(ServiceStateFolderName, StringComparison.OrdinalIgnoreCase);
    }

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
        return IsEntryListed(name, resolved, unresolved, resolvedRoot) ? DescribeListedEntry(name, resolved) : null;
    }

    private bool IsEntryListed(string name, string resolved, string unresolved, string resolvedRoot) =>
        IsInsideOrAt(resolved, resolvedRoot)
        && !IsInServiceStateFolder(resolved, unresolved, resolvedRoot)
        && !IsEntryHiddenByExposureOptions(name, resolved, unresolved);

    private ContentDirectoryEntry? DescribeListedEntry(string name, string resolved)
    {
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

    private static bool IsIndependentOfTheCurrentDirectory(string servedRoot) =>
        Path.IsPathFullyQualified(servedRoot)
        || servedRoot[0] == Path.DirectorySeparatorChar
        || servedRoot[0] == Path.AltDirectorySeparatorChar;

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
