namespace Surl.Protocol.Abstractions;

/// <summary>
/// Opens a proxy server's connection to an origin, named by host name or address and port,
/// without the proxy constructing a socket (ADR-0088, decision 2). The HTTP proxy and the SOCKS
/// server share it. Only <c>Surl.Networking</c> implements it over sockets, and it alone decides
/// which addresses may be reached, by the <see cref="AllowedOriginRule"/> the composition root
/// hands it; proxy tests use <see cref="InMemoryOriginConnector"/>.
/// </summary>
public interface IOriginConnector
{
    /// <summary>
    /// Connects to <paramref name="host"/> on <paramref name="port"/>.
    /// </summary>
    /// <param name="host">A DNS name, an IPv4 literal, or an IPv6 literal without brackets.</param>
    /// <param name="port">The origin's port, 1 to 65535.</param>
    /// <param name="timeout">How long resolving the name and opening the connection may take together.</param>
    /// <param name="cancellationToken">The exchange's cancellation token.</param>
    /// <returns>The open connection to the origin.</returns>
    /// <exception cref="OriginConnectionException">
    /// <see cref="OriginConnectionFailure.HostNotFound"/>, <see cref="OriginConnectionFailure.NotAllowed"/>,
    /// <see cref="OriginConnectionFailure.Refused"/>, <see cref="OriginConnectionFailure.Unreachable"/> or
    /// <see cref="OriginConnectionFailure.TimedOut"/>.
    /// </exception>
    ValueTask<IConnection> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken);
}
