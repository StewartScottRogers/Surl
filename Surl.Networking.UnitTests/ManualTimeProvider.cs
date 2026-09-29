namespace Surl.Networking;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock moves only when a test calls <see cref="Advance"/>,
/// firing every timer that falls due. The FakeTimeProvider package is not approved.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private readonly List<(int Count, TaskCompletionSource Created)> timerCountWaits = [];
    private DateTimeOffset now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private int timersCreated;

    /// <summary>
    /// Completes once the first timer has been created.
    /// </summary>
    public Task FirstTimerCreated => TimersCreated(1);

    /// <summary>
    /// Completes once <paramref name="count"/> timers have been created.
    /// </summary>
    public Task TimersCreated(int count)
    {
        lock (gate)
        {
            if (timersCreated >= count)
            {
                return Task.CompletedTask;
            }

            var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            timerCountWaits.Add((count, created));

            return created.Task;
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

        List<(int Count, TaskCompletionSource Created)> reached;

        lock (gate)
        {
            timers.Add(timer);
            timersCreated++;
            reached = timerCountWaits.Where(wait => wait.Count <= timersCreated).ToList();
            timerCountWaits.RemoveAll(wait => wait.Count <= timersCreated);
        }

        foreach (var (_, created) in reached)
        {
            created.TrySetResult();
        }

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
