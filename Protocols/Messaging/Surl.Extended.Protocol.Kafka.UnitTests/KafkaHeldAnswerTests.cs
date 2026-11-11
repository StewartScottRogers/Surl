using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// A held JoinGroup or SyncGroup pauses the exchange's idle clock until it is answered (BL-948;
/// ADR-0145 decision 9).
/// </summary>
[TestClass]
public sealed class KafkaHeldAnswerTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WaitAsync_HeldJoinGroup_PausesTheIdleClockAndRestartsItWhenAnswered()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var (held, idleClock) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        var pausedWhileHeld = idleClock.IsPaused;
        await harness.JoinV3Async(A);
        await held;

        Assert.IsTrue(pausedWhileHeld);
        Assert.AreEqual(1, idleClock.PauseCount);
        Assert.AreEqual(1, idleClock.RestartCount);
        Assert.IsFalse(idleClock.IsPaused);
    }

    [TestMethod]
    public async Task WaitAsync_JoinGroupAnsweredAtOnce_NeverPausesTheIdleClock()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var idleClock = new RecordingIdleClock();

        await harness.SendAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()), idleClock);

        Assert.AreEqual(0, idleClock.PauseCount);
    }

    [TestMethod]
    public async Task WaitAsync_HeldSyncGroup_PausesTheIdleClockUntilTheLeadersSync()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.JoinV3Async(A);
        await follower;

        var (held, idleClock) = await harness.SendHeldAsync(SyncGroup(0, "g", 2, B, null));
        await harness.SendAsync(SyncGroup(0, "g", 2, A, null, (B, "b")));
        await held;

        Assert.AreEqual(1, idleClock.PauseCount);
        Assert.AreEqual(1, idleClock.RestartCount);
    }
}
