namespace Surl.Http2;

/// <summary>
/// Serves the request streams of an HTTP/2 connection (ADR-0086, decision 1): what a server
/// hands <see cref="Http2ConnectionServer"/> to answer each request that passed validation.
/// </summary>
public interface IHttp2StreamHandler
{
    /// <summary>
    /// Reads the request on <paramref name="stream"/> and writes its response, ending the stream
    /// with <c>END_STREAM</c> or a reset. The connection calls it once per request stream, on a
    /// task of its own, concurrently up to <see cref="Http2ConnectionServer.MaxConcurrentStreams"/>.
    /// </summary>
    /// <param name="stream">The request stream.</param>
    /// <param name="cancellationToken">Cancelled when the client resets the stream or the connection ends.</param>
    /// <returns>A task that completes when the stream is served.</returns>
    Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken);
}
