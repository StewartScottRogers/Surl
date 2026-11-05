namespace Surl.Content;

/// <summary>
/// An opaque, in-memory copy of a <see cref="ContentStore"/>'s files and folders - its root's and
/// every share's - and, when one was given, its <see cref="ObjectMetadataIndex"/>'s entries, taken
/// by <see cref="ContentStore.TakeSnapshotAsync(ObjectMetadataIndex?, CancellationToken)"/> and put
/// back by <see cref="ContentStore.RestoreAsync(ContentStoreSnapshot, CancellationToken)"/>
/// (ADR-0124 decision 3).
/// </summary>
/// <remarks>
/// A snapshot carries the store that took it, and only that store restores it. Files share their
/// bytes with the store until either side writes them again, so taking one costs no copy of the
/// bytes; <see cref="ByteCount"/> is what the snapshot keeps alive, for a host that bounds the
/// snapshots of all its stores together (<c>MaxSnapshotBytes</c>).
/// </remarks>
public sealed class ContentStoreSnapshot
{
    internal ContentStoreSnapshot(
        ContentStore store,
        IReadOnlyList<(InMemoryContentFileSystem FileSystem, InMemoryContentFileSystemContents Contents)> fileSystems,
        ObjectMetadataIndex? metadataIndex,
        ObjectMetadataIndexContents? metadataContents)
    {
        Store = store;
        FileSystems = fileSystems;
        MetadataIndex = metadataIndex;
        MetadataContents = metadataContents;
    }

    /// <summary>The bytes of every file the snapshot holds.</summary>
    public long ByteCount => FileSystems.Sum(saved => saved.Contents.ByteCount);

    /// <summary>The store that took the snapshot, the only one that restores it.</summary>
    internal ContentStore Store { get; }

    /// <summary>Each distinct in-memory file system of the store, with its copy.</summary>
    internal IReadOnlyList<(InMemoryContentFileSystem FileSystem, InMemoryContentFileSystemContents Contents)> FileSystems { get; }

    /// <summary>The index whose entries were copied, or null when none was given.</summary>
    internal ObjectMetadataIndex? MetadataIndex { get; }

    /// <summary>The index's entries, or null when no index was given.</summary>
    internal ObjectMetadataIndexContents? MetadataContents { get; }
}
