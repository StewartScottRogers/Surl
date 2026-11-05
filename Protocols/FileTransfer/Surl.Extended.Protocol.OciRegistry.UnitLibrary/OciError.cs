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

    /// <summary>
    /// code-1: no blob at the digest, its bytes do not match it, or a digest unreadable on a read.
    /// </summary>
    public static OciError BlobUnknown { get; } = new("BLOB_UNKNOWN", "blob unknown to registry", HttpStatus.NotFound);

    /// <summary>
    /// code-4: a digest the referrers endpoint cannot read.
    /// </summary>
    public static OciError DigestInvalid { get; } = new("DIGEST_INVALID", "provided digest did not match uploaded content", HttpStatus.BadRequest);

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
    /// code-14: a connection past a connection limit.
    /// </summary>
    public static OciError TooManyRequests { get; } = new("TOOMANYREQUESTS", "too many requests", TooManyRequestsStatus);
}
