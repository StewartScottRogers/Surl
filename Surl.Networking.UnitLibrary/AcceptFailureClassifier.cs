using System.Collections.Frozen;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// Tells a failed accept that belongs to one client - the client reset or aborted its
/// connection before the server took it - from a failure of the listening socket itself
/// (ADR-0022). A per-connection failure is absorbed and the listener accepts again; a
/// listener-fatal one ends <see cref="TcpConnectionListener.AcceptAsync"/>.
/// </summary>
internal static class AcceptFailureClassifier
{
    // ConnectionReset: Windows' AcceptEx and accept, "an incoming connection was indicated,
    // but was subsequently terminated by the remote peer". ConnectionAborted: ECONNABORTED
    // on Linux and macOS, the same event. HostDown, HostUnreachable and NetworkUnreachable:
    // the pending network errors of the new connection Linux's accept(2) passes on and says
    // to treat like EAGAIN, by retrying.
    private static readonly FrozenSet<SocketError> PerConnectionErrors = new[]
    {
        SocketError.ConnectionReset,
        SocketError.ConnectionAborted,
        SocketError.HostDown,
        SocketError.HostUnreachable,
        SocketError.NetworkUnreachable,
    }.ToFrozenSet();

    /// <summary>
    /// Says whether an accept that failed with <paramref name="socketError"/> failed for one
    /// client only.
    /// </summary>
    /// <param name="socketError">The error the accept reported.</param>
    /// <returns><see langword="true"/> for a per-connection failure; <see langword="false"/> for a listener-fatal one.</returns>
    public static bool IsPerConnection(SocketError socketError) => PerConnectionErrors.Contains(socketError);

    /// <summary>
    /// Says whether <paramref name="exception"/>, thrown by an accept, is a failure that
    /// belongs to one client: a <see cref="SocketException"/> with a per-connection error, or
    /// an <see cref="AcceptedSocketLostException"/>.
    /// </summary>
    /// <param name="exception">What the accept threw.</param>
    /// <returns><see langword="true"/> when the listener should accept again.</returns>
    public static bool IsPerConnection(Exception exception) => exception switch
    {
        AcceptedSocketLostException => true,
        SocketException socketException => IsPerConnection(socketException.SocketErrorCode),
        _ => false,
    };
}
