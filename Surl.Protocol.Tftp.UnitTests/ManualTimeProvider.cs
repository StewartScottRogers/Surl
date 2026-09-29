namespace Surl.Protocol.Tftp;

/// <summary>
/// A hand-written <see cref="TimeProvider"/> whose clock moves only when a test calls
/// <see cref="Advance"/>, firing every timer that falls due, so a retransmission timeout is
/// reached without waiting.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset utcNow = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => utcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        timers.Add(timer);

        return timer;
    }

    public void Advance(TimeSpan by)
    {
        utcNow += by;
        foreach (var timer in timers.ToList())
        {
            timer.FireIfDue(utcNow);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private DateTimeOffset? dueAt;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.utcNow + dueTime;
            return true;
        }

        public void FireIfDue(DateTimeOffset now)
        {
            if (dueAt is { } due && due <= now)
            {
                dueAt = null;
                callback(state);
            }
        }

        public void Dispose()
        {
            dueAt = null;
            clock.timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
