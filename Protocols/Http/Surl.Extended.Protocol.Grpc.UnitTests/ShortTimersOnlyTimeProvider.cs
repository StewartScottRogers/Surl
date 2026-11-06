namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// A clock whose timers due within <see cref="Threshold"/> fire at once, on the thread pool, and
/// whose longer timers never fire: a 1 ms deadline always passes during a 60 s delay, and a 1 ms
/// delay always ends, every run, with no real waiting.
/// </summary>
internal sealed class ShortTimersOnlyTimeProvider : TimeProvider
{
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(1);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ShortTimer(callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class ShortTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != Timeout.InfiniteTimeSpan && dueTime <= Threshold)
            {
                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }

            return true;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
