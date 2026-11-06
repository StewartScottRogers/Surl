namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IIdleClock"/> that records its pauses and restarts, so a protocol test can set
/// <see cref="ExchangeContext.IdleClock"/>, wait for a server to pause it and show the idle clock
/// stayed stopped for a whole hold. Safe under concurrent calls.
/// </summary>
public sealed class RecordingIdleClock : IIdleClock
{
    private readonly TaskCompletionSource firstPause = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int pauseCount;
    private int restartCount;

    /// <summary>
    /// Completes the first time <see cref="Pause"/> is called. A server holding a command-stage delay
    /// pauses once its wait is counting, so a test may advance its clock from here.
    /// </summary>
    public Task FirstPause => firstPause.Task;

    /// <summary>
    /// How many times <see cref="Pause"/> has been called.
    /// </summary>
    public int PauseCount => Volatile.Read(ref pauseCount);

    /// <summary>
    /// How many times <see cref="Restart"/> has been called.
    /// </summary>
    public int RestartCount => Volatile.Read(ref restartCount);

    /// <summary>
    /// Whether the clock is paused now: more pauses than restarts so far.
    /// </summary>
    public bool IsPaused => PauseCount > RestartCount;

    /// <inheritdoc/>
    public void Pause()
    {
        Interlocked.Increment(ref pauseCount);
        firstPause.TrySetResult();
    }

    /// <inheritdoc/>
    public void Restart() => Interlocked.Increment(ref restartCount);
}
