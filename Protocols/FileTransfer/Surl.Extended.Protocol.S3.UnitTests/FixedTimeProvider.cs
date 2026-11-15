namespace Surl.Extended.Protocol.S3;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until a test moves it, so an answer's
/// <c>Date</c> is known and an upload's idleness can be measured. A timer it creates fires only when
/// <see cref="Advance"/> moves the clock to or past its due time, so a delay is tested without waiting.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly List<StillTimer> timers = [];
    private DateTimeOffset now = utcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (timers)
        {
            return now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new StillTimer(this, callback, state);
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
        StillTimer[] due;
        lock (timers)
        {
            now += by;
            due = [.. timers.Where(timer => timer.DueAt is { } dueAt && dueAt <= now)];
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

    private sealed class StillTimer(FixedTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.timers)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.now + dueTime;
            }

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (owner.timers)
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
