namespace Surl.Content;

/// <summary>
/// One entry of the <see cref="ObjectMetadataIndex"/>: a file's object metadata, stamped with the
/// length and last write time the file had when the entry was written.
/// </summary>
/// <param name="Length">The file's length when the entry was written.</param>
/// <param name="LastWriteTicks">The file's last write time, in UTC ticks, when the entry was written.</param>
/// <param name="Generation">The GCS <c>generation</c>.</param>
/// <param name="ETag">The ETag, quotes included.</param>
/// <param name="ContentType">The <c>Content-Type</c> set at upload, or <see langword="null"/>.</param>
/// <param name="ContentEncoding">The <c>Content-Encoding</c> set at upload, or <see langword="null"/>.</param>
/// <param name="ContentDisposition">The <c>Content-Disposition</c> set at upload, or <see langword="null"/>.</param>
/// <param name="CacheControl">The <c>Cache-Control</c> set at upload, or <see langword="null"/>.</param>
/// <param name="ContentLanguage">The <c>Content-Language</c> set at upload, or <see langword="null"/>.</param>
/// <param name="ContentMd5">The <c>Content-MD5</c> set at upload, or <see langword="null"/>.</param>
/// <param name="UserMetadata">The user metadata, in ordinal order of name.</param>
internal sealed record ObjectMetadataEntry(
    long Length,
    long LastWriteTicks,
    long Generation,
    string ETag,
    string? ContentType,
    string? ContentEncoding,
    string? ContentDisposition,
    string? CacheControl,
    string? ContentLanguage,
    string? ContentMd5,
    IReadOnlyList<KeyValuePair<string, string>> UserMetadata)
{
    /// <summary>
    /// The six header fields, in the order the index writes them.
    /// </summary>
    public IEnumerable<string?> HeaderFields => [ContentType, ContentEncoding, ContentDisposition, CacheControl, ContentLanguage, ContentMd5];

    /// <summary>
    /// Whether the entry still describes a file of <paramref name="length"/> bytes last written
    /// at <paramref name="lastWriteTicks"/>.
    /// </summary>
    public bool Describes(long length, long lastWriteTicks) => Length == length && LastWriteTicks == lastWriteTicks;

    /// <summary>
    /// The UTF-8 bytes of the header fields and the user metadata's names and values, counted
    /// against <see cref="ObjectMetadataIndex.MaxMetadataBytes"/>.
    /// </summary>
    public static int CountMetadataBytes(IEnumerable<string?> headerFields, IEnumerable<KeyValuePair<string, string>> userMetadata) =>
        headerFields.Sum(field => System.Text.Encoding.UTF8.GetByteCount(field ?? string.Empty))
        + userMetadata.Sum(pair => System.Text.Encoding.UTF8.GetByteCount(pair.Key) + System.Text.Encoding.UTF8.GetByteCount(pair.Value));
}
