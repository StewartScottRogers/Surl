namespace Surl.Content;

/// <summary>
/// The kind, a file's length and the last write time of a file or a directory in the served
/// root, as <see cref="ContentStore.GetEntryStatus(ContentPathMapping)"/> reports them.
/// </summary>
/// <param name="Kind"><see cref="ContentEntryKind.File"/> or
/// <see cref="ContentEntryKind.Directory"/>.</param>
/// <param name="Length">A file's length in bytes; <see langword="null"/> for a
/// directory.</param>
/// <param name="LastModifiedUtc">When the entry was last written, in UTC.</param>
public sealed record ContentEntryStatus(ContentEntryKind Kind, long? Length, DateTimeOffset LastModifiedUtc);
