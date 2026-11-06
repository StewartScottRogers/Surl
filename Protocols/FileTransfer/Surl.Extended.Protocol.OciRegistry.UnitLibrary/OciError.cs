using Surl.HttpMessage;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// One of the distribution specification's error codes with the fixed message and the status it
/// is sent with (Dist "Error Codes"; ADR-0115, decision 5.1). The message is Dist's description,
/// lower-cased, so no client byte is ever echoed.
/// </summary>
/// <param name="Code">The code, such as <c>MANIFEST_UNKNOWN</c>.</param>
/// <param name="Message">The fixed message.</param>
/// <param name="Status">The status the error is sent with.</param>
internal sealed record OciError(string Code, string Message, HttpStatus Status)
{
    private static readonly HttpStatus TooManyRequestsStatus = new(429, "Too Many Requests");

    private static readonly HttpStatus RangeNotSatisfiableStatus = new(416, "Range Not Satisfiable");

    /// <summary>
    /// code-1: no blob at the digest, its bytes do not match it, or a digest unreadable on a read.
    /// </summary>
    public static OciError BlobUnknown { get; } = new("BLOB_UNKNOWN", "blob unknown to registry", HttpStatus.NotFound);

    /// <summary>
    /// code-2 as <c>400</c>: an upload body that cannot be read to its end as the session's next bytes.
    /// </summary>
    public static OciError BlobUploadInvalid { get; } = new("BLOB_UPLOAD_INVALID", "blob upload invalid", HttpStatus.BadRequest);

    /// <summary>
    /// code-2 as <c>416</c>: a chunk whose <c>Content-Range</c> does not start where the session's bytes end.
    /// </summary>
    public static OciError BlobUploadOutOfOrder { get; } = new("BLOB_UPLOAD_INVALID", "blob upload invalid", RangeNotSatisfiableStatus);

    /// <summary>
    /// code-3: an unknown, expired, finished or another repository's upload session.
    /// </summary>
    public static OciError BlobUploadUnknown { get; } = new("BLOB_UPLOAD_UNKNOWN", "blob upload unknown to registry", HttpStatus.NotFound);

    /// <summary>
    /// code-4: a digest a write cannot read, or one the bytes received do not hash to; or a digest
    /// the referrers endpoint cannot read.
    /// </summary>
    public static OciError DigestInvalid { get; } = new("DIGEST_INVALID", "provided digest did not match uploaded content", HttpStatus.BadRequest);

    /// <summary>
    /// code-5: a manifest naming a blob or manifest the repository lacks.
    /// </summary>
    public static OciError ManifestBlobUnknown { get; } = new("MANIFEST_BLOB_UNKNOWN", "manifest references a manifest or blob unknown to registry", HttpStatus.BadRequest);

    /// <summary>
    /// code-6 as <c>400</c>: a manifest push whose media type, body or tag is invalid.
    /// </summary>
    public static OciError ManifestInvalid { get; } = new("MANIFEST_INVALID", "manifest invalid", HttpStatus.BadRequest);

    /// <summary>
    /// code-6 as <c>413</c>: a manifest past <see cref="OciRepository.MaxManifestBytes"/>.
    /// </summary>
    public static OciError ManifestTooLarge { get; } = new("MANIFEST_INVALID", "manifest invalid", HttpStatus.ContentTooLarge);

    /// <summary>
    /// code-10 as <c>400</c>: a chunk whose length is not its <c>Content-Range</c>'s span.
    /// </summary>
    public static OciError SizeInvalid { get; } = new("SIZE_INVALID", "provided length did not match content length", HttpStatus.BadRequest);

    /// <summary>
    /// code-10 as <c>413</c>: a blob past <c>--max-filesize</c>.
    /// </summary>
    public static OciError SizeTooLarge { get; } = new("SIZE_INVALID", "provided length did not match content length", HttpStatus.ContentTooLarge);

    /// <summary>
    /// code-12: a write while <c>--allow-uploads</c> is off, or one the share forbids.
    /// </summary>
    public static OciError Denied { get; } = new("DENIED", "requested access to the resource is denied", HttpStatus.Forbidden);

    /// <summary>
    /// code-12 for credentials sent to the token endpoint over plain text without
    /// <c>--allow-plaintext-auth</c> (ADR-0115, decision 4.4).
    /// </summary>
    public static OciError PlaintextCredentialsRefused { get; } = new("DENIED", "credentials over plain text are refused", HttpStatus.Forbidden);

    /// <summary>
    /// code-11: no token, an unknown or expired one, or a login the token endpoint refuses
    /// (ADR-0115, decision 4.4).
    /// </summary>
    public static OciError Unauthorized { get; } = new("UNAUTHORIZED", "authentication required", HttpStatus.Unauthorized);

    /// <summary>
    /// code-12 for a repository whose <c>index.json</c> already lists
    /// <see cref="OciIndexFile.MaxDescriptors"/> descriptors (ADR-0115, decision 7).
    /// </summary>
    public static OciError RepositoryFull { get; } = new("DENIED", "repository is full", HttpStatus.Forbidden);

    /// <summary>
    /// code-7: no manifest at the reference, or a reference unreadable on a read.
    /// </summary>
    public static OciError ManifestUnknown { get; } = new("MANIFEST_UNKNOWN", "manifest unknown", HttpStatus.NotFound);

    /// <summary>
    /// code-8: a repository name off the grammar or past 255 bytes.
    /// </summary>
    public static OciError NameInvalid { get; } = new("NAME_INVALID", "invalid repository name", HttpStatus.BadRequest);

    /// <summary>
    /// code-9: no repository of that name, or one the caller may not read.
    /// </summary>
    public static OciError NameUnknown { get; } = new("NAME_UNKNOWN", "repository name not known to registry", HttpStatus.NotFound);

    /// <summary>
    /// code-13 as <c>404</c>: a path no endpoint matches.
    /// </summary>
    public static OciError UnsupportedPath { get; } = new("UNSUPPORTED", "the operation is unsupported", HttpStatus.NotFound);

    /// <summary>
    /// code-13 as <c>405</c>: an endpoint's path with a method it does not list.
    /// </summary>
    public static OciError UnsupportedMethod { get; } = new("UNSUPPORTED", "the operation is unsupported", HttpStatus.MethodNotAllowed);

    /// <summary>
    /// code-13 as <c>400</c>: a tag list's <c>n</c> that is not a count from 0 to 10000.
    /// </summary>
    public static OciError UnsupportedParameter { get; } = new("UNSUPPORTED", "the operation is unsupported", HttpStatus.BadRequest);

    /// <summary>
    /// code-14: a connection past a connection limit, or an upload past the open session bound.
    /// </summary>
    public static OciError TooManyRequests { get; } = new("TOOMANYREQUESTS", "too many requests", TooManyRequestsStatus);
}
