namespace Surl.Content;

/// <summary>
/// The object metadata an object-storage server stores with an upload (ADR-0100, decision 2.3):
/// what a file system cannot hold, kept in the <see cref="ObjectMetadataIndex"/> beside the file.
/// </summary>
/// <remarks>
/// A <see langword="null"/> or empty header field is absent. <see cref="ETag"/> left
/// <see langword="null"/> is computed by the index as the quoted lower-case hexadecimal MD5 of
/// the file's bytes; <see cref="Generation"/> left <see langword="null"/> is the file's last
/// write time in microseconds since the Unix epoch.
/// </remarks>
public sealed record ObjectMetadata
{
    /// <summary>
    /// The <c>Content-Type</c> set at upload; absent, the object answers with
    /// the one <see cref="ContentMediaTypes.Of"/> names from its key's extension.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The <c>Content-Encoding</c> set at upload.
    /// </summary>
    public string? ContentEncoding { get; init; }

    /// <summary>
    /// The <c>Content-Disposition</c> set at upload.
    /// </summary>
    public string? ContentDisposition { get; init; }

    /// <summary>
    /// The <c>Cache-Control</c> set at upload.
    /// </summary>
    public string? CacheControl { get; init; }

    /// <summary>
    /// The <c>Content-Language</c> set at upload.
    /// </summary>
    public string? ContentLanguage { get; init; }

    /// <summary>
    /// The <c>Content-MD5</c> set at upload - Azure's <c>x-ms-blob-content-md5</c> - kept as sent,
    /// without being checked against the bytes (ADR-0109, decision 5.6).
    /// </summary>
    public string? ContentMd5 { get; init; }

    /// <summary>
    /// The user metadata set at upload - S3 <c>x-amz-meta-*</c>, Azure <c>x-ms-meta-*</c>, GCS
    /// custom <c>metadata</c> - by name, without the service's prefix.
    /// </summary>
    public IReadOnlyDictionary<string, string> UserMetadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The ETag the server gives the object, quotes included, such as a multipart upload's
    /// <c>"&lt;hex&gt;-&lt;parts&gt;"</c>; <see langword="null"/> for the MD5 of the file's bytes.
    /// At most <see cref="ObjectMetadataIndex.MaxETagBytes"/> bytes of UTF-8.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// The object's GCS <c>generation</c>; <see langword="null"/> for the file's last write time
    /// in microseconds since the Unix epoch.
    /// </summary>
    public long? Generation { get; init; }

    /// <summary>
    /// The object GCS <c>metageneration</c> (ADR-0148, decision 3); <see langword="null"/> for 1.
    /// Must be at least 1.
    /// </summary>
    public long? Metageneration { get; init; }

    /// <summary>
    /// The CRC-32C (<see cref="Crc32cChecksum"/>) of the file's bytes, cached beside the ETag so
    /// Cloud Storage need not read the file to answer <c>x-goog-hash</c>. Used only with a given
    /// <see cref="ETag"/>; without one the index computes both. <see langword="null"/> when not known.
    /// </summary>
    public uint? Crc32c { get; init; }
}
