using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// Receives from an <see cref="IDatagramSocket"/> past the connection resets UDP reports.
/// </summary>
internal static class DatagramReceiving
{
    /// <summary>
    /// Waits for the next datagram on <paramref name="socket"/>, skipping every
    /// <see cref="SocketError.ConnectionReset"/>: Windows reports an ICMP port-unreachable, left
    /// by an earlier send to a client that has gone, as a failed receive, and it says nothing
    /// about the next datagram.
    /// </summary>
    /// <param name="socket">The socket to receive from.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The next datagram and the endpoint that sent it.</returns>
    /// <exception cref="SocketException">The receive failed for any other reason.</exception>
    public static async ValueTask<ReceivedDatagram> ReceiveIgnoringConnectionResetsAsync(
        IDatagramSocket socket, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return await socket.ReceiveAsync(cancellationToken);
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionReset)
            {
                // A reset left by an earlier send; the next datagram is still to come.
            }
        }
    }
}
