using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Wraps the listener factory the serving engine starts listeners through, to report on the
/// starts: once the last of the expected listeners has bound, it writes one status line per
/// listener in the order they started (ADR-0007 section 7); and it keeps the
/// <see cref="ListenerBindException"/> a failed start threw, so <c>surl</c> can say what
/// could not be bound (ADR-0007 section 5).
/// </summary>
/// <param name="inner">The factory that really starts the listeners.</param>
/// <param name="statusLine">Writes the status lines.</param>
/// <param name="expectedListenerCount">How many listeners the engine starts: one per listen URL.</param>
internal sealed class ListenerStartReporter(IListenerFactory inner, ListenerStatusLine statusLine, int expectedListenerCount)
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
        IConnectionListener listener;
        try
        {
            listener = await inner.StartConnectionListenerAsync(listenUrl, cancellationToken);
        }
        catch (ListenerBindException bindFailure)
        {
            BindFailure = bindFailure;
            throw;
        }

        boundListenUrls.Add(listener.ListenUrl);
        if (boundListenUrls.Count == expectedListenerCount)
        {
            boundListenUrls.ForEach(statusLine.Write);
        }

        return listener;
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">
    /// Always: the serving engine starts no datagram listener yet (BL-032).
    /// </exception>
    public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The serving engine starts no datagram listener yet.");
}
