namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class RecordingIdleClockTests
{
    [TestMethod]
    public void New_IsNotPaused_AndItsFirstPauseIsPending()
    {
        var clock = new RecordingIdleClock();

        Assert.IsFalse(clock.IsPaused);
        Assert.IsFalse(clock.FirstPause.IsCompleted);
        Assert.AreEqual((0, 0), (clock.PauseCount, clock.RestartCount));
    }

    [TestMethod]
    public void Pause_ThenRestart_CountsEach_IsPausedOnlyBetween_AndCompletesTheFirstPause()
    {
        var clock = new RecordingIdleClock();

        clock.Pause();
        var pausedBetween = clock.IsPaused;
        clock.Restart();

        Assert.IsTrue(pausedBetween);
        Assert.IsFalse(clock.IsPaused);
        Assert.IsTrue(clock.FirstPause.IsCompletedSuccessfully);
        Assert.AreEqual((1, 1), (clock.PauseCount, clock.RestartCount));
    }

    [TestMethod]
    public void Pause_Twice_CountsBoth()
    {
        var clock = new RecordingIdleClock();

        clock.Pause();
        clock.Pause();

        Assert.AreEqual(2, clock.PauseCount);
    }
}
