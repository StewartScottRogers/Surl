using System.Diagnostics;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// A listener factory that binds nothing: each connection listener it starts reports a bound
/// port and waits in <see cref="IConnectionListener.AcceptAsync"/> until cancelled, or throws
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

    /// <summary>Every listen URL a listener was started for, in order.</summary>
    public List<ListenUrl> StartedListenUrls { get; } = [];

    /// <summary>Completes when the engine first waits for a connection: every listener is up.</summary>
    public TaskCompletionSource AcceptStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        if (BindFailure is not null)
        {
            throw BindFailure;
        }

        StartedListenUrls.Add(listenUrl);
        return ValueTask.FromResult<IConnectionListener>(new FakeConnectionListener(listenUrl.WithBoundPort(BoundPort), this));
    }

    public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private sealed class FakeConnectionListener(ListenUrl listenUrl, FakeListenerFactory factory) : IConnectionListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, BoundPort)];

        public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken)
        {
            factory.AcceptStarted.TrySetResult();
            if (factory.AcceptFailure is not null)
            {
                throw factory.AcceptFailure;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
