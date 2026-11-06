namespace Surl.Protocol.Gopher;

/// <summary>
/// A clock that moves only when a test calls <see cref="Advance"/>, firing every timer that
/// falls due on the way, on the caller's thread. A server may create and dispose timers from
/// another thread, so the timer list is locked.
/// </summary>
/// <remarks>
/// A timer's due time is set under the same lock <see cref="Advance"/> takes, before the timer is
/// counted in <see cref="PendingTimerCount"/>: a test that waits for the count on one thread and
/// then advances the clock can never see a timer whose due time is still being set on another,
/// which would miss the advance and wait forever.
/// </remarks>
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
            timer.Change(dueTime, period);
            timers.Add(timer);
        }

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

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        /// <summary>When the timer is next due; read and written only under the owner's lock.</summary>
        public DateTimeOffset? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.utcNow + dueTime;
            }

            return true;
        }

        public void Fire() => callback(state);

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
