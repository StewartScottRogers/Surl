namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which way an <see cref="ExchangeBytesEvent"/>'s bytes crossed the connection.
/// </summary>
public enum ExchangeDirection
{
    /// <summary>
    /// From the client to the server.
    /// </summary>
    Received,

    /// <summary>
    /// From the server to the client.
    /// </summary>
    Sent,
}
