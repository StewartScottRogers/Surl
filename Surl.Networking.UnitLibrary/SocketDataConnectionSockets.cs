using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// Binds, accepts and connects the TCP sockets of FTP data connections for
/// <see cref="SocketDataConnectionOpener"/>. Every member is a socket call, exercised by the
/// integration tests (ADR-0004, section 8).
/// </summary>
internal sealed class SocketDataConnectionSockets : IDataConnectionSockets
{
    // Creates a TCP socket, makes an IPv6 one IPv6-only as TcpConnectionListener does, then
    // calls Socket.Bind on port 0 and Socket.Listen.
    [ExcludeFromCodeCoverage(Justification = "Binds a socket; covered by the integration tests.")]
    public IPassiveDataSocket Listen(IPAddress address)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                socket.DualMode = false;
            }

            socket.Bind(new IPEndPoint(address, 0));
            socket.Listen();

            return new ListeningDataSocket(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Calls Socket.ConnectAsync, then turns Nagle's algorithm off and reads the end points.
    [ExcludeFromCodeCoverage(Justification = "Connects a socket; covered by the integration tests.")]
    public async Task<DataTransport> ConnectAsync(IPEndPoint target, CancellationToken cancellationToken)
    {
        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            await socket.ConnectAsync(target, cancellationToken);
            socket.NoDelay = true;

            return TransportOf(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Wraps a connected socket in a NetworkStream that owns it.
    [ExcludeFromCodeCoverage(Justification = "Wraps a connected socket; covered by the integration tests.")]
    private static DataTransport TransportOf(Socket socket) =>
        new(
            new NetworkStream(socket, ownsSocket: true),
            socket.LocalEndPoint!,
            socket.RemoteEndPoint!,
            new SocketTransportControl(socket));

    // One listening socket; every member is one socket call.
    [ExcludeFromCodeCoverage(Justification = "Wraps a listening socket; covered by the integration tests.")]
    private sealed class ListeningDataSocket(Socket socket) : IPassiveDataSocket
    {
        public IPEndPoint LocalEndPoint { get; } = (IPEndPoint)socket.LocalEndPoint!;

        // Calls Socket.AcceptAsync, then sets NoDelay and reads the end points; a client that
        // reset before those calls is an AcceptedSocketLostException (ADR-0022).
        public async Task<DataTransport> AcceptAsync(CancellationToken cancellationToken)
        {
            var accepted = await socket.AcceptAsync(cancellationToken);

            try
            {
                accepted.NoDelay = true;

                return TransportOf(accepted);
            }
            catch (SocketException exception)
            {
                accepted.Dispose();
                throw new AcceptedSocketLostException(exception);
            }
        }

        public void Close() => socket.Dispose();
    }
}
