namespace Surl.Protocol.Http;

/// <summary>
/// A hand-written <see cref="TimeProvider"/> whose clock moves only when a test calls
/// <see cref="Advance"/>, firing every timer that falls due, so a timeout is tested without
/// waiting for it.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset utcNow = start;

    /// <summary>
    /// How many timers were created and not yet disposed.
    /// </summary>
    public int ActiveTimerCount
    {
        get
        {
            lock (timers)
            {
                return timers.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (timers)
        {
            return utcNow;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (timers)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);

        return timer;
    }

    /// <summary>
    /// Moves the clock on by <paramref name="by"/> and fires, once, every timer due by then.
    /// </summary>
    public void Advance(TimeSpan by)
    {
        ManualTimer[] due;
        lock (timers)
        {
            utcNow += by;
            due = timers.Where(timer => timer.DueAt is { } dueAt && dueAt <= utcNow).ToArray();
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (timers)
        {
            timers.Remove(timer);
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

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
