using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// The default <see cref="ExchangeContext.DataConnections"/>: every request throws
/// <see cref="DataConnectionException"/> with <see cref="DataConnectionFailure.Unavailable"/>
/// (ADR-0052, decision 9), so an FTP server given no opener answers <c>425</c>.
/// </summary>
public sealed class RefusingDataConnectionOpener : IDataConnectionOpener
{
    private RefusingDataConnectionOpener()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static RefusingDataConnectionOpener Instance { get; } = new();

    /// <inheritdoc/>
    /// <remarks>Always throws <see cref="DataConnectionFailure.Unavailable"/>.</remarks>
    public ValueTask<IPassiveDataListener> StartPassiveListenerAsync(
        EndPoint controlLocal, EndPoint controlRemote, CancellationToken cancellationToken) =>
        throw new DataConnectionException(
            DataConnectionFailure.Unavailable, "This exchange has no data connections to open.");

    /// <inheritdoc/>
    /// <remarks>Always throws <see cref="DataConnectionFailure.Unavailable"/>.</remarks>
    public ValueTask<IConnection> ConnectActiveAsync(
        EndPoint controlRemote, IPEndPoint target, TimeSpan timeout, CancellationToken cancellationToken) =>
        throw new DataConnectionException(
            DataConnectionFailure.Unavailable, "This exchange has no data connections to open.");
}
