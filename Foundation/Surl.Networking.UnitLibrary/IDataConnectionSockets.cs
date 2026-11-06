using System.Net;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// The socket calls behind <see cref="SocketDataConnectionOpener"/>: binding a passive listening
/// socket and connecting an active one. <see cref="SocketDataConnectionSockets"/> makes them over
/// real sockets; the fast tests give the opener a fake, so its rules run with no network.
/// </summary>
internal interface IDataConnectionSockets
{
    /// <summary>
    /// Binds a TCP socket on <paramref name="address"/>, on a port the operating system picks,
    /// and starts listening on it.
    /// </summary>
    /// <param name="address">The address to bind, already normalized.</param>
    /// <returns>The listening socket.</returns>
    /// <exception cref="SocketException">The address could not be bound.</exception>
    IPassiveDataSocket Listen(IPAddress address);

    /// <summary>
    /// Connects a TCP socket to <paramref name="target"/>.
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cuts the connect off.</param>
    /// <returns>The connected transport.</returns>
    /// <exception cref="SocketException">The target could not be reached.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the connect off.</exception>
    Task<DataTransport> ConnectAsync(IPEndPoint target, CancellationToken cancellationToken);
}
