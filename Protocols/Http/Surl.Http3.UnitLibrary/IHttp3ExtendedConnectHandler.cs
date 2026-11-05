namespace Surl.Http3;

/// <summary>
/// Serves the extended <c>CONNECT</c> requests (RFC 9220) of one <c>:protocol</c>, and the streams
/// that name its sessions (ADR-0086 decision 3): what a server adds to
/// <see cref="Http3ServerOptions.ExtendedConnectHandlers"/>. With one registered, the connection's
/// <c>SETTINGS</c> carry <c>SETTINGS_ENABLE_CONNECT_PROTOCOL</c> and <c>SETTINGS_H3_DATAGRAM</c>.
/// </summary>
public interface IHttp3ExtendedConnectHandler
{
    /// <summary>The <c>:protocol</c> value this handler serves, e.g. <c>webtransport-h3</c>, compared byte for byte.</summary>
    string Protocol { get; }

    /// <summary>
    /// The value that opens a client bidirectional stream belonging to one of this handler's sessions
    /// (WebTransport's <c>0x41</c>), followed by the session ID; <see langword="null"/> for none.
    /// </summary>
    ulong? BidirectionalStreamSignal { get; }

    /// <summary>
    /// The unidirectional stream type of a stream belonging to one of this handler's sessions
    /// (WebTransport's <c>0x54</c>), followed by the session ID; <see langword="null"/> for none.
    /// </summary>
    ulong? UnidirectionalStreamType { get; }

    /// <summary>
    /// Serves one session: answers its extended <c>CONNECT</c> (a 2xx accepts it), then exchanges
    /// capsules, HTTP datagrams and streams until it ends. The connection calls it once per extended
    /// <c>CONNECT</c> whose <c>:protocol</c> is <see cref="Protocol"/>, on the request stream's task.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the session is over.</returns>
    Task ServeSessionAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Serves a stream that opened with <see cref="BidirectionalStreamSignal"/> or is of
    /// <see cref="UnidirectionalStreamType"/> but names no session of this handler that is open now:
    /// one whose <c>CONNECT</c> has not arrived yet, has ended, or was never this handler's. The
    /// handler holds it, resets it or drops it, as its protocol says.
    /// </summary>
    /// <param name="stream">The stream, read past its signal or type and its session ID.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the stream is dealt with.</returns>
    Task ServeStreamWithoutSessionAsync(Http3SessionStream stream, CancellationToken cancellationToken);
}
