namespace Surl.Quic;

/// <summary>A clock that stands still until a test moves it; its timers fire when it is moved past their due time.</summary>
internal sealed class UnitTestTimeProvider : TimeProvider
{
    private readonly object sync = new();
    private readonly List<UnitTestTimer> timers = [];
    private DateTimeOffset now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>How many timers are waiting to fire.</summary>
    public int PendingTimerCount
    {
        get
        {
            lock (sync)
            {
                return timers.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (sync)
        {
            return now;
        }
    }

    public void Advance(TimeSpan by)
    {
        List<UnitTestTimer> due;
        lock (sync)
        {
            now += by;
            due = timers.Where(timer => timer.DueAt <= now).ToList();
            timers.RemoveAll(due.Contains);
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new UnitTestTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private void Schedule(UnitTestTimer timer, TimeSpan dueTime)
    {
        bool fireNow;
        lock (sync)
        {
            timers.Remove(timer);
            fireNow = dueTime == TimeSpan.Zero;
            if (dueTime != Timeout.InfiniteTimeSpan && !fireNow)
            {
                timer.DueAt = now + dueTime;
                timers.Add(timer);
            }
        }

        if (fireNow)
        {
            timer.Fire();
        }
    }

    private void Cancel(UnitTestTimer timer)
    {
        lock (sync)
        {
            timers.Remove(timer);
        }
    }

    private sealed class UnitTestTimer(UnitTestTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            owner.Schedule(this, dueTime);
            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => owner.Cancel(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
