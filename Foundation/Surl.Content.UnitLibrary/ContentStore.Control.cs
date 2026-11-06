using Surl.Protocol.Abstractions;

namespace Surl.Content;

/// <content>
/// Store control for a test that uses Surl as a stand-in (ADR-0124 decision 3): seed files and
/// folders before a client connects, reset to the configured state between tests, and take and
/// restore an in-memory snapshot.
/// </content>
public sealed partial class ContentStore
{
    private const string OnDiskMessage =
        "The content store is on disk (--directory): reset, snapshot and restore are refused so a test never empties a real directory.";

    // Who seeds: the test itself, not a peer, so it reaches every share and is not held to
    // --allow-uploads, readonly or share access (ADR-0124 decision 3).
    private static readonly ContentCaller TheTest = ContentCaller.AnonymousOn(null);

    // What the in-memory file systems held when the store was made - the files a host supplied
    // at start - or null when any file system is not in memory, and so the store is on disk.
    private readonly IReadOnlyList<(InMemoryContentFileSystem FileSystem, InMemoryContentFileSystemContents Contents)>? configuredContents;

    /// <summary>
    /// Whether <see cref="ResetAsync(ObjectMetadataIndex?, CancellationToken)"/>,
    /// <see cref="TakeSnapshotAsync(ObjectMetadataIndex?, CancellationToken)"/> and
    /// <see cref="RestoreAsync(ContentStoreSnapshot, CancellationToken)"/> are allowed: only when
    /// the root and every share are held in an <see cref="InMemoryContentFileSystem"/>. A store on
    /// disk (<c>--directory</c>) refuses them, so a test fixture can never empty a real directory
    /// (ADR-0124 decision 3).
    /// </summary>
    public bool IsControllable => configuredContents is not null;

    /// <summary>
    /// Seeds a file for a test before its client connects: writes <paramref name="contents"/> at
    /// <paramref name="requestPath"/>, creating each missing folder above it, through the same
    /// write an upload takes (ADR-0124 decision 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A seed is the test's own write, not a peer's: it needs neither
    /// <see cref="ContentExposureOptions.AllowUploads"/> nor write access to a share, reaches a
    /// <c>readonly</c> share and a share whatever its schemes, and never asks the fault plan, so an
    /// out-of-space rule cannot trip it. Every other rule of an upload holds: a path under
    /// <c>/.surl</c> or hidden by the exposure options, a trailing <c>/</c>, a directory at the
    /// path or a file where a folder above it must be is
    /// <see cref="ContentUploadResult.NotPermitted"/>, and more than
    /// <see cref="ContentExposureOptions.MaxUploadBytes"/> is
    /// <see cref="ContentUploadResult.TooLarge"/>. Seeding a store on disk is ordinary writing and
    /// is allowed.
    /// </para>
    /// <para>
    /// Each folder created publishes a <see cref="StoreChange.Seeded"/> change with no byte count,
    /// then the file publishes one with its length.
    /// </para>
    /// </remarks>
    /// <param name="requestPath">Where the file goes, as a request path: <c>/</c>-separated,
    /// percent-encoded, a share's name first to reach a share.</param>
    /// <param name="contents">The file's bytes, read to its end.</param>
    /// <param name="cancellationToken">Cancels the write; throws
    /// <see cref="OperationCanceledException"/>.</param>
    /// <returns>Whether the file was written, not permitted, or too large.</returns>
    /// <exception cref="ArgumentException"><paramref name="requestPath"/> is a path a request may not
    /// name, such as one with a <c>..</c> segment.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="contents"/> is null.</exception>
    public async Task<ContentUploadResult> SeedFileAsync(string requestPath, Stream contents, CancellationToken cancellationToken)
    {
        ContentPathMapping mapping = MapForTheTest(requestPath);
        ArgumentNullException.ThrowIfNull(contents);
        cancellationToken.ThrowIfCancellationRequested();
        string location = mapping.Location!;
        IContentFileSystem fileSystem = FileSystemOf(mapping);
        if (mapping.IsAnsweredAsAbsent
            || mapping.NamesADirectory
            || fileSystem.GetEntryKind(location) == ContentEntryKind.Directory
            || !SeedFoldersDownTo(mapping, ParentDirectoryOf(location)))
        {
            return ContentUploadResult.NotPermitted;
        }

        long? writtenLength = await WriteThroughTemporaryFileAsync(fileSystem, location, contents, appending: false, cancellationToken);
        if (writtenLength is null)
        {
            return ContentUploadResult.TooLarge;
        }

        eventsAndFaults.PublishChange(StoreChange.Seeded, PublishedNameOf(mapping, location), writtenLength);
        return ContentUploadResult.Written;
    }

