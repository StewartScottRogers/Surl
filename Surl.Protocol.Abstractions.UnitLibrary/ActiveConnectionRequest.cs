using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// One <see cref="IDataConnectionOpener.ConnectActiveAsync"/> call, as
/// <see cref="InMemoryDataConnections"/> records it.
/// </summary>
/// <param name="ControlRemote">The control connection's remote end point the server passed.</param>
/// <param name="Target">The address and port the server was asked to connect to.</param>
/// <param name="Timeout">The timeout the server passed.</param>
public sealed record ActiveConnectionRequest(EndPoint ControlRemote, IPEndPoint Target, TimeSpan Timeout);
