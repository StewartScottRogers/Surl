using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Mqtt.MqttTestExchange;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class HeadTimeoutTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;
    private static readonly TimeSpan JustUnderHeadTimeout = HeadTimeout - TimeSpan.FromTicks(1);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PartialConnect_AfterHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connect = RecordedFixture.ReadRequestBytes("closed-before-connack");
        var connection = new InMemoryConnection([connect.AsMemory(0, connect.Length / 2)], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(JustUnderHeadTimeout);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        CollectionAssert.AreEqual(RecordedFixture.ReadAcceptedReplyBytes("closed-before-connack"), connection.WrittenBytes);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The first packet was not complete within the head timeout; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task NothingSent_AfterHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken));
        clock.Advance(HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ConnectedClientIdlePastTheHeadTimeout_IsNotDisconnected()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection([ClientPackets.CurlConnect()], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token));
        clock.Advance(HeadTimeout * 4);

        Assert.IsFalse(serving.IsCompleted);
        Assert.AreEqual(0, clock.PendingTimerCount);
        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);

        await idleTimeout.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task InfiniteHeadTimeout_NeverCloses()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        var connection = new InMemoryConnection([new byte[] { 0x10 }], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token, limits));
        clock.Advance(TimeSpan.FromDays(1));

        Assert.IsFalse(serving.IsCompleted);
        Assert.AreEqual(0, clock.PendingTimerCount);

        await idleTimeout.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ExchangeCancelledMidConnect_IsNotTakenForAHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection([new byte[] { 0x10 }], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token, log: log));
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    [DataRow("closed-before-connack", DisplayName = "A fetch")]
    [DataRow("publish-closed-before-connack", DisplayName = "A publish (-d)")]
    public void RecordedCloseBeforeConnack_IsReportedByUpstreamCurlAsExit56(string caseName)
    {
        Assert.AreEqual("56", Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        StringAssert.StartsWith(Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "stderr.txt")), "curl: (56) Connection disconnected");
        Assert.IsEmpty(RecordedFixture.ReadAcceptedReplyBytes(caseName));
    }

    private static MqttProtocolServer Server() => new(new MqttRetainedMessages(), new AnonymousAuthenticationPolicy());
}
