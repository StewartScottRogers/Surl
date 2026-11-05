namespace Surl.Protocol.Http;

/// <summary>
/// Why an HTTP/2 connection ends with <c>GOAWAY</c> (RFC 9113 section 5.4.1): the error code
/// sent and the reason the exchange log is given.
/// </summary>
/// <param name="Code">The <c>GOAWAY</c> error code.</param>
/// <param name="Reason">What the client did, for the log; never sent.</param>
internal sealed record Http2ConnectionError(Http2ErrorCode Code, string Reason);
