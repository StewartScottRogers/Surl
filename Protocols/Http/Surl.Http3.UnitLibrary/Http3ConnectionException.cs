namespace Surl.Http3;

/// <summary>
/// A connection error (RFC 9114 section 8): the client broke a rule that ends the whole HTTP/3
/// connection, which <see cref="Http3ConnectionServer"/> closes with <see cref="Code"/>.
/// </summary>
/// <param name="code">The error code <c>CONNECTION_CLOSE</c> carries.</param>
/// <param name="reason">What the client did, for the exchange log.</param>
internal sealed class Http3ConnectionException(Http3ErrorCode code, string reason) : Exception(reason)
{
    /// <summary>The error code <c>CONNECTION_CLOSE</c> carries.</summary>
    public Http3ErrorCode Code { get; } = code;
}
