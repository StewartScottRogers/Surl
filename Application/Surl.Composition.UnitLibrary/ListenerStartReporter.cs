using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Wraps the listener factory the serving engine starts listeners through, to report on the
/// starts: once the last of the expected listeners has bound, connection and datagram
/// listeners alike, it writes one status line per listener in the order they started
/// (ADR-0007 section 7) and hands the bound listen URLs to <paramref name="allListenersBound"/>
/// (ADR-0099, decision 2); and it keeps the <see cref="ListenerBindException"/> a failed start
/// threw, so the host can say what could not be bound (ADR-0007 section 5). A pair bound on one
/// port (an <c>https</c> listen URL with <c>--http3</c>) counts as its connection listener alone,
/// whose status line names the port both share, and its datagram listener is handed to
/// <paramref name="sharedPortDatagramListenerBound"/> instead (ADR-0082, decision 6).
/// </summary>
/// <param name="inner">The factory that really starts the listeners.</param>
/// <param name="statusLine">Writes the status lines.</param>
/// <param name="expectedListenerCount">How many listeners the engine starts: one per listen URL.</param>
/// <param name="allListenersBound">Called once with every bound listen URL, in start order, after the status lines.</param>
/// <param name="sharedPortDatagramListenerBound">Called with the bound listen URL of each datagram listener bound beside a connection listener on its port.</param>
internal sealed class ListenerStartReporter(
    IListenerFactory inner,
    ListenerStatusLine statusLine,
    int expectedListenerCount,
    Action<IReadOnlyList<ListenUrl>> allListenersBound,
    Action<ListenUrl> sharedPortDatagramListenerBound)
    : IListenerFactory
{
    private readonly List<ListenUrl> boundListenUrls = [];

    /// <summary>
    /// The bind failure that stopped a start, or <see langword="null"/> while none has.
    /// </summary>
    public ListenerBindException? BindFailure { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<IConnectionListener> StartConnectionListenerAsync(
        ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        var listener = await KeepBindFailureAsync(() => inner.StartConnectionListenerAsync(listenUrl, cancellationToken));
        ReportBound(listener.ListenUrl);
        return listener;
    }

    /// <inheritdoc/>
    public async ValueTask<IDatagramListener> StartDatagramListenerAsync(
        ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        var listener = await KeepBindFailureAsync(() => inner.StartDatagramListenerAsync(listenUrl, cancellationToken));
        ReportBound(listener.ListenUrl);
        return listener;
    }

    /// <inheritdoc/>
    public async ValueTask<SharedPortListeners> StartSharedPortListenersAsync(
        ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        var listeners = await KeepBindFailureAsync(() => inner.StartSharedPortListenersAsync(listenUrl, cancellationToken));
        sharedPortDatagramListenerBound(listeners.DatagramListener.ListenUrl);
        ReportBound(listeners.ConnectionListener.ListenUrl);
        return listeners;
    }

    private async ValueTask<TListener> KeepBindFailureAsync<TListener>(Func<ValueTask<TListener>> start)
    {
        try
        {
            return await start();
        }
        catch (ListenerBindException bindFailure)
        {
            BindFailure = bindFailure;
            throw;
        }
    }

    private void ReportBound(ListenUrl boundListenUrl)
    {
        boundListenUrls.Add(boundListenUrl);
        if (boundListenUrls.Count == expectedListenerCount)
        {
            boundListenUrls.ForEach(statusLine.Write);
            allListenersBound([.. boundListenUrls]);
        }
    }
}
