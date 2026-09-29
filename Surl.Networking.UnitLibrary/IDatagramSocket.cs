using System.Net;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// One bound UDP socket, as the datagram demultiplexer and its flows use it: receive a
/// datagram with the endpoint it came from, send one to an endpoint. <see cref="UdpDatagramSocket"/>
/// is the production one; the fast tests fake it, so everything above it runs without a
/// socket (ADR-0004, section 8).
/// </summary>
internal interface IDatagramSocket : IDisposable
{
    /// <summary>
    /// The address and port the socket is bound to.
    /// </summary>
    EndPoint LocalEndPoint { get; }

    /// <summary>
    /// Waits for the next datagram sent to <see cref="LocalEndPoint"/>.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The whole datagram and the endpoint that sent it.</returns>
    /// <exception cref="SocketException">The receive failed.</exception>
    ValueTask<ReceivedDatagram> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends one datagram from <see cref="LocalEndPoint"/> to <paramref name="remoteEndPoint"/>.
    /// </summary>
    /// <param name="datagram">The whole datagram.</param>
    /// <param name="remoteEndPoint">Where it goes.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram has been sent.</returns>
    /// <exception cref="SocketException">The send failed.</exception>
    ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint remoteEndPoint, CancellationToken cancellationToken);
}
