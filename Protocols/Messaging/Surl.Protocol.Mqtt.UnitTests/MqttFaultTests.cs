using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The chosen-error fault (ADR-0124 decision 4, ADR-0139): a matching <see cref="FaultKind.Error"/>
/// rule answers a <c>SUBSCRIBE</c> filter with <c>SUBACK</c> <c>0x80</c>, an accepted <c>CONNECT</c>
/// with the rule's <c>CONNACK</c> return code and a close, and a <c>PUBLISH</c> with a close and
/// nothing published, each publishing one <see cref="FaultAppliedEvent"/>. The client packets are
/// the ones upstream curl 8.21.0 sends (ADR-0102, R12 and R14), which ends with exit 8 at a
/// <c>SUBACK</c> <c>0x80</c> (R12) or a <c>CONNACK</c> other than 0 (BL-115), and exit 0 after a QoS 0
/// publish, for which it waits on nothing (R14).
/// </summary>
[TestClass]
public sealed class MqttFaultTests
{
    private static readonly byte[] ConnackAccepted = [0x20, 0x02, 0x00, 0x00];
    private static readonly byte[] Disconnect = [0xE0, 0x00];

    // The SUBSCRIBE upstream curl 8.21.0 sends for mqtt://…/a/b (ADR-0102, R12).
    private static readonly byte[] CurlSubscribeToAB = [0x82, 0x08, 0x00, 0x01, 0x00, 0x03, 0x61, 0x2F, 0x62, 0x00];

