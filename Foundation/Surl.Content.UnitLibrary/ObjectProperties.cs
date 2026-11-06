namespace Surl.Content;

/// <summary>
/// What an object-storage server answers about one file in the content store (ADR-0100,
/// decision 2.3): the metadata stored with its upload, or the defaults for a file with none.
/// </summary>
/// <param name="ContentType">The <c>Content-Type</c> set at upload, else the one
/// <see cref="ContentMediaTypes.Of"/> names from the key's extension.</param>
/// <param name="ContentEncoding">The <c>Content-Encoding</c> set at upload, or <see langword="null"/>.</param>
/// <param name="ContentDisposition">The <c>Content-Disposition</c> set at upload, or <see langword="null"/>.</param>
/// <param name="CacheControl">The <c>Cache-Control</c> set at upload, or <see langword="null"/>.</param>
/// <param name="ContentLanguage">The <c>Content-Language</c> set at upload, or <see langword="null"/>.</param>
/// <param name="UserMetadata">The user metadata set at upload, in ordinal order of name; empty
/// for a file with none.</param>
/// <param name="ETag">The ETag, quotes included: the one set at upload, else the quoted
/// lower-case hexadecimal MD5 of the file's bytes.</param>
/// <param name="Generation">The GCS <c>generation</c>: the one set at upload, else the file's last
/// write time in microseconds since the Unix epoch.</param>
/// <param name="Length">The file's length in bytes.</param>
/// <param name="LastWriteTimeUtc">When the file was last written, in UTC.</param>
public sealed record ObjectProperties(
    string ContentType,
    string? ContentEncoding,
    string? ContentDisposition,
    string? CacheControl,
    string? ContentLanguage,
    IReadOnlyList<KeyValuePair<string, string>> UserMetadata,
    string ETag,
    long Generation,
    long Length,
    DateTimeOffset LastWriteTimeUtc)
{
    /// <summary>
    /// The <c>Content-MD5</c> set at upload (<see cref="ObjectMetadata.ContentMd5"/>), or
    /// <see langword="null"/> when none was.
    /// </summary>
    public string? ContentMd5 { get; init; }
}
