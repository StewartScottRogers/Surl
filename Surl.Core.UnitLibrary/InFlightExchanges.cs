namespace Surl.Core;

/// <summary>
/// Counts the exchanges the serving engine has started and not yet finished, so shutdown can
/// wait for them without keeping a task per exchange for the life of the process.
/// </summary>
/// <remarks>
/// The count starts at one, held by the accepting side, so it cannot reach zero while
/// connections may still arrive; <see cref="WaitForAllAsync"/> releases that hold.
/// </remarks>
internal sealed class InFlightExchanges
{
    private readonly TaskCompletionSource allEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int count = 1;

    /// <summary>
    /// Runs one exchange on the thread pool and counts it until it ends.
    /// </summary>
    /// <param name="runExchange">The exchange.</param>
    public void Start(Func<Task> runExchange)
    {
        Interlocked.Increment(ref count);
        _ = RunAndReleaseAsync(runExchange);
    }

    /// <summary>
    /// Once nothing more will be started: waits up to <paramref name="gracePeriod"/> for every
    /// exchange to end, and if any is still running, cancels <paramref name="giveUp"/> and waits
    /// for the rest to end.
    /// </summary>
    /// <param name="gracePeriod">How long to wait before giving up on the exchanges.</param>
    /// <param name="timeProvider">The clock the grace period is measured on.</param>
    /// <param name="giveUp">Cancelled to give up on the exchanges still running.</param>
    /// <returns>A task that completes once every exchange has ended.</returns>
    public async Task WaitForAllAsync(TimeSpan gracePeriod, TimeProvider timeProvider, CancellationTokenSource giveUp)
    {
        Release();

        try
        {
            await allEnded.Task.WaitAsync(gracePeriod, timeProvider);
        }
        catch (TimeoutException)
        {
            await giveUp.CancelAsync();
            await allEnded.Task;
        }
    }

    private async Task RunAndReleaseAsync(Func<Task> runExchange)
    {
        try
        {
            await Task.Run(runExchange);
        }
        finally
        {
            Release();
        }
    }

    private void Release()
    {
        if (Interlocked.Decrement(ref count) == 0)
        {
            allEnded.TrySetResult();
        }
    }
}
