using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IDatagramSocket"/> over a bound UDP <see cref="Socket"/>. Every member is one
/// socket call, exercised by the integration tests (ADR-0004, section 8).
/// </summary>
/// <remarks>Meant for one receive at a time: every receive fills the same buffer.</remarks>
internal sealed class UdpDatagramSocket : IDatagramSocket
{
    // The largest datagram UDP's 16-bit length field can describe.
    private const int LargestDatagram = 65_535;

    private readonly Socket socket;
    private readonly EndPoint anyRemoteEndPoint;
    private readonly byte[] receiveBuffer = new byte[LargestDatagram];

    // Keeps the bound socket and reads Socket.LocalEndPoint.
    [ExcludeFromCodeCoverage(Justification = "Reads Socket.LocalEndPoint; covered by the integration tests.")]
    private UdpDatagramSocket(Socket socket, EndPoint anyRemoteEndPoint)
    {
        this.socket = socket;
        this.anyRemoteEndPoint = anyRemoteEndPoint;
        LocalEndPoint = socket.LocalEndPoint!;
    }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint
    {
        // Returns the value the socket-reading constructor set.
        [ExcludeFromCodeCoverage(Justification = "Set only by the socket-reading constructor; covered by the integration tests.")]
        get;
    }

    /// <summary>
    /// Creates a UDP socket and binds it to <paramref name="endPoint"/>. An IPv6 socket is made
    /// IPv6-only first, so <c>[::]</c> binds only IPv6 on every platform, as the TCP listener does.
    /// </summary>
    /// <param name="endPoint">The address and port to bind; port 0 asks for an ephemeral port.</param>
    /// <returns>The bound socket.</returns>
    /// <exception cref="SocketException">The bind failed; nothing stays bound.</exception>
    // Creates a Socket and calls Socket.Bind on it.
    [ExcludeFromCodeCoverage(Justification = "Binds a socket; covered by the integration tests.")]
    public static UdpDatagramSocket Bind(IPEndPoint endPoint)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            var isIPv6 = endPoint.AddressFamily == AddressFamily.InterNetworkV6;
            if (isIPv6)
            {
                socket.DualMode = false;
            }

            socket.Bind(endPoint);

            return new UdpDatagramSocket(socket, new IPEndPoint(isIPv6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    // Calls Socket.ReceiveFromAsync and copies the datagram out of the shared buffer.
    [ExcludeFromCodeCoverage(Justification = "Calls Socket.ReceiveFromAsync; covered by the integration tests.")]
    public async ValueTask<ReceivedDatagram> ReceiveAsync(CancellationToken cancellationToken)
    {
        var received = await socket.ReceiveFromAsync(receiveBuffer, SocketFlags.None, anyRemoteEndPoint, cancellationToken);

        return new ReceivedDatagram(receiveBuffer.AsSpan(0, received.ReceivedBytes).ToArray(), received.RemoteEndPoint);
    }

    /// <inheritdoc/>
    // Calls Socket.SendToAsync.
    [ExcludeFromCodeCoverage(Justification = "Calls Socket.SendToAsync; covered by the integration tests.")]
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint remoteEndPoint, CancellationToken cancellationToken) =>
        await socket.SendToAsync(datagram, SocketFlags.None, remoteEndPoint, cancellationToken);

    /// <inheritdoc/>
    // Calls Socket.Dispose.
    [ExcludeFromCodeCoverage(Justification = "Disposes a socket; covered by the integration tests.")]
    public void Dispose() => socket.Dispose();
}
