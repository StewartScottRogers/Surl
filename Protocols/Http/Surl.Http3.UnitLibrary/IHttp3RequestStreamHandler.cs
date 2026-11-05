namespace Surl.Http3;

/// <summary>
/// Serves the request streams of an HTTP/3 connection (ADR-0086, decision 2): what a server hands
/// <see cref="Http3ConnectionServer"/> to answer each request whose <c>HEADERS</c> arrived whole and
/// well formed.
/// </summary>
public interface IHttp3RequestStreamHandler
{
    /// <summary>
    /// Reads the request on <paramref name="stream"/> and writes its response, ending the stream
    /// with <c>FIN</c> or a reset. The connection calls it once per request stream, on a task of its
    /// own; when it returns with the client's half still open, the connection stops that half with
    /// <c>STOP_SENDING</c> <c>H3_NO_ERROR</c> (RFC 9114 section 4.1).
    /// </summary>
    /// <param name="stream">The request stream.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the stream is served.</returns>
    Task ServeRequestStreamAsync(Http3RequestStream stream, CancellationToken cancellationToken);
}
