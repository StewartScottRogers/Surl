namespace Surl.Protocol.Abstractions;

/// <summary>
/// Why an <see cref="IHttpAuthenticationSession"/> refused a request, for a server that words
/// each refusal differently: the S3 server answers each with its own S3 error code (ADR-0108,
/// decisions 4.3 and 4.4). The HTTP server ignores it, so its bytes do not change.
/// </summary>
public enum HttpAuthenticationRefusal
{
    /// <summary>
    /// No <c>Authorization</c> field, and anonymous requests are not allowed.
    /// </summary>
    MissingCredentials,

    /// <summary>
    /// An <c>Authorization</c> field whose scheme is not the one the request needs.
    /// </summary>
    UnsupportedScheme,

    /// <summary>
    /// Credentials that do not parse: a scope for another service, a request date that is not
    /// signed, unreadable or not the scope's date.
    /// </summary>
    MalformedCredentials,

    /// <summary>
    /// A signed request without the payload hash field (<c>x-amz-content-sha256</c>) the
    /// request needs.
    /// </summary>
    MissingPayloadHash,

    /// <summary>
    /// Credentials naming no account.
    /// </summary>
    UnknownAccount,

    /// <summary>
    /// A signature that does not verify with the account's secret.
    /// </summary>
    BadSignature,

    /// <summary>
    /// A request date further from the server's clock than the method allows.
    /// </summary>
    ClockSkew,

    /// <summary>
    /// A body whose SHA-256 is not the payload hash the request signed, judged by an
    /// <see cref="IHttpRequestBodyCheck"/> once the body is read.
    /// </summary>
    PayloadHashMismatch,
}
