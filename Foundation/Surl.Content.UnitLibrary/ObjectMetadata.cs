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
    /// <see cref="ObjectMetadataIndex.DefaultContentType"/>.
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
}
