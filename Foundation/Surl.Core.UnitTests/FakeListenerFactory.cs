using System.Collections.Concurrent;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IListenerFactory"/> that starts a <see cref="FakeConnectionListener"/> or a
/// <see cref="FakeDatagramListener"/> per listen URL, or throws the exception a test scripted
/// for it, and records every request.
/// </summary>
internal sealed class FakeListenerFactory : IListenerFactory
{
    private readonly ConcurrentQueue<ListenUrl> startRequests = new();
    private readonly ConcurrentDictionary<ListenUrl, Exception> startFailures = new();
    private readonly ConcurrentDictionary<ListenUrl, FakeConnectionListener> listeners = new();
    private readonly ConcurrentQueue<ListenUrl> datagramStartRequests = new();
    private readonly ConcurrentDictionary<ListenUrl, FakeDatagramListener> datagramListeners = new();

    /// <summary>
    /// Every connection listener asked for, in order.
    /// </summary>
    public IReadOnlyList<ListenUrl> StartRequests => [.. startRequests];

    /// <summary>
    /// Every datagram listener asked for, in order.
    /// </summary>
    public IReadOnlyList<ListenUrl> DatagramStartRequests => [.. datagramStartRequests];

    public FakeListenerFactory FailToStart(ListenUrl listenUrl, Exception exception)
    {
        startFailures[listenUrl] = exception;

        return this;
    }

    /// <summary>
    /// The listener this factory starts, or started, for <paramref name="listenUrl"/>. An
    /// ephemeral port binds 40000.
    /// </summary>
    public FakeConnectionListener ListenerFor(ListenUrl listenUrl) =>
        listeners.GetOrAdd(
            listenUrl, url => new FakeConnectionListener(url.WithBoundPort(url.Port == 0 ? 40000 : url.Port)));

    public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        startRequests.Enqueue(listenUrl);

        if (startFailures.TryGetValue(listenUrl, out var exception))
        {
            throw exception;
        }

        return ValueTask.FromResult<IConnectionListener>(ListenerFor(listenUrl));
    }

    /// <summary>
    /// The datagram listener this factory starts, or started, for <paramref name="listenUrl"/>.
    /// An ephemeral port binds 40000.
    /// </summary>
    public FakeDatagramListener DatagramListenerFor(ListenUrl listenUrl) =>
        datagramListeners.GetOrAdd(
            listenUrl, url => new FakeDatagramListener(url.WithBoundPort(url.Port == 0 ? 40000 : url.Port)));

    public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        datagramStartRequests.Enqueue(listenUrl);

        if (startFailures.TryGetValue(listenUrl, out var exception))
        {
            throw exception;
        }

        return ValueTask.FromResult<IDatagramListener>(DatagramListenerFor(listenUrl));
    }
}
