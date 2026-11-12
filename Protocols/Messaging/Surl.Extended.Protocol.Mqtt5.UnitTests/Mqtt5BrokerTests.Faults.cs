using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9, ADR-0142): a matching
/// <see cref="FaultKind.Error"/> rule's reason code goes in a <c>SUBSCRIBE</c> filter's <c>SUBACK</c>
/// slot, a QoS 1 <c>PUBLISH</c>'s <c>PUBACK</c>, a QoS 2 one's <c>PUBREC</c>, or a <c>DISCONNECT</c>
/// after a QoS 0 one, with nothing published or subscribed, and one <see cref="FaultAppliedEvent"/> is
/// published. Every byte is written from MQTT 5.0.
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    [TestMethod]
    public async Task ServeAsync_SubscribeToABAndCDUnderARuleOnAB_AnswersTheChosenCodeForABAndGrantsCD()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(FaultRule("SUBSCRIBE", "a/b", "135"));
        var broker = Broker(policy: new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted));

        var connection = await ServeAsync(broker, FaultContext(plan, events), ConnectAsAlice, Subscribe(1, ("a/b", 0), ("c/d", 1)), DisconnectNormally);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x01, 0x00, 0x87, 0x01 }, AfterConnackSuccess(connection.WrittenBytes));
        AssertOneFaultApplied(events, "SUBSCRIBE a/b");
        var point = plan.Points.First();
        Assert.AreEqual(FaultStage.Command, point.Stage);
        Assert.AreEqual(1L, point.ExchangeId);
        Assert.AreEqual("mqtt5://127.0.0.1:1883/", point.ListenUrl);
        Assert.AreEqual("mqtt5", point.Scheme);
        Assert.AreEqual("alice", point.Account);
        CollectionAssert.AreEqual(new[] { "a/b", "c/d" }, plan.Points.Select(asked => asked.Path).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeUnderARuleWhoseCodeSubackCannotCarry_AnswersUnspecifiedError()
    {
        var connection = await ServeAsync(
            Broker(), FaultContext(new ScriptedFaultPlan(FaultRule("SUBSCRIBE", "a/b", "136")), new RecordingSurlEventPublisher()), Connect(), Subscribe(1, ("a/b", 0)), DisconnectNormally);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x80 }, AfterConnackSuccess(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow(1, (byte)0x40, DisplayName = "QoS 1 PUBACK")]
    [DataRow(2, (byte)0x50, DisplayName = "QoS 2 PUBREC")]
    public async Task ServeAsync_PublishUnderARule_AcknowledgesWithTheChosenCodeAndPublishesNothing(int qualityOfService, byte responseType)
    {
        var state = NewState();
        var published = PublishedOn(state);
        var events = new RecordingSurlEventPublisher();

        var connection = await ServeAsync(
            Broker(state), FaultContext(new ScriptedFaultPlan(FaultRule("PUBLISH", "a/b", "151")), events), Connect(), Publish("a/b", "hi", qualityOfService, 7), PingRequest, DisconnectNormally);

        CollectionAssert.AreEqual(new byte[] { responseType, 0x03, 0x00, 0x07, 0x97, 0xD0, 0x00 }, AfterConnackSuccess(connection.WrittenBytes));
        Assert.IsEmpty(published);
        AssertOneFaultApplied(events, "PUBLISH a/b");
    }

    [TestMethod]
    public async Task ServeAsync_PublishUnderARuleWhoseCodePubackCannotCarry_AcknowledgesUnspecifiedError()
    {
        var connection = await ServeAsync(
            Broker(), FaultContext(new ScriptedFaultPlan(FaultRule("PUBLISH", "a/b", "136")), new RecordingSurlEventPublisher()), Connect(), Publish("a/b", "hi", 1, 7), DisconnectNormally);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x07, 0x80 }, AfterConnackSuccess(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("151", (byte)0x97, DisplayName = "A DISCONNECT code")]
    [DataRow("135", (byte)0x87, DisplayName = "Not authorized")]
    [DataRow("132", (byte)0x80, DisplayName = "A CONNACK-only code")]
    public async Task ServeAsync_QualityOfServiceZeroPublishUnderARule_DisconnectsWithTheChosenCode(string reply, byte reasonCode)
    {
        var state = NewState();
        var published = PublishedOn(state);
        var events = new RecordingSurlEventPublisher();

        var connection = await ServeAsync(
            Broker(state), FaultContext(new ScriptedFaultPlan(FaultRule("PUBLISH", "a/b", reply)), events), Connect(), Publish("a/b", "hi", 0, 0), PingRequest);

        CollectionAssert.AreEqual(Disconnect(reasonCode), AfterConnackSuccess(connection.WrittenBytes));
        Assert.IsEmpty(published);
        AssertOneFaultApplied(events, "PUBLISH a/b");
    }

    [TestMethod]
    public async Task ServeAsync_PublishOutsideTheRulesTopic_IsPublished()
    {
        var state = NewState();
        var published = PublishedOn(state);
        var events = new RecordingSurlEventPublisher();

        var connection = await ServeAsync(
            Broker(state), FaultContext(new ScriptedFaultPlan(FaultRule("PUBLISH", "x/y", "151")), events), Connect(), Publish("a/b", "hi", 1, 7), DisconnectNormally);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x07 }, AfterConnackSuccess(connection.WrittenBytes));
        Assert.HasCount(1, published);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow("127")]
    [DataRow("256")]
    [DataRow("135 no")]
    [DataRow(null)]
    public async Task ServeAsync_ARuleWhoseReplyIsNotAReasonCode_IsNotedAndNotApplied(string? reply)
    {
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log) with
        {
            Faults = new ScriptedFaultPlan(FaultRule("SUBSCRIBE", "a/b", reply)),
            Events = events,
        };

        var connection = await ServeAsync(Broker(), context, Connect(), Subscribe(1, ("a/b", 0)), DisconnectNormally);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, AfterConnackSuccess(connection.WrittenBytes));
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "SUBSCRIBE a/b: fault rule f1 is not an error with an MQTT reason code 128 to 255; not applied.");
    }

    [TestMethod]
    public async Task ServeAsync_ADelayRuleAtTheCommandStage_IsNotApplied()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Command = "PUBLISH", DelayMilliseconds = 10 });

        var connection = await ServeAsync(Broker(), FaultContext(plan, events), Connect(), Publish("a/b", "hi", 1, 7), DisconnectNormally);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x07, 0x10 }, AfterConnackSuccess(connection.WrittenBytes));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow((byte)0x87, (byte)0x87)]
    [DataRow((byte)0x84, (byte)0x80)]
    public void ReasonCodeIn_ACodeThePacketMayOrMayNotCarry_IsItselfOrUnspecifiedError(byte chosen, byte expected) =>
        Assert.AreEqual((Mqtt5ReasonCode)expected, Mqtt5BrokerConnection.ReasonCodeIn(new HashSet<byte> { 0x80, 0x87 }, chosen));

    private static FaultRule FaultRule(string command, string path, string? reply) =>
        new("f1", FaultKind.Error) { Scheme = "mqtt5", Command = command, Path = path, Reply = reply };

    private ExchangeContext FaultContext(IFaultPlan plan, ISurlEventPublisher events) =>
        Context(new ManualTimeProvider(), TestContext.CancellationToken) with { Faults = plan, Events = events };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.OfType<FaultAppliedEvent>().Single());
}
