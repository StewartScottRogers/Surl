namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// A hand-written <see cref="TimeProvider"/> whose clock moves only when a test calls
/// <see cref="Advance"/>, firing every timer that falls due, so the heartbeat, the connect
/// timeout and the farewell's deadline are reached without waiting. Safe to use from the server's tasks and
/// the test at once. It starts at 2026-11-05 12:00:00 UTC.
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

    /// <summary>
    /// Waits, in real time, until a timer is due at <paramref name="at"/>: the server is then waiting
    /// on it, and an <see cref="Advance"/> to it fires it.
    /// </summary>
    public async Task WaitForTimerDueAtAsync(DateTimeOffset at)
    {
        var giveUpAt = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!HasTimerDueAt(at))
        {
            if (DateTime.UtcNow > giveUpAt)
            {
                throw new TimeoutException($"No timer became due at {at:O}.");
            }

            await Task.Delay(1);
        }
    }

    /// <summary>
    /// Waits for a timer due <paramref name="by"/> from now, then moves the clock to it.
    /// </summary>
    public async Task AdvanceWhenWaitingAsync(TimeSpan by)
    {
        await WaitForTimerDueAtAsync(GetUtcNow() + by);
        Advance(by);
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

    private bool HasTimerDueAt(DateTimeOffset at)
    {
        lock (gate)
        {
            return timers.Any(timer => timer.DueAt == at);
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
