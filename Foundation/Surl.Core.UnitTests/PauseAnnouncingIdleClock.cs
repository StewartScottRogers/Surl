using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IIdleClock"/> that forwards to <paramref name="inner"/>, usually the engine's own,
/// and completes <see cref="Paused"/> once <paramref name="inner"/> has been paused, so a test
/// knows when it may advance the clock under a held exchange.
/// </summary>
internal sealed class PauseAnnouncingIdleClock(IIdleClock inner) : IIdleClock
{
    private readonly TaskCompletionSource paused = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes once the first <see cref="Pause"/> has paused the inner clock.
    /// </summary>
    public Task Paused => paused.Task;

    public void Pause()
    {
        inner.Pause();
        paused.TrySetResult();
    }

    public void Restart() => inner.Restart();
}
