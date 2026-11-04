using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// One exchange's <see cref="ExchangeContext.DataConnections"/> (ADR-0052, decision 9): opens
/// data connections through the engine's opener and hands each out wrapped like the control
/// connection, so its bytes restart the exchange's idle clock and reach the exchange's log, with
/// a note when it opens and closes. Disposing it at the exchange's end disposes every listener
/// and data connection the server left open.
/// </summary>
/// <remarks>
/// A data connection is part of its exchange: it is not counted against the connection limits
/// and gets no exchange ID of its own.
/// </remarks>
/// <param name="opener">The engine's opener, which makes the transports.</param>
/// <param name="log">The exchange's log.</param>
/// <param name="deadlines">The exchange's clocks.</param>
internal sealed class ExchangeDataConnections(IDataConnectionOpener opener, IExchangeLog log, ExchangeDeadlines deadlines)
    : IDataConnectionOpener, IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly HashSet<IAsyncDisposable> stillOpen = [];

    /// <inheritdoc/>
    public async ValueTask<IPassiveDataListener> StartPassiveListenerAsync(
        EndPoint controlLocal, EndPoint controlRemote, CancellationToken cancellationToken)
    {
        var listener = await opener.StartPassiveListenerAsync(controlLocal, controlRemote, cancellationToken);

        return Track(new ExchangePassiveDataListener(listener, this));
    }

    /// <inheritdoc/>
    public async ValueTask<IConnection> ConnectActiveAsync(
        EndPoint controlRemote, IPEndPoint target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var connection = await opener.ConnectActiveAsync(controlRemote, target, timeout, cancellationToken);

        return Opened(connection, "active to");
    }

    /// <summary>
    /// Disposes every listener and data connection still open, even after one fails to dispose.
    /// </summary>
    /// <returns>A task that completes once every one has been disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        IAsyncDisposable[] leftOpen;

        lock (gate)
        {
            leftOpen = [.. stillOpen];
        }

        foreach (var disposable in leftOpen)
        {
            try
            {
                await disposable.DisposeAsync();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// Notes that <paramref name="connection"/> opened and hands it out wrapped.
    /// </summary>
    /// <param name="connection">The data connection the opener or a listener handed out.</param>
    /// <param name="direction">How it opened: <c>passive from</c> or <c>active to</c>.</param>
    /// <returns>The wrapped connection.</returns>
    internal IConnection Opened(IConnection connection, string direction)
    {
        log.Note($"Data connection opened: {direction} {connection.RemoteEndPoint}.");

        return Track(new ExchangeDataConnection(
            new IdleClockRestartingConnection(new RecordingConnection(connection, log), deadlines), log, this));
    }

    /// <summary>
    /// Stops tracking <paramref name="disposable"/>, which has been disposed.
    /// </summary>
    /// <param name="disposable">The listener or data connection.</param>
    internal void Forget(IAsyncDisposable disposable)
    {
        lock (gate)
        {
            stillOpen.Remove(disposable);
        }
    }

    private T Track<T>(T disposable)
        where T : IAsyncDisposable
    {
        lock (gate)
        {
            stillOpen.Add(disposable);
        }

        return disposable;
    }
}
