namespace Surl.Http3;

/// <summary>
/// What a server sets on its <see cref="Http3ConnectionServer"/> beyond the exchange's limits
/// (ADR-0086, decisions 2 and 3).
/// </summary>
public sealed class Http3ServerOptions
{
    /// <summary>
    /// The <c>server</c> field the answers the connection writes itself carry - the <c>:status 431</c>
    /// for a field section past <c>--max-message</c>, and the <c>:status 501</c> for an extended
    /// <c>CONNECT</c> of an unregistered <c>:protocol</c> - between <c>:status</c> and
    /// <c>content-length</c>; <see langword="null"/> for none.
    /// </summary>
    public string? ServerFieldValue { get; init; }

    /// <summary>
    /// The extended <c>CONNECT</c> handlers, one per <c>:protocol</c>; empty, the default, for none.
    /// With one or more, the server's <c>SETTINGS</c> carry <c>SETTINGS_ENABLE_CONNECT_PROTOCOL</c> = 1
    /// and <c>SETTINGS_H3_DATAGRAM</c> = 1, a request may carry <c>:protocol</c>, and HTTP datagrams
    /// are read from the QUIC connection - which receives them only when the server that composes it
    /// sets <c>QuicServerConnectionOptions.MaxDatagramFrameSize</c> (RFC 9221 section 3).
    /// </summary>
    public IReadOnlyList<IHttp3ExtendedConnectHandler> ExtendedConnectHandlers { get; init; } = [];

    /// <summary>
    /// Further <c>SETTINGS</c> parameters, identifier and value, sent in order after the ones the
    /// connection derives (a WebTransport server's own, ADR-0117); empty, the default, for none.
    /// </summary>
    public IReadOnlyList<KeyValuePair<ulong, ulong>> FurtherSettings { get; init; } = [];

    /// <summary>
    /// How long the connection keeps serving once the exchange is cancelled (ADR-0117 decision 6), or
    /// <see langword="null"/>, the default, for no grace: the open streams are cut off at once and
    /// <c>GOAWAY</c> follows, as the HTTP server has it. With a grace, the connection first marks every
    /// extended <c>CONNECT</c> session shutting down and awaits its
    /// <see cref="Http3ExtendedConnectSession.AddShutdownWrite"/> writes, then sends <c>GOAWAY</c>,
    /// keeps serving until the request streams open at <c>GOAWAY</c> are done or the grace has passed,
    /// and only then cuts the rest off and closes <c>H3_NO_ERROR</c>.
    /// </summary>
    public TimeSpan? ShutdownGrace { get; init; }
}
