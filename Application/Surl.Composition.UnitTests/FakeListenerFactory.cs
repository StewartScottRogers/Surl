using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A listener factory that binds nothing: each connection or datagram listener it starts reports
/// <see cref="BoundPort"/> and waits in its accept until cancelled, or the start throws
/// <see cref="BindFailure"/> when one is set.
/// </summary>
internal sealed class FakeListenerFactory : IListenerFactory
{
    /// <summary>The port every listener reports as bound.</summary>
    public const int BoundPort = 49731;

    /// <summary>Thrown by every start instead of starting, when set.</summary>
    public ListenerBindException? BindFailure { get; init; }

    /// <summary>Thrown by every accept instead of waiting, when set.</summary>
    public Exception? AcceptFailure { get; init; }

    private readonly ConcurrentDictionary<string, Task<FakeConnection>> connectionsByScheme = new(StringComparer.Ordinal);

    /// <summary>
    /// Handed out, once each, by the first connection accept of the listener for its scheme, when
    /// set: the accept waits for the task, so a test can hand one scheme its connection only after
    /// another's has ended. Every later accept waits until cancelled.
    /// </summary>
    public IReadOnlyDictionary<string, Task<FakeConnection>> ConnectionsByScheme
    {
        get => connectionsByScheme;
        init => connectionsByScheme = new(value, StringComparer.Ordinal);
    }

    public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        ThrowBindFailureWhenSet();
        return ValueTask.FromResult<IConnectionListener>(new FakeConnectionListener(listenUrl.WithBoundPort(BoundPort), this));
    }

    public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        ThrowBindFailureWhenSet();
        var reported = ReportsDatagramBoundPort ? listenUrl.WithBoundPort(BoundPort) : listenUrl;
        return ValueTask.FromResult<IDatagramListener>(new FakeDatagramListener(reported, this));
    }

    /// <summary>
    /// Whether each datagram listener reports <see cref="BoundPort"/> on its listen URL; when
    /// <see langword="false"/>, it reports the listen URL as given, with no bound port.
    /// </summary>
    public bool ReportsDatagramBoundPort { get; init; } = true;

    private void ThrowBindFailureWhenSet()
    {
        if (BindFailure is not null)
        {
            throw BindFailure;
        }
    }

    private async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        if (AcceptFailure is not null)
        {
            throw AcceptFailure;
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    }

    private sealed class FakeConnectionListener(ListenUrl listenUrl, FakeListenerFactory factory) : IConnectionListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, BoundPort)];

        public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken)
        {
            if (factory.connectionsByScheme.TryRemove(ListenUrl.Scheme, out var schemeConnection))
            {
                return await schemeConnection.WaitAsync(cancellationToken);
            }

            await factory.WaitUntilCancelledAsync(cancellationToken);
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeDatagramListener(ListenUrl listenUrl, FakeListenerFactory factory) : IDatagramListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, BoundPort)];

        public async ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken)
        {
            await factory.WaitUntilCancelledAsync(cancellationToken);
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
