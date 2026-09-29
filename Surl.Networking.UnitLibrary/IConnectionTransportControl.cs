namespace Surl.Networking;

/// <summary>
/// The two things a <see cref="StreamConnection"/> does to its transport that a
/// <see cref="Stream"/> cannot: half-close and reset (ADR-0004, section 2).
/// </summary>
internal interface IConnectionTransportControl
{
    /// <summary>
    /// Sends FIN, leaving the connection readable.
    /// </summary>
    /// <exception cref="System.Net.Sockets.SocketException">The transport failed.</exception>
    void ShutdownSend();

    /// <summary>
    /// Drops the connection at once with a reset, discarding unsent bytes.
    /// </summary>
    void ResetAndClose();
}
