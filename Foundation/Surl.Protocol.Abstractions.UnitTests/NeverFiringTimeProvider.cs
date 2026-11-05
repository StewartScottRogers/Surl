namespace Surl.Protocol.Abstractions;

/// <summary>
/// A <see cref="TimeProvider"/> whose timers never fire, counting how many were created, so a
/// test can show a wait runs on the clock it was given and end it only by cancelling.
/// </summary>
internal sealed class NeverFiringTimeProvider : TimeProvider
{
    private int timersCreated;

    public int TimersCreated => Volatile.Read(ref timersCreated);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Interlocked.Increment(ref timersCreated);

        return new NeverFiringTimer();
    }

    private sealed class NeverFiringTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
