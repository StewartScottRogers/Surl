using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// Thrown when a socket was accepted but its client was gone before it could be made into
/// a connection (setting its options or reading its endpoints failed). The socket has
/// already been released; the failure belongs to that client, so the listener accepts
/// again (ADR-0022).
/// </summary>
internal sealed class AcceptedSocketLostException : Exception
{
    /// <summary>
    /// Creates the exception for the socket call that failed.
    /// </summary>
    /// <param name="innerException">The failure of the socket call on the accepted socket.</param>
    public AcceptedSocketLostException(SocketException innerException)
        : base(innerException.Message, innerException)
    {
    }
}
