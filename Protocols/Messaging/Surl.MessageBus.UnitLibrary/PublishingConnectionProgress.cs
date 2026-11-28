namespace Surl.MessageBus;

/// <summary>
/// Whether one client connection that may publish on the bus is still handling what its client has
/// sent, or has caught up: it is waiting - for the client to send more, or as
/// <see cref="CountAsCaughtUpWhileAsync{TResult}"/> is told - or it has left. A connection that has just
/// joined (<see cref="PublishingConnections.Join"/>) is handling. Disposing it leaves.
/// </summary>
/// <remarks>
/// It exists so that a subscriber answered only with what is retained at that moment can first wait
/// for the connections that joined before it (<see cref="WaitForEarlierConnectionsAsync"/>): a client
/// that publishes, disconnects and at once reads back on a new connection gets what it published,
/// though nothing orders two connections on the wire (ADR-0100).
/// </remarks>
public sealed class PublishingConnectionProgress : IDisposable
{
    private readonly PublishingConnections joined;
    private readonly Lock gate = new();
    private TaskCompletionSource caughtUp = NewSignal();

    internal PublishingConnectionProgress(PublishingConnections joined) => this.joined = joined;

    // Completes when the connection next has caught up; already complete while it has.
    internal Task CaughtUp
    {
        get
        {
            lock (gate)
            {
                return caughtUp.Task;
            }
        }
    }

    /// <summary>
    /// Awaits <paramref name="waiting"/>, the connection counting as caught up for as long as it is
    /// not complete, and as handling again as soon as it succeeds, before anything is made of its
    /// result. A task that is already complete leaves the connection handling.
    /// </summary>
    /// <typeparam name="TResult">What the task returns.</typeparam>
    /// <param name="waiting">A read from the client, or a delay the connection is held by.</param>
    /// <returns>The task's result.</returns>
    public async ValueTask<TResult> CountAsCaughtUpWhileAsync<TResult>(ValueTask<TResult> waiting)
    {
        if (!waiting.IsCompleted)
        {
            MarkCaughtUp();
        }

        var result = await waiting;
        MarkHandling();
        return result;
    }

    /// <summary>
    /// Waits until every connection that joined before this one, and has not left, has caught up at
    /// least once, for at most <see cref="PublishingConnections.MaxWaitForEarlierConnections"/>.
    /// </summary>
    /// <param name="timeProvider">The clock the longest wait is measured on.</param>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <returns><see langword="false"/> when one was still handling after the longest wait.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> ended the wait.</exception>
    public async ValueTask<bool> WaitForEarlierConnectionsAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        try
        {
            await joined.EarlierThanHaveCaughtUp(this)
                .WaitAsync(PublishingConnections.MaxWaitForEarlierConnections, timeProvider, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Leaves: the connection has ended, so it has caught up.</summary>
    public void Dispose()
    {
        joined.Remove(this);
        MarkCaughtUp();
    }

    private void MarkCaughtUp()
    {
        lock (gate)
        {
            caughtUp.TrySetResult();
        }
    }

    private void MarkHandling()
    {
        lock (gate)
        {
            if (caughtUp.Task.IsCompleted)
            {
                caughtUp = NewSignal();
            }
        }
    }

    // A waiting subscriber goes on beside the connection that caught up, not inside its call.
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
