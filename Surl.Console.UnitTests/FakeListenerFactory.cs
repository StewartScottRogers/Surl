using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// A listener factory that binds nothing: each connection or datagram listener it starts
/// reports a bound port and waits in <see cref="IConnectionListener.AcceptAsync"/> or
/// <see cref="IDatagramListener.AcceptFlowAsync"/> until cancelled, or throws
/// <see cref="BindFailure"/> or <see cref="AcceptFailure"/> when one is set.
/// </summary>
internal sealed class FakeListenerFactory : IListenerFactory
{
    /// <summary>The port every listener reports as bound.</summary>
    public const int BoundPort = 49731;

    /// <summary>Thrown by the next start instead of starting, when set.</summary>
    public ListenerBindException? BindFailure { get; init; }

    /// <summary>Thrown by every accept instead of waiting, when set.</summary>
    public Exception? AcceptFailure { get; init; }

    /// <summary>Handed out by the first connection accept instead of waiting, when set.</summary>
    public FakeConnection? Connection
    {
        get => connection;
        init => connection = value;
    }

    private FakeConnection? connection;

    private readonly ConcurrentDictionary<string, Task<FakeConnection>> connectionsByScheme = new(StringComparer.Ordinal);

    /// <summary>
    /// Handed out, once each, by the first connection accept of the listener for its scheme,
    /// when set: the accept waits for the task, so a test can hand one scheme its connection
    /// only after another's has ended. Checked after <see cref="Connection"/>.
    /// </summary>
    public IReadOnlyDictionary<string, Task<FakeConnection>> ConnectionsByScheme
    {
        get => connectionsByScheme;
        init => connectionsByScheme = new(value, StringComparer.Ordinal);
    }

    /// <summary>Every listen URL a listener was started for, in order.</summary>
    public List<ListenUrl> StartedListenUrls { get; } = [];

    /// <summary>Every listen URL a datagram listener was started for, in order.</summary>
    public List<ListenUrl> StartedDatagramListenUrls { get; } = [];

    /// <summary>Completes when the engine first waits for a connection or a flow: every listener is up.</summary>
    public TaskCompletionSource AcceptStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        ThrowBindFailureWhenSet();
        StartedListenUrls.Add(listenUrl);
        return ValueTask.FromResult<IConnectionListener>(new FakeConnectionListener(listenUrl.WithBoundPort(BoundPort), this));
    }

    public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        ThrowBindFailureWhenSet();
        StartedListenUrls.Add(listenUrl);
        StartedDatagramListenUrls.Add(listenUrl);
        return ValueTask.FromResult<IDatagramListener>(new FakeDatagramListener(listenUrl.WithBoundPort(BoundPort), this));
    }

    private void ThrowBindFailureWhenSet()
    {
        if (BindFailure is not null)
        {
            throw BindFailure;
        }
    }

    private async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        AcceptStarted.TrySetResult();
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
            if (Interlocked.Exchange(ref factory.connection, null) is { } connection)
            {
                return connection;
            }

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
