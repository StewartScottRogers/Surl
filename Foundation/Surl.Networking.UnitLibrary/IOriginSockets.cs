using System.Net;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// The name resolution and socket calls behind <see cref="SocketOriginConnector"/>.
/// <see cref="SocketOriginSockets"/> makes them with <see cref="Dns"/> and real sockets; the fast
/// tests also give the connector a fake, so its timeout and ordering rules run with no network.
/// </summary>
internal interface IOriginSockets
{
    /// <summary>
    /// Resolves <paramref name="host"/> to its addresses; an IP literal resolves to itself.
    /// </summary>
    /// <param name="host">A DNS name or an IP literal without brackets.</param>
    /// <param name="cancellationToken">Cuts the resolution off.</param>
    /// <returns>The addresses, in the resolver's order.</returns>
    /// <exception cref="SocketException">The name did not resolve.</exception>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);

    /// <summary>
    /// Connects a TCP socket to <paramref name="target"/>.
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cuts the connect off.</param>
    /// <returns>The connected transport.</returns>
    /// <exception cref="SocketException">The target could not be reached.</exception>
    Task<DataTransport> ConnectAsync(IPEndPoint target, CancellationToken cancellationToken);
}