    /// <summary>
    /// Seeds a folder for a test: creates the folder at <paramref name="requestPath"/> and each
    /// missing folder above it, publishing a <see cref="StoreChange.Seeded"/> change for each one
    /// created (ADR-0124 decision 3).
    /// </summary>
    /// <remarks>
    /// The test's own write, as <see cref="SeedFileAsync(string, Stream, CancellationToken)"/> is.
    /// </remarks>
    /// <param name="requestPath">Where the folder goes, as a request path.</param>
    /// <returns><see cref="ContentChangeResult.Done"/>; <see cref="ContentChangeResult.Exists"/>
    /// when a folder is already there, publishing nothing; or
    /// <see cref="ContentChangeResult.NotPermitted"/> for a path under <c>/.surl</c> or hidden by
    /// the exposure options, or a file at the path or where a folder above it must be.</returns>
    /// <exception cref="ArgumentException"><paramref name="requestPath"/> is a path a request may not
    /// name.</exception>
    public ContentChangeResult SeedFolder(string requestPath)
    {
        ContentPathMapping mapping = MapForTheTest(requestPath);
        if (mapping.IsAnsweredAsAbsent)
        {
            return ContentChangeResult.NotPermitted;
        }

        string location = mapping.Location!;
        return FileSystemOf(mapping).GetEntryKind(location) switch
        {
            ContentEntryKind.Directory => ContentChangeResult.Exists,
            ContentEntryKind.File => ContentChangeResult.NotPermitted,
            _ => SeedFoldersDownTo(mapping, location) ? ContentChangeResult.Done : ContentChangeResult.NotPermitted,
        };
    }

    /// <summary>
    /// Returns the store to its configured state (ADR-0124 decision 3): what the in-memory file
    /// systems held when the store was made - the files a host supplied at start - with every
    /// share's folder and every bucket's content path there as a folder, and no object metadata.
    /// </summary>
    /// <remarks>
    /// Waits for no one: open sessions keep their connections and see the configured state on
    /// their next command, and an upload in progress fails at its commit with the store's
    /// ordinary write failure. Publishes one <see cref="StoreChange.Reset"/> change named
    /// <c>/</c>.
    /// </remarks>
    /// <param name="metadataIndex">The store's object metadata index, emptied with it; null when
    /// the store has none.</param>
    /// <param name="cancellationToken">Cancels the wait for the index's lock, before anything
    /// changes.</param>
    /// <returns>A task that completes once the store is reset.</returns>
    /// <exception cref="InvalidOperationException">The store, or the index, is on disk
    /// (<see cref="IsControllable"/> is false).</exception>
    public async Task ResetAsync(ObjectMetadataIndex? metadataIndex, CancellationToken cancellationToken)
    {
        var configured = RequireInMemory(metadataIndex);
        if (metadataIndex is not null)
        {
            await metadataIndex.ReplaceEntriesAsync(new ObjectMetadataIndexContents(new Dictionary<string, ObjectMetadataEntry>()), cancellationToken);
        }

        foreach (var (fileSystem, contents) in configured)
        {
            fileSystem.ReplaceContents(contents);
        }

        CreateConfiguredFolders();
        eventsAndFaults.PublishChange(StoreChange.Reset, "/", null);
    }

    /// <summary>
    /// Takes an in-memory snapshot of the store's files and folders, its root's and every
    /// share's, and of <paramref name="metadataIndex"/>'s entries (ADR-0124 decision 3). Changes
    /// nothing, so publishes nothing. A file still being written is left out.
    /// </summary>
    /// <param name="metadataIndex">The store's object metadata index, copied too; null when the
    /// store has none.</param>
    /// <param name="cancellationToken">Cancels the wait for the index's lock.</param>
    /// <returns>The snapshot, which only this store restores.</returns>
    /// <exception cref="InvalidOperationException">The store, or the index, is on disk
    /// (<see cref="IsControllable"/> is false).</exception>
    public async Task<ContentStoreSnapshot> TakeSnapshotAsync(ObjectMetadataIndex? metadataIndex, CancellationToken cancellationToken)
    {
        var configured = RequireInMemory(metadataIndex);
        ObjectMetadataIndexContents? metadataContents = metadataIndex is null ? null : await metadataIndex.CaptureEntriesAsync(cancellationToken);
        return new ContentStoreSnapshot(
            this,
            [.. configured.Select(saved => (saved.FileSystem, saved.FileSystem.CaptureContents()))],
            metadataIndex,
            metadataContents);
    }

