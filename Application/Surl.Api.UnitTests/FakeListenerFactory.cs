using System.Diagnostics;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A listener factory that binds nothing: each listener it starts reports <see cref="BoundPort"/>
/// and waits in its accept until cancelled. With <see cref="BindFails"/> every start throws an
/// address-in-use bind failure; with <see cref="StartWaitsUntilCancelled"/> every start waits for
/// its cancellation instead of binding.
/// </summary>
internal sealed class FakeListenerFactory : IListenerFactory
{
    /// <summary>The port every listener reports as bound.</summary>
    public const int BoundPort = 49731;

    private int startedListenerCount;
    private int disposedListenerCount;
    private IConnection? connectionToAccept;

    /// <summary>
    /// A connection the first accept of any connection listener returns, once; every other accept
    /// waits until cancelled.
    /// </summary>
    public IConnection? ConnectionToAccept
    {
        get => Volatile.Read(ref connectionToAccept);
        init => connectionToAccept = value;
    }

    /// <summary>Whether every start throws an address-in-use <see cref="ListenerBindException"/>.</summary>
    public bool BindFails { get; init; }

    /// <summary>Whether every start waits until it is cancelled instead of binding.</summary>
    public bool StartWaitsUntilCancelled { get; init; }

    /// <summary>How many listeners were started and are not yet disposed.</summary>
    public int OpenListenerCount => Volatile.Read(ref startedListenerCount) - Volatile.Read(ref disposedListenerCount);

    public async ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        await StartAsync(listenUrl, cancellationToken);
        return new FakeConnectionListener(listenUrl.WithBoundPort(BoundPort), this);
    }

    public async ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        await StartAsync(listenUrl, cancellationToken);
        return new FakeDatagramListener(listenUrl.WithBoundPort(BoundPort), this);
    }

    private async Task StartAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        if (BindFails)
        {
            throw new ListenerBindException(listenUrl, null, ListenerBindFailure.AddressInUse, null);
        }

        if (StartWaitsUntilCancelled)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        Interlocked.Increment(ref startedListenerCount);
    }

    private static async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
        cancellationToken.ThrowIfCancellationRequested();
        throw new UnreachableException();
    }

    private sealed class FakeConnectionListener(ListenUrl listenUrl, FakeListenerFactory factory) : IConnectionListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, BoundPort)];

        public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref factory.connectionToAccept, null) is { } connection)
            {
                return connection;
            }

            await WaitUntilCancelledAsync(cancellationToken);
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref factory.disposedListenerCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeDatagramListener(ListenUrl listenUrl, FakeListenerFactory factory) : IDatagramListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, BoundPort)];

        public async ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken)
        {
            await WaitUntilCancelledAsync(cancellationToken);
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref factory.disposedListenerCount);
            return ValueTask.CompletedTask;
        }
    }
}
