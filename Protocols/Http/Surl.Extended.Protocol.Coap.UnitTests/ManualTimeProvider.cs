namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// A hand-written <see cref="TimeProvider"/> whose clock moves only when a test calls
/// <see cref="Advance"/>, firing every timer that falls due, so a retransmission timeout is
/// reached without waiting. Safe to use from the server's tasks and the test at once.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset utcNow = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return utcNow;
        }
    }

    /// <summary>How many timers are waiting to fire.</summary>
    public int PendingTimers
    {
        get
        {
            lock (gate)
            {
                return timers.Count(timer => timer.DueAt is not null);
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);

        lock (gate)
        {
            timers.Add(timer);
        }

        return timer;
    }

    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;

        lock (gate)
        {
            utcNow += by;
            due = [.. timers.Where(timer => timer.DueAt is { } at && at <= utcNow)];

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

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.gate)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.utcNow + dueTime;
            }

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (clock.gate)
            {
                DueAt = null;
                clock.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
