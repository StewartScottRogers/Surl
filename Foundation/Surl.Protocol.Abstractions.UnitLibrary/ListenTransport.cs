namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which of its scheme's listeners a listen URL binds when the scheme has both a connection
/// server and a datagram server (<see cref="ListenUrl.OnlyTransport"/>; ADR-0092, Amendment 1).
/// </summary>
public enum ListenTransport
{
    /// <summary>
    /// Only the connection listener, served by the scheme's <see cref="IConnectionProtocolServer"/>.
    /// </summary>
    Connections = 1,

    /// <summary>
    /// Only the datagram listener, served by the scheme's <see cref="IDatagramProtocolServer"/>.
    /// </summary>
    Datagrams = 2,
}
