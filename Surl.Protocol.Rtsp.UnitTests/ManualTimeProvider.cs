namespace Surl.Protocol.Rtsp;

/// <summary>
/// A hand-written <see cref="TimeProvider"/> whose clock moves only when a test calls
/// <see cref="Advance"/>, firing every timer that falls due, so a timeout is tested without
/// waiting for it.
/// </summary>
/// <remarks>
/// A timer is counted in <see cref="ActiveTimerCount"/> only once its due time is set, under the
/// same lock <see cref="Advance"/> takes: a test that waits for the count on one thread and then
/// advances the clock can never see a timer whose due time is still being set on another, which
/// would miss the advance and wait forever.
/// </remarks>
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
            timer.Change(dueTime, period);
            timers.Add(timer);
        }

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
            foreach (var timer in due)
            {
                timer.DueAt = null;
            }
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
        /// <summary>When the timer is next due; read and written only under the owner's lock.</summary>
        public DateTimeOffset? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.timers)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.utcNow + dueTime;
            }

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
