namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which connection limit a connection or flow is past, handed to
/// <see cref="IConnectionRefusalWriter"/> and <see cref="IDatagramRefusalWriter"/>
/// (ADR-0006, section 6).
/// </summary>
public enum ConnectionRefusal
{
    /// <summary>
    /// The listener already holds as many connections as it is allowed.
    /// </summary>
    TooManyConnections,

    /// <summary>
    /// The remote address already holds as many connections as one address is allowed.
    /// </summary>
    TooManyConnectionsFromAddress,
}
