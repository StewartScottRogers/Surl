using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// One <see cref="IDataConnectionOpener.StartPassiveListenerAsync"/> call, as
/// <see cref="InMemoryDataConnections"/> records it.
/// </summary>
/// <param name="ControlLocal">The control connection's local end point the server passed.</param>
/// <param name="ControlRemote">The control connection's remote end point the server passed.</param>
public sealed record PassiveListenerRequest(EndPoint ControlLocal, EndPoint ControlRemote);
