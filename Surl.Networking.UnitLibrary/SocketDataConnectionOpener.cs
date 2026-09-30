using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Opens FTP data connections over TCP sockets (ADR-0052, decisions 5, 6 and 9): a passive
/// listener on the control connection's local address that takes a connection only from the
/// control connection's peer, and an active connection only to the peer's own address on a
/// port of 1024 or more. Every data connection is a <see cref="StreamConnection"/> - lingering
/// close (ADR-0021) included - whose <see cref="IConnection.UpgradeToTlsAsync"/> uses the
/// server's <see cref="ServerTlsSettings"/> with no ALPN, as the FTP control connection does.
/// </summary>
/// <remarks>
/// The rules live here and in <see cref="SocketPassiveDataListener"/> and
/// <see cref="DataConnectionPeer"/>, fast-tested over a fake <see cref="IDataConnectionSockets"/>;
/// only <see cref="SocketDataConnectionSockets"/> touches a socket.
/// </remarks>
public sealed class SocketDataConnectionOpener : IDataConnectionOpener
{
    private readonly ServerTlsHandshake? tlsHandshake;
    private readonly TimeProvider timeProvider;
    private readonly IDataConnectionSockets sockets;

    /// <summary>
    /// Creates the opener.
    /// </summary>
    /// <param name="tlsSettings">
    /// The process's TLS settings, the ones the control listener uses, or <see langword="null"/>
    /// for data connections that cannot be secured.
    /// </param>
    /// <param name="timeProvider">Times the timeouts and the lingering close; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public SocketDataConnectionOpener(ServerTlsSettings? tlsSettings, TimeProvider? timeProvider = null)
        : this(tlsSettings, timeProvider ?? TimeProvider.System, new SocketDataConnectionSockets())
    {
    }

    /// <summary>
    /// Creates the opener over the given socket calls.
    /// </summary>
    /// <param name="tlsSettings">The process's TLS settings, or <see langword="null"/>.</param>
    /// <param name="timeProvider">Times the timeouts and the lingering close.</param>
    /// <param name="sockets">Binds, accepts and connects the sockets.</param>
    internal SocketDataConnectionOpener(ServerTlsSettings? tlsSettings, TimeProvider timeProvider, IDataConnectionSockets sockets)
    {
        tlsHandshake = tlsSettings is null ? null : new ServerTlsHandshake(tlsSettings, TlsApplicationProtocols.ForScheme("ftp"));
        this.timeProvider = timeProvider;
        this.sockets = sockets;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Binds <paramref name="controlLocal"/>'s address - as IPv4 when it is IPv4-mapped - on a
    /// port the operating system picks.
    /// </remarks>
    /// <exception cref="DataConnectionException">
    /// <see cref="DataConnectionFailure.Unreachable"/> when <paramref name="controlLocal"/> is not
    /// an IP end point or its address could not be bound.
    /// </exception>
    public ValueTask<IPassiveDataListener> StartPassiveListenerAsync(
        EndPoint controlLocal, EndPoint controlRemote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(controlLocal);
        ArgumentNullException.ThrowIfNull(controlRemote);
        cancellationToken.ThrowIfCancellationRequested();

        if (controlLocal is not IPEndPoint local)
        {
            throw new DataConnectionException(
                DataConnectionFailure.Unreachable, $"The control connection's local end point {controlLocal} has no IP address to listen on.");
        }

        var address = DataConnectionPeer.Normalize(local.Address);
        IPassiveDataSocket socket;

        try
        {
            socket = sockets.Listen(address);
        }
        catch (SocketException exception)
        {
            throw new DataConnectionException(
                DataConnectionFailure.Unreachable, $"No data listener could be bound on {address}: {exception.Message}");
        }

        return ValueTask.FromResult<IPassiveDataListener>(
            new SocketPassiveDataListener(socket, controlRemote, CreateConnection, timeProvider));
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the connect off.</exception>
    public async ValueTask<IConnection> ConnectActiveAsync(
        EndPoint controlRemote, IPEndPoint target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(controlRemote);
        ArgumentNullException.ThrowIfNull(target);

        if (!DataConnectionPeer.IsAllowedActiveTarget(controlRemote, target))
        {
            throw new DataConnectionException(
                DataConnectionFailure.Refused,
                $"An active data connection goes only to {controlRemote}'s address on port {DataConnectionPeer.LowestActivePort} or above, not to {target}.");
        }

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            return CreateConnection(await sockets.ConnectAsync(target, linked.Token));
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new DataConnectionException(
                DataConnectionFailure.TimedOut, $"The data connection to {target} did not open within {timeout}.");
        }
        catch (SocketException exception)
        {
            throw new DataConnectionException(
                DataConnectionFailure.Unreachable, $"The data connection to {target} could not be opened: {exception.Message}");
        }
    }

    private StreamConnection CreateConnection(DataTransport transport) =>
        new(transport.Stream, transport.LocalEndPoint, transport.RemoteEndPoint, transport.Control, tlsHandshake, timeProvider);
}
