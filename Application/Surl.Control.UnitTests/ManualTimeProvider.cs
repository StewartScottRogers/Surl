namespace Surl.Control;

/// <summary>
/// A clock that moves only when a test calls <see cref="Advance"/>, firing every timer that falls due on the way;
/// <see cref="WaitForTimerAsync"/> lets a test wait until the code under test has started a timer of a given length.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    /// <summary>The time the clock starts at: 2026-11-05 10:00 UTC.</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    /// <summary><see cref="Start"/> as an HTTP date.</summary>
    public const string StartHttpDate = "Thu, 05 Nov 2026 10:00:00 GMT";

    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset utcNow = Start;

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

    /// <summary>Waits until a timer due <paramref name="dueIn"/> from now is pending.</summary>
    /// <param name="dueIn">How far ahead the timer is due.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The wait.</returns>
    public async Task WaitForTimerAsync(TimeSpan dueIn, CancellationToken cancellationToken)
    {
        while (!HasTimerDueAt(GetUtcNow() + dueIn))
        {
            await Task.Delay(1, cancellationToken);
        }
    }

    /// <summary>Moves the clock on by <paramref name="duration"/> and fires the timers now due.</summary>
    /// <param name="duration">How far.</param>
    public void Advance(TimeSpan duration)
    {
        List<ManualTimer> due;
        lock (gate)
        {
            utcNow += duration;
            due = [.. timers.Where(timer => timer.DueAt <= utcNow)];
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

    private bool HasTimerDueAt(DateTimeOffset dueAt)
    {
        lock (gate)
        {
            return timers.Any(timer => timer.DueAt == dueAt);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
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
