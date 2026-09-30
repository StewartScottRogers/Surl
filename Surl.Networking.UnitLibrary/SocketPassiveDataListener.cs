using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// A passive FTP data listener over one listening socket (ADR-0052, decisions 6 and 9): hands
/// out the first connection from the control connection's peer address within the timeout,
/// resets every connection from any other address unseen and goes on waiting, and stops
/// listening once it has handed its connection out. Accept failures of one client are absorbed
/// by <see cref="AcceptRace{TAccepted}"/> (ADR-0022).
/// </summary>
internal sealed class SocketPassiveDataListener : IPassiveDataListener
{
    private readonly IPassiveDataSocket socket;
    private readonly EndPoint controlRemote;
    private readonly Func<DataTransport, IConnection> createConnection;
    private readonly TimeProvider timeProvider;
    private readonly AcceptRace<DataTransport> acceptRace;
    private bool handedOut;
    private bool disposed;

    /// <summary>
    /// Creates the listener over a socket that is already listening.
    /// </summary>
    /// <param name="socket">The listening socket; the listener closes it.</param>
    /// <param name="controlRemote">The control connection's remote end point, curl's address.</param>
    /// <param name="createConnection">Turns the peer's accepted transport into the data connection.</param>
    /// <param name="timeProvider">Times <see cref="AcceptAsync"/>'s timeout.</param>
    public SocketPassiveDataListener(
        IPassiveDataSocket socket,
        EndPoint controlRemote,
        Func<DataTransport, IConnection> createConnection,
        TimeProvider timeProvider)
    {
        this.socket = socket;
        this.controlRemote = controlRemote;
        this.createConnection = createConnection;
        this.timeProvider = timeProvider;
        acceptRace = new AcceptRace<DataTransport>(1, (_, token) => socket.AcceptAsync(token), transport => transport.ResetAndRelease());
    }

    /// <inheritdoc/>
    public IPEndPoint LocalEndPoint => socket.LocalEndPoint;

    /// <inheritdoc/>
    /// <exception cref="DataConnectionException">
    /// <see cref="DataConnectionFailure.TimedOut"/> when no connection from the peer arrived within
    /// <paramref name="timeout"/>; <see cref="DataConnectionFailure.Unreachable"/> when the
    /// listening socket failed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The listener has already handed out its connection.</exception>
    /// <exception cref="ObjectDisposedException">The listener was disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the wait off.</exception>
    public async ValueTask<IConnection> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (handedOut)
        {
            throw new InvalidOperationException("The passive data listener has already handed out its connection.");
        }

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        while (true)
        {
            var transport = await AcceptWithinTimeoutAsync(timeout, timeoutSource, linked.Token);

            if (DataConnectionPeer.IsPeer(controlRemote, transport.RemoteEndPoint))
            {
                handedOut = true;
                await StopListeningAsync();

                return createConnection(transport);
            }

            transport.ResetAndRelease();
        }
    }

    /// <summary>
    /// Stops listening and closes the listening socket; a connection already handed out is
    /// unaffected. Calling it twice is harmless.
    /// </summary>
    /// <returns>A task that completes once the socket is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await StopListeningAsync();
    }

    private async Task<DataTransport> AcceptWithinTimeoutAsync(
        TimeSpan timeout, CancellationTokenSource timeoutSource, CancellationToken linkedToken)
    {
        try
        {
            return await acceptRace.AcceptNextAsync(linkedToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new DataConnectionException(
                DataConnectionFailure.TimedOut, $"No data connection from the client arrived within {timeout}.");
        }
        catch (IOException exception)
        {
            throw new DataConnectionException(
                DataConnectionFailure.Unreachable, $"The passive data listener failed: {exception.Message}");
        }
    }

    private async Task StopListeningAsync()
    {
        var stopping = acceptRace.StopAsync();
        socket.Close();
        await stopping;
    }
}
