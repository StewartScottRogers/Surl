namespace Surl.MessageBus;

/// <summary>
/// A clock whose one timer fires only when a test calls <see cref="Fire"/>, and which records how
/// long that timer was set for.
/// </summary>
internal sealed class FiringTimeProvider : TimeProvider
{
    private Action? fire;

    /// <summary>How long the timer created last was set for.</summary>
    public TimeSpan DueTime { get; private set; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        DueTime = dueTime;
        fire = () => callback(state);
        return new NeverTimer();
    }

    /// <summary>Fires the timer created last.</summary>
    public void Fire() => fire!();

    private sealed class NeverTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
