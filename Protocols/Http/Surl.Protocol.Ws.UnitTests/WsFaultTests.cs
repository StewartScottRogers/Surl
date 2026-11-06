using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ws.WsServerHarness;

namespace Surl.Protocol.Ws;

/// <summary>
/// A fault rule's chosen close (ADR-0124 decision 4, ADR-0134 decision 3), replaying the upgrade
/// pinned upstream curl 8.21.0 sent in <c>Fixtures/fault-close-1013</c> and the recorded
/// <c>upgrade-101</c>.
/// </summary>
[TestClass]
public sealed class WsFaultTests
{
    private const string Fault1013Accept = "ino39YtKRCTKyNWKqk4fvUHp8LY=";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RecordedUpgrade_WithAChosenCloseRule_Is101ThenTheChosenCloseAndPublishesOneEvent()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Scheme = "ws", Path = "/chat", Reply = "1013 try later" };

        var (connection, log, events, plan) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("fault-close-1013"), new ScriptedFaultPlan(rule));

        var expected = Recorded101Head.Replace("ktpdlwK4CWD8HWwKyLX0kug7bZ8=", Fault1013Accept, StringComparison.Ordinal)
            + "\x88\x0B\x03\xF5try later";
        Assert.AreEqual(expected, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("try later", Latin1(RecordedFixture.ReadBytes("fault-close-1013", "stdout.bin"))[2..]);
        var applied = events.Events.OfType<FaultAppliedEvent>().Single();
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/chat"), applied);
        var point = plan.Points.Single();
        Assert.AreEqual("ws://127.0.0.1:18301/", point.ListenUrl);
        Assert.AreEqual("GET", point.Command);
        Assert.IsNull(point.Account);
        Assert.Contains("GET /chat: answered 101 then CLOSE 1013 by fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task RecordedUpgrade_ForAnotherPath_IsUpgradedAsWithoutTheRule()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Path = "/other", Reply = "1011" };

        var (connection, _, events, _) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("upgrade-101"), new ScriptedFaultPlan(rule));

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>().ToList());
    }

    [TestMethod]
    public async Task ChosenCloseWithNoReason_IsACloseCarryingTheCodeAlone()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Reply = "1011" };

        var (connection, _, _, _) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("upgrade-101"), new ScriptedFaultPlan(rule));

        Assert.AreEqual(Recorded101Head + "\x88\x02\x03\xF3", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("1005")]
    [DataRow("1016 not on the wire")]
    [DataRow("1011 ")]
    [DataRow("close")]
    [DataRow(null)]
    public async Task ChosenCloseThatDoesNotFit_IsNotAppliedAndIsNoted(string? reply)
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Reply = reply };

        var (connection, log, events, _) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("upgrade-101"), new ScriptedFaultPlan(rule));

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.IsEmpty(events.Events.ToList());
        Assert.Contains("WebSocket upgrade for /chat: fault rule f1's reply does not fit the WebSocket close shape; answered as without it.", log.Notes);
    }

    [TestMethod]
    public async Task ChosenCloseWithAReasonPastTheCloseFrame_IsNotApplied()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Reply = "1011 " + new string('x', 124) };

        var (connection, _, events, _) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("upgrade-101"), new ScriptedFaultPlan(rule));

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.IsEmpty(events.Events.ToList());
    }

    [TestMethod]
    public async Task ChosenCloseScopedToAlice_FiresOnlyOnceAliceLoggedIn()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Account = "alice", Reply = "1008" };
        var policy = new UnitTestAuthenticationPolicy(request => request.Fields.Any(field => field.Key == "Authorization")
            ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice")
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));

        var (anonymous, _, anonymousEvents, _) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("upgrade-101"), new ScriptedFaultPlan(rule), policy);
        var (alice, _, aliceEvents, plan) = await ServeWithFaultsAsync(
            RecordedUpgradeRequestWith("Connection: Upgrade\r\n", "Connection: Upgrade\r\nAuthorization: Basic YWxpY2U6c2VjcmV0\r\n"), new ScriptedFaultPlan(rule), policy);

        Assert.AreEqual(Recorded101Response, Latin1(anonymous.WrittenBytes));
        Assert.IsEmpty(anonymousEvents.Events.ToList());
        Assert.AreEqual(Recorded101Head + "\x88\x02\x03\xF0", Latin1(alice.WrittenBytes));
        Assert.AreEqual("alice", plan.Points.Single().Account);
        Assert.HasCount(1, aliceEvents.Events.OfType<FaultAppliedEvent>().ToList());
    }

    [TestMethod]
    public async Task UpgradeRefusedByItsChecks_NeverAsksTheFaultPlan()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "1011" });

        var (connection, _, _, _) = await ServeWithFaultsAsync(RecordedUpgradeRequestWith("Sec-WebSocket-Version: 13", "Sec-WebSocket-Version: 8"), plan);

        Assert.StartsWith("HTTP/1.1 426 Upgrade Required\r\n", Latin1(connection.WrittenBytes));
        Assert.IsEmpty(plan.Points.ToList());
    }

    [TestMethod]
    public async Task DelayRuleAtTheCommandStage_IsNotAppliedAndIsNoted()
    {
        var rule = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 500 };

        var (connection, log, events, _) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("upgrade-101"), new ScriptedFaultPlan(rule));

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.IsEmpty(events.Events.ToList());
        Assert.Contains("GET /chat: fault rule f1 asks for Delay at the command stage, which this server does not apply; answered as without it.", log.Notes);
    }

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log, RecordingSurlEventPublisher Events, ScriptedFaultPlan Plan)> ServeWithFaultsAsync(
        byte[] request, ScriptedFaultPlan plan, IAuthenticationPolicy? policy = null)
    {
        var connection = new InMemoryConnection([request]);
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var context = Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken) with { Faults = plan, Events = events };

        await WithinTimeout(Server(policy).ServeAsync(connection, context));

        return (connection, log, events, plan);
    }
}