    /// <summary>
    /// Replaces the store's files and folders, and the object metadata index the snapshot copied,
    /// with <paramref name="snapshot"/>'s (ADR-0124 decision 3). Live sessions are treated as
    /// <see cref="ResetAsync(ObjectMetadataIndex?, CancellationToken)"/> treats them. Publishes one
    /// <see cref="StoreChange.Restored"/> change named <c>/</c>.
    /// </summary>
    /// <param name="snapshot">A snapshot this store took.</param>
    /// <param name="cancellationToken">Cancels the wait for the index's lock, before anything
    /// changes.</param>
    /// <returns>A task that completes once the store is restored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> was taken by another
    /// store.</exception>
    public async Task RestoreAsync(ContentStoreSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(snapshot.Store, this))
        {
            throw new ArgumentException("The snapshot was taken by another content store.", nameof(snapshot));
        }

        if (snapshot.MetadataIndex is not null)
        {
            await snapshot.MetadataIndex.ReplaceEntriesAsync(snapshot.MetadataContents!, cancellationToken);
        }

        foreach (var (fileSystem, contents) in snapshot.FileSystems)
        {
            fileSystem.ReplaceContents(contents);
        }

        eventsAndFaults.PublishChange(StoreChange.Restored, "/", null);
    }

    private IReadOnlyList<(InMemoryContentFileSystem FileSystem, InMemoryContentFileSystemContents Contents)>? CaptureInMemoryContents()
    {
        IContentFileSystem[] fileSystems = [rootContainer.FileSystem, .. sharesByName.Values.Select(named => named.Container.FileSystem)];
        return Array.TrueForAll(fileSystems, fileSystem => fileSystem is InMemoryContentFileSystem)
            ? [.. fileSystems.Distinct(ReferenceEqualityComparer.Instance).Cast<InMemoryContentFileSystem>().Select(inMemory => (inMemory, inMemory.CaptureContents()))]
            : null;
    }

    private IReadOnlyList<(InMemoryContentFileSystem FileSystem, InMemoryContentFileSystemContents Contents)> RequireInMemory(ObjectMetadataIndex? metadataIndex) =>
        metadataIndex?.IndexPath is null && configuredContents is not null
            ? configuredContents
            : throw new InvalidOperationException(OnDiskMessage);

    private ContentPathMapping MapForTheTest(string requestPath)
    {
        ContentPathMapping mapping = Map(requestPath, TheTest, readsBucketNames: false);
        return mapping.IsMapped
            ? mapping
            : throw new ArgumentException($"The request path is not one a request may name ({mapping.Refusal}).", nameof(requestPath));
    }

    // Creates each missing folder from the container's root down to directory, publishing each
    // one; false when a file is in the way, or the container's own folder is missing.
    private bool SeedFoldersDownTo(ContentPathMapping mapping, string directory)
    {
        IContentFileSystem fileSystem = FileSystemOf(mapping);
        ContentEntryKind kind = fileSystem.GetEntryKind(directory);
        if (kind != ContentEntryKind.None || IsContainerRoot(mapping, directory))
        {
            return kind == ContentEntryKind.Directory;
        }

        if (!SeedFoldersDownTo(mapping, ParentDirectoryOf(directory)))
        {
            return false;
        }

        fileSystem.CreateDirectory(directory);
        eventsAndFaults.PublishChange(StoreChange.Seeded, PublishedNameOf(mapping, directory), null);
        return true;
    }

    // ADR-0124 decision 3: after a reset every configured share and bucket is there as a folder.
    private void CreateConfiguredFolders()
    {
        foreach (var (_, container) in sharesByName.Values)
        {
            container.FileSystem.CreateDirectory(container.Root);
        }

        foreach (ContentBucket bucket in bucketsByName.Values)
        {
            ContentPathMapping mapping = MapForTheTest(bucket.ContentPath);
            if (!mapping.IsAnsweredAsAbsent && FileSystemOf(mapping).GetEntryKind(mapping.Location!) == ContentEntryKind.None)
            {
                FileSystemOf(mapping).CreateDirectory(mapping.Location!);
            }
        }
    }
}
