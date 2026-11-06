namespace Surl.Content;

/// <summary>
/// A copy of every entry an <see cref="ObjectMetadataIndex"/> held at one moment, for a
/// <see cref="ContentStoreSnapshot"/> (ADR-0124 decision 3). Entries are immutable, so the copy
/// shares them.
/// </summary>
/// <param name="Entries">Every entry, by key.</param>
internal sealed record ObjectMetadataIndexContents(IReadOnlyDictionary<string, ObjectMetadataEntry> Entries);
