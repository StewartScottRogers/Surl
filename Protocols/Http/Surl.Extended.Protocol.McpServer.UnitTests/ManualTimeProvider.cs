namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock moves only when a test calls <see cref="Advance"/>,
/// firing every timer that falls due. The FakeTimeProvider package is not approved.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private readonly List<(int Count, TaskCompletionSource Reached)> timerCountWaits = [];
    private readonly TaskCompletionSource firstTimerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset now = McpHttpHarness.Now;

    /// <summary>
    /// Completes once the first timer has been created.
    /// </summary>
    public Task FirstTimerCreated => firstTimerCreated.Task;

    /// <summary>
    /// Completes once <paramref name="count"/> timers have been created in all.
    /// </summary>
    public Task TimersCreated(int count)
    {
        lock (gate)
        {
            if (timers.Count >= count)
            {
                return Task.CompletedTask;
            }

            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            timerCountWaits.Add((count, reached));

            return reached.Task;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);

        timer.Change(dueTime, period);

        lock (gate)
        {
            timers.Add(timer);
            timerCountWaits.RemoveAll(wait => wait.Count <= timers.Count && wait.Reached.TrySetResult());
        }

        firstTimerCreated.TrySetResult();

        return timer;
    }

    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;

        lock (gate)
        {
            now += by;
            due = timers.Where(timer => timer.DueAt <= now).ToList();
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;

            return true;
        }

        public void Fire()
        {
            DueAt = null;
            callback(state);
        }

        public void Dispose() => DueAt = null;

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
