namespace Surl.Protocol.Abstractions;

/// <summary>
/// How a <see cref="FaultKind.Drop"/> rule ends the connection (ADR-0188, decision 1).
/// </summary>
public enum FaultClose
{
    /// <summary>
    /// Abort the connection, so the peer sees a reset. The default.
    /// </summary>
    Reset = 0,

    /// <summary>
    /// Close the connection gracefully, so the peer sees an ordinary end of stream.
    /// </summary>
    Graceful,
}
