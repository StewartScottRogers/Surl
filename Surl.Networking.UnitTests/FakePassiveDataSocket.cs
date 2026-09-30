using System.Net;
using System.Threading.Channels;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IPassiveDataSocket"/> whose accepts a test scripts: each <see cref="Arrive"/>
/// or <see cref="Fail"/> completes one accept, in order.
/// </summary>
/// <param name="localEndPoint">The end point the socket reports it is bound to.</param>
/// <param name="acceptsIgnoreCancellation">
/// Whether a pending accept ignores its token, as an accept that completes just as the listener
/// stops does, so the test can hand it a transport after the stop.
/// </param>
internal sealed class FakePassiveDataSocket(IPEndPoint localEndPoint, bool acceptsIgnoreCancellation = false) : IPassiveDataSocket
{
    private readonly Channel<Func<DataTransport>> arrivals = Channel.CreateUnbounded<Func<DataTransport>>();
    private readonly Lock gate = new();
    private readonly List<(int Count, TaskCompletionSource Started)> acceptCountWaits = [];
    private int acceptsStarted;

    public IPEndPoint LocalEndPoint { get; } = localEndPoint;

    public int CloseCount { get; private set; }

    public void Arrive(DataTransport transport) => arrivals.Writer.TryWrite(() => transport);

    public void Fail(Exception failure) => arrivals.Writer.TryWrite(() => throw failure);

    /// <summary>
    /// Completes once <paramref name="count"/> accepts have started.
    /// </summary>
    public Task AcceptsStarted(int count)
    {
        lock (gate)
        {
            if (acceptsStarted >= count)
            {
                return Task.CompletedTask;
            }

            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            acceptCountWaits.Add((count, started));

            return started.Task;
        }
    }

    public async Task<DataTransport> AcceptAsync(CancellationToken cancellationToken)
    {
        List<(int Count, TaskCompletionSource Started)> reached;

        lock (gate)
        {
            acceptsStarted++;
            reached = acceptCountWaits.Where(wait => wait.Count <= acceptsStarted).ToList();
            acceptCountWaits.RemoveAll(wait => wait.Count <= acceptsStarted);
        }

        foreach (var (_, started) in reached)
        {
            started.TrySetResult();
        }

        var next = await arrivals.Reader.ReadAsync(acceptsIgnoreCancellation ? CancellationToken.None : cancellationToken);

        return next();
    }

    public void Close() => CloseCount++;
}
