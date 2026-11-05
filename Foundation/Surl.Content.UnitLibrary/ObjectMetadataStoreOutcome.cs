namespace Surl.Content;

/// <summary>
/// What <see cref="ObjectMetadataIndex.StoreAsync"/> did with an upload's metadata (ADR-0100,
/// decision 2.3). Each object-storage server words a refusal in its own service's terms.
/// </summary>
public enum ObjectMetadataStoreOutcome
{
    /// <summary>
    /// The metadata is stored, stamped with the file's length and last write time.
    /// </summary>
    Stored,

    /// <summary>
    /// Nothing is stored: the metadata is more than
    /// <see cref="ObjectMetadataIndex.MaxMetadataBytes"/> bytes.
    /// </summary>
    MetadataTooLarge,

    /// <summary>
    /// Nothing is stored: the index holds <see cref="ObjectMetadataIndex.MaxEntries"/> entries
    /// for files that still exist unchanged, and this key is not one of them.
    /// </summary>
    MetadataIndexFull,

    /// <summary>
    /// Nothing is stored: no file is at the key.
    /// </summary>
    NoSuchFile,
}