    // The PUBLISH upstream curl 8.21.0 sends for -d hi mqtt://…/a/b (ADR-0102, R14).
    private static readonly byte[] CurlPublishHiToAB = [0x30, 0x07, 0x00, 0x03, 0x61, 0x2F, 0x62, 0x68, 0x69];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_SubscribeToABAndCDUnderARuleOnAB_AnswersSubAck0x80ForABAndGrantsCD()
    {
        var bus = TestMessageBus.New();
        bus.Retain("a/b", "no"u8);
        bus.Retain("c/d", "hi"u8);
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(Rule(command: "SUBSCRIBE", path: "a/b"));

        var (written, log) = await ServeAsync(bus, plan, events, LoginAs("ann"), ClientPackets.Subscribe(1, ("a/b", 0), ("c/d", 0), ("a/#/b", 0)));

        byte[] expected = [.. ConnackAccepted, 0x90, 0x05, 0x00, 0x01, 0x80, 0x00, 0x80, 0x31, 0x07, 0x00, 0x03, 0x63, 0x2F, 0x64, 0x68, 0x69, .. Disconnect];
        CollectionAssert.AreEqual(expected, written);
        AssertOneFaultApplied(events, "SUBSCRIBE a/b");
        CollectionAssert.Contains(log.Notes.ToArray(), "SUBSCRIBE a/b answered by fault rule f1 with reason code 135.");
        CollectionAssert.AreEqual(new[] { "CONNECT", "SUBSCRIBE", "SUBSCRIBE" }, plan.Points.Select(point => point.Command).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_CurlSubscribeUnderARule_AnswersSubAck0x80ThenDisconnect()
    {
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(TestMessageBus.New(), new ScriptedFaultPlan(Rule(command: "SUBSCRIBE")), events, LoginAs("ann"), CurlSubscribeToAB);

        // R12's bytes: upstream curl 8.21.0 answers SUBACK 0x80 with exit 8.
        byte[] expected = [.. ConnackAccepted, 0x90, 0x03, 0x00, 0x01, 0x80, .. Disconnect];
        CollectionAssert.AreEqual(expected, written);
        AssertOneFaultApplied(events, "SUBSCRIBE a/b");
    }

    [TestMethod]
    [DataRow("132", (byte)0x01)]
    [DataRow("133", (byte)0x02)]
    [DataRow("134", (byte)0x04)]
    [DataRow("135", (byte)0x05)]
    [DataRow("136", (byte)0x03)]
    [DataRow("128", (byte)0x03)]
    [DataRow("255", (byte)0x03)]
    public async Task ServeAsync_ConnectUnderARule_AnswersTheRulesConnackCodeAndCloses(string reply, byte returnCode)
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(Rule(command: "CONNECT") with { Reply = reply });

        var (written, log) = await ServeAsync(TestMessageBus.New(), plan, events, LoginAs("ann"), CurlSubscribeToAB);

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, returnCode }, written);
        AssertOneFaultApplied(events, "CONNECT");
        CollectionAssert.Contains(log.Notes.ToArray(), $"CONNECT answered by fault rule f1 with reason code {reply}.");
        var point = plan.Points.Single();
        Assert.AreEqual(FaultStage.Command, point.Stage);
        Assert.AreEqual(1L, point.ExchangeId);
        Assert.AreEqual("mqtt://127.0.0.1:18883/", point.ListenUrl);
        Assert.AreEqual("mqtt", point.Scheme);
        Assert.AreEqual("ann", point.Account);
        Assert.IsNull(point.Path);
    }

    [TestMethod]
    public async Task ServeAsync_ARuleScopedToAlice_FiresForAliceOnly()
    {
        var rule = Rule(command: "SUBSCRIBE") with { Account = "alice" };

        var aliceEvents = new RecordingSurlEventPublisher();
        var (aliceWritten, _) = await ServeAsync(TestMessageBus.New(), new ScriptedFaultPlan(rule), aliceEvents, LoginAs("alice"), CurlSubscribeToAB);
        var bobEvents = new RecordingSurlEventPublisher();
        var (bobWritten, _) = await ServeAsync(TestMessageBus.New(), new ScriptedFaultPlan(rule), bobEvents, LoginAs("bob"), CurlSubscribeToAB);

        CollectionAssert.AreEqual(SubAckThenDisconnect(0x80), aliceWritten);
        AssertOneFaultApplied(aliceEvents, "SUBSCRIBE a/b");
        CollectionAssert.AreEqual(SubAckThenDisconnect(0x00), bobWritten);
        Assert.IsEmpty(bobEvents.Events);
    }

    [TestMethod]
    public async Task ServeAsync_ALoginAcceptedUnchecked_CarriesNoAccountToThePlan()
    {
        var plan = new ScriptedFaultPlan(Rule(command: "SUBSCRIBE") with { Account = "ann" });
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.AcceptedUnchecked);

        var (written, _) = await ServeAsync(TestMessageBus.New(), plan, new RecordingSurlEventPublisher(), LoginAs("ann"), [CurlSubscribeToAB], policy);

        CollectionAssert.AreEqual(SubAckThenDisconnect(0x00), written);
        Assert.IsTrue(plan.Points.All(point => point.Account is null));
    }

    [TestMethod]
    public async Task ServeAsync_RefusedLogin_AsksThePlanNothing()
    {
        var plan = new ScriptedFaultPlan(Rule(command: "CONNECT"));
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials);

        var (written, _) = await ServeAsync(TestMessageBus.New(), plan, new RecordingSurlEventPublisher(), LoginAs("ann"), [CurlSubscribeToAB], policy);

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x04 }, written);
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task ServeAsync_CurlPublishUnderARule_ClosesWithNothingPublished()
    {
        var bus = TestMessageBus.New();
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(bus, new ScriptedFaultPlan(Rule(command: "PUBLISH", path: "a/b")), events, LoginAs("ann"), CurlPublishHiToAB, ClientPackets.PingRequest);

        // R14: a QoS 0 publish waits for nothing, so CONNACK is all upstream curl sees (exit 0);
        // the PINGREQ after it is never answered because the connection closed.
        CollectionAssert.AreEqual(ConnackAccepted, written);
        Assert.IsEmpty(bus.MatchingAny(["#"]));
        AssertOneFaultApplied(events, "PUBLISH a/b");
    }

    [TestMethod]
    public async Task ServeAsync_Qos1PublishUnderARule_ClosesWithNoPubAck()
    {
        var bus = TestMessageBus.New();

        var (written, _) = await ServeAsync(bus, new ScriptedFaultPlan(Rule(command: "PUBLISH")), new RecordingSurlEventPublisher(), LoginAs("ann"), ClientPackets.Publish(0x32, "a/b", 1, "hi"));

        CollectionAssert.AreEqual(ConnackAccepted, written);
        Assert.IsEmpty(bus.MatchingAny(["#"]));
    }

    [TestMethod]
    public async Task ServeAsync_PublishOutsideTheRulesTopic_IsRetained()
    {
        var bus = TestMessageBus.New();
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(bus, new ScriptedFaultPlan(Rule(command: "PUBLISH", path: "x/y")), events, LoginAs("ann"), CurlPublishHiToAB, ClientPackets.Disconnect);

        CollectionAssert.AreEqual(ConnackAccepted, written);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(bus.MatchingAny(["a/b"]).Single().Value));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow("127")]
    [DataRow("256")]
    [DataRow("135 not authorized")]
    [DataRow(null)]
    public async Task ServeAsync_ARuleWhoseReplyIsNotAReasonCode_IsNotedAndNotApplied(string? reply)
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(Rule(command: "SUBSCRIBE") with { Reply = reply });

        var (written, log) = await ServeAsync(TestMessageBus.New(), plan, events, LoginAs("ann"), CurlSubscribeToAB);

        CollectionAssert.AreEqual(SubAckThenDisconnect(0x00), written);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "SUBSCRIBE a/b: fault rule f1 is not an error with an MQTT reason code 128 to 255; not applied.");
    }

    [TestMethod]
    public async Task ServeAsync_AnOutOfSpaceRuleAtTheCommandStage_IsNotedAndNotApplied()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Command = "CONNECT" });

        var (written, log) = await ServeAsync(TestMessageBus.New(), plan, events, LoginAs("ann"), ClientPackets.Disconnect);

        CollectionAssert.AreEqual(ConnackAccepted, written);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "CONNECT: fault rule f1 is not an error with an MQTT reason code 128 to 255; not applied.");
    }

    // ADR-0141: a command-stage delay holds the SUBACK on the exchange's clock, with the idle
    // clock paused for the whole hold, then answers the SUBSCRIBE as usual.
    [TestMethod]
    public async Task ServeAsync_SubscribeDelayRule_HoldsTheSubAckUntilTheClockAdvances500Ms_WithTheIdleClockPaused()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Command = "SUBSCRIBE", DelayMilliseconds = 500 };
        var clock = new ManualTimeProvider();
        var idleClock = new RecordingIdleClock();
        var (serving, connection, events) = StartServing(TestMessageBus.New(), new ScriptedFaultPlan(delay), clock, idleClock, LoginAs("ann"), CurlSubscribeToAB);

        await idleClock.FirstPause.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var writtenAtTheHold = connection.WrittenBytes;
        clock.Advance(TimeSpan.FromMilliseconds(499));
        var writtenAfter499Ms = connection.WrittenBytes;
        var pausedAfter499Ms = idleClock.IsPaused;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(ConnackAccepted, writtenAtTheHold);
        CollectionAssert.AreEqual(ConnackAccepted, writtenAfter499Ms);
        Assert.IsTrue(pausedAfter499Ms);
        Assert.IsFalse(idleClock.IsPaused);
        CollectionAssert.AreEqual(SubAckThenDisconnect(0x00), connection.WrittenBytes);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "SUBSCRIBE a/b"), events.Events.Single());
    }

    // ADR-0141: a command-stage drop aborts the connection in place of anything the PUBLISH
    // would have caused, and nothing is published.
    [TestMethod]
    public async Task ServeAsync_PublishDropRule_AbortsTheConnectionWithNothingPublished()
    {
        var bus = TestMessageBus.New();
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "PUBLISH" };
        var (serving, connection, events) = StartServing(
            bus, new ScriptedFaultPlan(drop), new ManualTimeProvider(), new RecordingIdleClock(), LoginAs("ann"), ClientPackets.Publish(0x32, "a/b", 1, "hi"), ClientPackets.PingRequest);

        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(connection.Aborted);
        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        Assert.IsEmpty(bus.MatchingAny(["#"]));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "PUBLISH a/b"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ConnectDropRule_AbortsTheConnectionWithNoConnAck()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "CONNECT" };
        var (serving, connection, events) = StartServing(
            TestMessageBus.New(), new ScriptedFaultPlan(drop), new ManualTimeProvider(), new RecordingIdleClock(), LoginAs("ann"), CurlSubscribeToAB);

        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(connection.Aborted);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "CONNECT"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeDropRuleOnTheFirstFilter_AbortsWithNoSubAckAndAsksNoFurtherFilter()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "SUBSCRIBE", Path = "a/b" });
        var (serving, connection, events) = StartServing(
            TestMessageBus.New(), plan, new ManualTimeProvider(), new RecordingIdleClock(), LoginAs("ann"), ClientPackets.Subscribe(1, ("a/b", 0), ("c/d", 0)));

        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(connection.Aborted);
        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "CONNECT", "SUBSCRIBE" }, plan.Points.Select(point => point.Command).ToArray());
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "SUBSCRIBE a/b"), events.Events.Single());
    }

    private (Task Serving, InMemoryConnection Connection, RecordingSurlEventPublisher Events) StartServing(
        MessageBus.MessageBus bus, IFaultPlan plan, ManualTimeProvider clock, RecordingIdleClock idleClock, byte[] connect, params byte[][] packets)
    {
        var events = new RecordingSurlEventPublisher();
        var server = new MqttProtocolServer(bus, new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted));
        var connection = new InMemoryConnection(RecordedFixture.Whole(ClientPackets.Join([connect, .. packets])));
        var context = MqttTestExchange.Context(clock, TestContext.CancellationToken) with { Faults = plan, Events = events, IdleClock = idleClock };

        return (server.ServeAsync(connection, context), connection, events);
    }

    // CONNACK 0, a SUBACK for packet identifier 1 with one return code, then DISCONNECT.
    private static byte[] SubAckThenDisconnect(byte returnCode) =>
        [.. ConnackAccepted, 0x90, 0x03, 0x00, 0x01, returnCode, .. Disconnect];

    private static FaultRule Rule(string command, string? path = null) =>
        new("f1", FaultKind.Error) { Scheme = "mqtt", Command = command, Path = path, Reply = "135" };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.OfType<FaultAppliedEvent>().Single());

    // The CONNECT upstream curl sends with -u <user>:pw: user name and password flags, clean session.
    private static byte[] LoginAs(string userName) =>
        ClientPackets.ConnectWithFields(0xC2, "curlTESTTEST", ClientPackets.String(userName), ClientPackets.String("pw"));

    private Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(
        MessageBus.MessageBus bus, IFaultPlan plan, ISurlEventPublisher events, byte[] connect, params byte[][] packets) =>
        ServeAsync(bus, plan, events, connect, packets, new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted));

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(
        MessageBus.MessageBus bus, IFaultPlan plan, ISurlEventPublisher events, byte[] connect, byte[][] packets, IAuthenticationPolicy policy)
    {
        var server = new MqttProtocolServer(bus, policy);
        var connection = new InMemoryConnection(RecordedFixture.Whole(ClientPackets.Join([connect, .. packets])));
        var log = new RecordingExchangeLog();
        var context = MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken, log: log) with { Faults = plan, Events = events };

        await server.ServeAsync(connection, context);
        await connection.DisposeAsync();

        return (connection.WrittenBytes, log);
    }
}
