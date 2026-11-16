using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): a matching
/// <see cref="FaultKind.Error"/> rule answers a method naming an exchange or queue with its reply
/// code and text - a channel exception as <c>channel.close</c>, a connection exception as
/// <c>connection.close</c> - and publishes one <see cref="FaultAppliedEvent"/>. Every byte is written
/// from AMQP 0-9-1.
/// </summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    [TestMethod]
    public async Task QueueDeclare_UnderARuleOnItsQueue_ClosesTheChannelWithTheChosenExceptionAndAnotherQueueIsServed()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(FaultRule("queue.declare", "orders", "404 NOT_FOUND - chosen"));

        var (written, log) = await ServeFaultedAsync(plan, events, QueueDeclare(1, "audit"), QueueDeclare(1, "orders"), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "audit"), BrokerChannelClose(1, 404, "NOT_FOUND - chosen", 50, 10));
        AssertOneFaultApplied(events, "queue.declare orders");
        CollectionAssert.Contains(log.Notes.ToArray(), "queue.declare orders answered by fault rule f1 with reply code 404.");
        var point = plan.Points.Last();
        Assert.AreEqual(FaultStage.Command, point.Stage);
        Assert.AreEqual(1L, point.ExchangeId);
        Assert.AreEqual("amqp://127.0.0.1:5672/", point.ListenUrl);
        Assert.AreEqual("amqp", point.Scheme);
        Assert.AreEqual("alice", point.Account);
    }

    [TestMethod]
    public async Task Publish_ToTheDefaultExchangeUnderARuleOnItsQueue_ClosesTheChannelAndDiscardsTheContent()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(FaultRule("basic.publish", "orders", "406 PRECONDITION_FAILED - chosen"));

        var (written, _) = await ServeFaultedAsync(plan, events, QueueDeclare(1, "orders"), PublishText(1, string.Empty, "orders", "hi"), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "orders"), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - chosen", 60, 40));
        AssertOneFaultApplied(events, "basic.publish orders");
    }

    [TestMethod]
    public async Task Publish_ToANamedExchange_AsksWithTheExchangeAsItsPath()
    {
        var plan = new ScriptedFaultPlan();

        var (written, _) = await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), PublishText(1, "amq.direct", "orders", "hi"));

        AssertAnswered(written);
        Assert.AreEqual("amq.direct", plan.Points.Single().Path);
    }

    [TestMethod]
    public async Task Consume_UnderARuleWithAConnectionException_ClosesTheConnection()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(FaultRule("basic.consume", "orders", "541 INTERNAL_ERROR - chosen"));

        var (written, _) = await ServeFaultedAsync(plan, events, QueueDeclare(1, "orders"), Consume(1, "orders", "c1"));

        AssertAnswered(written, QueueDeclareOk(1, "orders"), BrokerClose(541, "INTERNAL_ERROR - chosen", 60, 20));
        AssertOneFaultApplied(events, "basic.consume orders");
    }

    [TestMethod]
    [DataRow("200 OK")]
    [DataRow("404")]
    [DataRow(null)]
    public async Task ExchangeDeclare_UnderARuleWhoseReplyIsNotAnException_IsNotedAndServed(string? reply)
    {
        var events = new RecordingSurlEventPublisher();

        var (written, log) = await ServeFaultedAsync(new ScriptedFaultPlan(FaultRule("exchange.declare", "x", reply)), events, ExchangeDeclare(1, "x", "direct"));

        AssertAnswered(written, ExchangeDeclareOk(1));
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>());
        CollectionAssert.Contains(log.Notes.ToArray(), "exchange.declare x: fault rule f1 is not an error with an AMQP 0-9-1 reply code and text; not applied.");
    }

    [TestMethod]
    public async Task QueueDeclare_UnderARuleOfAnotherKind_IsNotedAndServed()
    {
        var events = new RecordingSurlEventPublisher();
        var rule = new FaultRule("f1", FaultKind.Error) { Scheme = "amqp", Command = "queue.declare", Path = "orders", Reply = null };
        var kindless = new ScriptedFaultPlan(rule);

        var (written, _) = await ServeFaultedAsync(kindless, events, QueueDeclare(1, "orders"));

        AssertAnswered(written, QueueDeclareOk(1, "orders"));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task Publish_UnderADelayRule_HoldsTheMethodForTheDelayThenHandlesItAsUsual()
    {
        var events = new RecordingSurlEventPublisher();
        var idleClock = new RecordingIdleClock();
        var clock = new ManualTimeProvider();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Command = "basic.publish", DelayMilliseconds = 10 });
        byte[][] all = [ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), ChannelOpen(1), QueueDeclare(1, "orders"), PublishText(1, string.Empty, "orders", "hi"), Close()];
        var connection = new InMemoryConnection(all.Select(chunk => (ReadOnlyMemory<byte>)chunk));
        var context = new ExchangeContext(
            1,
            new ListenUrl("amqp", "127.0.0.1", 5672).WithBoundPort(5672),
            new IPEndPoint(IPAddress.Loopback, 5672),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            clock,
            TestContext.CancellationToken) with
        { Faults = plan, Events = events, IdleClock = idleClock };

        var serving = NewBroker(NewBus()).ServeAsync(connection, context);
        await idleClock.FirstPause.WaitAsync(TestContext.CancellationToken);
        Assert.IsTrue(idleClock.IsPaused);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        await serving;

        AssertAnswered(connection.WrittenBytes, QueueDeclareOk(1, "orders"));
        Assert.AreEqual(1, idleClock.RestartCount);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "basic.publish orders"), events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task QueueDeclare_UnderADropRule_AbortsTheConnectionWithNoQueueDeclareOk()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "queue.declare", Path = "orders" });
        byte[][] all = [ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), ChannelOpen(1), QueueDeclare(1, "orders"), Close()];
        var connection = new InMemoryConnection(all.Select(chunk => (ReadOnlyMemory<byte>)chunk));

        await NewBroker(NewBus()).ServeAsync(connection, Context(new RecordingExchangeLog()) with { Faults = plan, Events = events });

        CollectionAssert.AreEqual(Concat([Prefix()]), connection.WrittenBytes, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "queue.declare orders"), events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task QueueDeclare_UnderARuleForAnotherMethod_ChangesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "queue.delete" });

        var (written, _) = await ServeFaultedAsync(plan, events, QueueDeclare(1, "orders"));

        AssertAnswered(written, QueueDeclareOk(1, "orders"));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task MethodsNamingNoPathOrUnreadable_AskThePlanNothing()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "404 x" });

        var (written, _) = await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), Qos(1, 5), Method(1, 50, 10, 0));

        AssertAnswered(written, QosOk(1), BrokerClose(502, "SYNTAX_ERROR - malformed method", 50, 10));
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task AMethodWhileAPublishsContentIsPending_AsksThePlanNothing()
    {
        var plan = new ScriptedFaultPlan();

        var (written, _) = await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), Publish(1, "amq.direct", "k"), QueueDeclare(1, "orders"));

        AssertAnswered(written, BrokerClose(505, "UNEXPECTED_FRAME - method before the content was complete", 50, 10));
        Assert.AreEqual("basic.publish", plan.Points.Single().Command);
    }

    [TestMethod]
    [DataRow("311 x", true, (ushort)311, "x")]
    [DataRow("541 internal error", true, (ushort)541, "internal error")]
    [DataRow("200 OK", false, (ushort)200, "OK")]
    [DataRow("404 ", false, (ushort)404, "")]
    [DataRow("404", false, (ushort)0, "")]
    [DataRow(" 404 x", false, (ushort)0, "404 x")]
    [DataRow("x404 x", false, (ushort)0, "x")]
    [DataRow(null, false, (ushort)0, "")]
    public void TryReadException_Reply_IsAListedCodeAndText(string? reply, bool expected, ushort replyCode, string replyText)
    {
        var read = Amqp0BrokerConnection.TryReadException(reply, out var code, out var text);

        Assert.AreEqual(expected, read);
        Assert.AreEqual(replyCode, code);
        Assert.AreEqual(replyText, text);
    }

    [TestMethod]
    public void TryReadException_TextOver255Bytes_IsRefused() =>
        Assert.IsFalse(Amqp0BrokerConnection.TryReadException("404 " + new string('x', 256), out _, out _));

    private static FaultRule FaultRule(string command, string path, string? reply) =>
        new("f1", FaultKind.Error) { Scheme = "amqp", Command = command, Path = path, Reply = reply };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.OfType<FaultAppliedEvent>().Single());

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeFaultedAsync(IFaultPlan plan, ISurlEventPublisher events, params byte[][] chunks)
    {
        var log = new RecordingExchangeLog();
        byte[][] all = [ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), ChannelOpen(1), .. chunks, Close()];
        var connection = new InMemoryConnection(all.Select(chunk => (ReadOnlyMemory<byte>)chunk));

        await NewBroker(NewBus()).ServeAsync(connection, Context(log) with { Faults = plan, Events = events });

        return (connection.WrittenBytes, log);
    }
}
