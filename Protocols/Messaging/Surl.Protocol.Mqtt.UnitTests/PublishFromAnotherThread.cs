namespace Surl.Protocol.Mqtt;

/// <summary>
/// Makes one publish on a thread of its own, as another connection would, so that a lock the
/// calling thread holds keeps it waiting.
/// </summary>
internal sealed class PublishFromAnotherThread : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly Thread thread;

    public PublishFromAnotherThread(Action publish) => thread = new Thread(() => publish()) { IsBackground = true };

    /// <summary>
    /// Starts the publish and returns once it has been made, or once its thread waits for
    /// something, which here is the bus's lock.
    /// </summary>
    public void StartAndWaitUntilItEndsOrIsBlocked()
    {
        thread.Start();
        Assert.IsTrue(SpinWait.SpinUntil(
            () => !thread.IsAlive || (thread.ThreadState & ThreadState.WaitSleepJoin) != 0,
            Timeout));
    }

    /// <summary>Waits for the publish to have been made, failing when it was never started.</summary>
    public void WaitUntilItEnds() => Assert.IsTrue(thread.Join(Timeout));

    public void Dispose() => thread.Join(Timeout);
}
