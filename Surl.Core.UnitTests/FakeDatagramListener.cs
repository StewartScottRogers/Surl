using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IDatagramListener"/> that opens the flows a test hands it, in order.
/// </summary>
internal sealed class FakeDatagramListener(ListenUrl listenUrl) : IDatagramListener
{
    private readonly Channel<Func<IDatagramFlow>> arrivals = Channel.CreateUnbounded<Func<IDatagramFlow>>();
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ListenUrl ListenUrl { get; } = listenUrl;

    public IReadOnlyList<EndPoint> BoundEndPoints => [new IPEndPoint(IPAddress.Loopback, ListenUrl.BoundPort ?? 0)];

    public bool Disposed => disposed.Task.IsCompleted;

    public Task WhenDisposed => disposed.Task;

    public void Open(IDatagramFlow flow) => arrivals.Writer.TryWrite(() => flow);

    public void FailNextAccept(Exception exception) => arrivals.Writer.TryWrite(() => throw exception);

    public async ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken)
    {
        var next = await arrivals.Reader.ReadAsync(cancellationToken);

        return next();
    }

    public ValueTask DisposeAsync()
    {
        disposed.TrySetResult();

        return ValueTask.CompletedTask;
    }
}
