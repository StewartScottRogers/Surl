using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// Half-closes and resets an accepted TCP <see cref="Socket"/> for a <see cref="StreamConnection"/>
/// (ADR-0004, section 2). Every member is one socket call, exercised by the integration tests
/// (ADR-0004, section 8).
/// </summary>
internal sealed class SocketTransportControl : IConnectionTransportControl
{
    private readonly Socket socket;

    // Keeps the accepted socket; only constructed beside a real socket, in TcpConnectionListener.
    [ExcludeFromCodeCoverage(Justification = "Constructed only over an accepted socket; covered by the integration tests.")]
    public SocketTransportControl(Socket socket) => this.socket = socket;

    // Wraps Socket.Shutdown(SocketShutdown.Send), which sends FIN.
    [ExcludeFromCodeCoverage(Justification = "Calls Socket.Shutdown; covered by the integration tests.")]
    public void ShutdownSend() => socket.Shutdown(SocketShutdown.Send);

    // Wraps Socket.LingerState = (true, 0) and Socket.Close, which reset the connection. A peer
    // that already reset makes some systems (macOS) refuse the linger option; the socket is
    // closed either way, because an abort must not throw.
    [ExcludeFromCodeCoverage(Justification = "Sets Socket.LingerState and calls Socket.Close; covered by the integration tests.")]
    public void ResetAndClose()
    {
        try
        {
            socket.LingerState = new LingerOption(true, 0);
        }
        catch (SocketException)
        {
            // The connection is already gone; closing it is all that is left.
        }
        finally
        {
            socket.Close();
        }
    }
}
