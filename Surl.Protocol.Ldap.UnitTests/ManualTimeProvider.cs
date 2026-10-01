namespace Surl.Protocol.Ldap;

/// <summary>
/// A clock that moves only when a test calls <see cref="Advance"/>, firing every timer that
/// falls due on the way, on the caller's thread. A server may create and dispose timers from
/// another thread, so the timer list is locked.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset utcNow = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// How many timers are waiting to fire.
    /// </summary>
    public int PendingTimerCount
    {
        get
        {
            lock (gate)
            {
                return timers.Count(timer => timer.DueAt is not null);
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return utcNow;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (gate)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);

        return timer;
    }

    /// <summary>
    /// Moves the clock on by <paramref name="duration"/> and fires the timers now due.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        List<ManualTimer> due;
        lock (gate)
        {
            utcNow += duration;
            due = timers.Where(timer => timer.DueAt <= utcNow).ToList();
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

        public void Dispose()
        {
            lock (owner.gate)
            {
                owner.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
