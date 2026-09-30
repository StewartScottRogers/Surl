using System.Net;

namespace Surl.Networking;

/// <summary>
/// A connected TCP transport, before it becomes a <see cref="StreamConnection"/>.
/// </summary>
/// <param name="Stream">The transport's byte stream, which owns the socket.</param>
/// <param name="LocalEndPoint">The server's end of the connection.</param>
/// <param name="RemoteEndPoint">The client's end of the connection.</param>
/// <param name="Control">Half-closes and resets the transport.</param>
internal readonly record struct DataTransport(
    Stream Stream, EndPoint LocalEndPoint, EndPoint RemoteEndPoint, IConnectionTransportControl Control)
{
    /// <summary>
    /// Drops the transport at once with a reset and releases it.
    /// </summary>
    public void ResetAndRelease()
    {
        Control.ResetAndClose();
        Stream.Dispose();
    }
}
