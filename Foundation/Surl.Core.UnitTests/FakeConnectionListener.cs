using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IConnectionListener"/> that accepts the connections a test hands it, in order.
/// </summary>
internal sealed class FakeConnectionListener(ListenUrl listenUrl) : IConnectionListener
{
    private readonly Channel<Func<IConnection>> arrivals = Channel.CreateUnbounded<Func<IConnection>>();
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int acceptCalls;

    public ListenUrl ListenUrl { get; } = listenUrl;

    public IReadOnlyList<EndPoint> BoundEndPoints => [new IPEndPoint(IPAddress.Loopback, ListenUrl.BoundPort ?? 0)];

    public bool Disposed => disposed.Task.IsCompleted;

    public Task WhenDisposed => disposed.Task;

    /// <summary>
    /// How many times <see cref="AcceptAsync"/> has been called.
    /// </summary>
    public int AcceptCalls => Volatile.Read(ref acceptCalls);

    public void Connect(IConnection connection) => arrivals.Writer.TryWrite(() => connection);

    public void FailNextAccept(Exception exception) => arrivals.Writer.TryWrite(() => throw exception);

    public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref acceptCalls);

        var next = await arrivals.Reader.ReadAsync(cancellationToken);

        return next();
    }

    /// <summary>
    /// When set, <see cref="DisposeAsync"/> throws it after marking the listener disposed.
    /// </summary>
    public Exception? DisposeFailure { get; set; }

    public ValueTask DisposeAsync()
    {
        disposed.TrySetResult();

        return DisposeFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(DisposeFailure);
    }
}
