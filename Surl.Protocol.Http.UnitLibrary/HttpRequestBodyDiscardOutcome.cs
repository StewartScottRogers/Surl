namespace Surl.Protocol.Http;

/// <summary>
/// How reading and discarding a request body ended.
/// </summary>
internal enum HttpRequestBodyDiscardOutcome
{
    /// <summary>
    /// The whole body was read and dropped; the next request follows it.
    /// </summary>
    Discarded,

    /// <summary>
    /// The body went past the upload limit
    /// (<see cref="Surl.Protocol.Abstractions.ExchangeLimits.MaxUploadBytes"/>).
    /// </summary>
    TooLarge,

    /// <summary>
    /// The body is malformed chunked coding, or the client half-closed before its end.
    /// </summary>
    Incomplete,
}
