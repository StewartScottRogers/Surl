namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until a test moves it, so an answer's
/// <c>Date</c> is known. A timer it creates fires only when <see cref="Advance"/> reaches its due time,
/// so a delay is tested without waiting for it.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly List<StillTimer> timers = [];

    /// <summary>
    /// The time the clock stands at.
    /// </summary>
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new StillTimer(this, callback, state, UtcNow + dueTime);
        lock (timers)
        {
            timers.Add(timer);
        }

        return timer;
    }

    /// <summary>
    /// Moves the clock on by <paramref name="by"/> and fires, once, every timer due by then.
    /// </summary>
    /// <param name="by">How far to move the clock.</param>
    public void Advance(TimeSpan by)
    {
        StillTimer[] due;
        lock (timers)
        {
            UtcNow += by;
            due = [.. timers.Where(timer => timer.DueAt <= UtcNow)];
            timers.RemoveAll(timer => timer.DueAt <= UtcNow);
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class StillTimer(FixedTimeProvider owner, TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        public DateTimeOffset DueAt { get; } = dueAt;

        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

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
