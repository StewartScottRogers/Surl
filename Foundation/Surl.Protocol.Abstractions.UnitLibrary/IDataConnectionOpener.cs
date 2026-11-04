using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Opens an exchange's FTP data connections (ADR-0052, decision 9). Only <c>Surl.Networking</c>
/// implements it over sockets; <c>Surl.Core</c> wraps it per exchange (logs, idle clock);
/// protocol tests use <see cref="InMemoryDataConnections"/>. A server reaches it through
/// <see cref="ExchangeContext.DataConnections"/>.
/// </summary>
public interface IDataConnectionOpener
{
    /// <summary>
    /// Binds a listener on <paramref name="controlLocal"/>'s address, on an ephemeral port, that
    /// accepts connections only from <paramref name="controlRemote"/>'s address (an IPv4-mapped
    /// address compared as IPv4). Serves <c>EPSV</c> and <c>PASV</c>.
    /// </summary>
    /// <param name="controlLocal">The control connection's local end point, the address curl reached.</param>
    /// <param name="controlRemote">The control connection's remote end point, curl's address.</param>
    /// <param name="cancellationToken">The exchange's cancellation token.</param>
    /// <returns>The listener, whose <see cref="IPassiveDataListener.LocalEndPoint"/> the server announces.</returns>
    /// <exception cref="DataConnectionException">No listener could be started.</exception>
    ValueTask<IPassiveDataListener> StartPassiveListenerAsync(
        EndPoint controlLocal, EndPoint controlRemote, CancellationToken cancellationToken);

    /// <summary>
    /// Connects to <paramref name="target"/>, which must have <paramref name="controlRemote"/>'s
    /// address and a port of 1024 or more. Serves <c>EPRT</c> and <c>PORT</c>.
    /// </summary>
    /// <param name="controlRemote">The control connection's remote end point, curl's address.</param>
    /// <param name="target">The address and port curl named.</param>
    /// <param name="timeout">How long the connection may take to open.</param>
    /// <param name="cancellationToken">The exchange's cancellation token.</param>
    /// <returns>The open data connection.</returns>
    /// <exception cref="DataConnectionException">
    /// <see cref="DataConnectionFailure.Refused"/> for another address,
    /// <see cref="DataConnectionFailure.Unreachable"/> or <see cref="DataConnectionFailure.TimedOut"/>.
    /// </exception>
    ValueTask<IConnection> ConnectActiveAsync(
        EndPoint controlRemote, IPEndPoint target, TimeSpan timeout, CancellationToken cancellationToken);
}
