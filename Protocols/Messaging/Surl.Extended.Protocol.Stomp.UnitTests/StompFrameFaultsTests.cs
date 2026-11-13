using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9; BL-1062): a frame an <see cref="FaultKind.Error"/>
/// rule matches is answered ERROR with the rule's text as <c>message</c>, then the connection closes
/// as STOMP 1.2 says; frames are written by hand from the specification.
/// </summary>
[TestClass]
public sealed class StompFrameFaultsTests
{
    private const string Subscribe = "SUBSCRIBE\nid:0\ndestination:/topic/t\nreceipt:s\n\n\0";

    private static Surl.MessageBus.MessageBus NewBus() => new(TimeProvider.System);

    private static FaultRule Rule(FaultKind kind, string? reply = null, string? command = "SUBSCRIBE") =>
        new("f1", kind) { Scheme = "stomp", Command = command, Path = "/topic/t", Reply = reply, DelayMilliseconds = kind == FaultKind.Delay ? 1 : null };

    [TestMethod]
    public async Task Frame_UnderAnErrorRule_GetsTheChosenErrorAndTheConnectionCloses()
    {
        var faults = new ScriptedFaultPlan(Rule(FaultKind.Error, "Access refused"));
        var events = new RecordingSurlEventPublisher();
        var session = await StompTestSession.StartAsync(NewBus(), faults: faults, events: events);

        session.Send(Subscribe);
        await session.EndAsync();

        Assert.AreEqual("ERROR\nmessage:Access refused\nreceipt-id:s\n\n\0", session.Answers);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/topic/t"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "stomp://127.0.0.1:61613/", Scheme = "stomp", Account = "alice", Path = "/topic/t", Command = "SUBSCRIBE" },
            faults.Points.Single());
    }

    [TestMethod]
    public async Task Frame_TheRuleDoesNotName_IsServed()
    {
        var faults = new ScriptedFaultPlan(Rule(FaultKind.Error, "Access refused", command: "SEND"));
        var events = new RecordingSurlEventPublisher();
        var session = await StompTestSession.StartAsync(NewBus(), faults: faults, events: events);

        await session.SendAndWaitForReceiptAsync(Subscribe, "s");
        await session.CloseAsync();

        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task Frame_UnderADropRule_EndsTheConnectionAfterWhatWasQueued()
    {
        var events = new RecordingSurlEventPublisher();
        var session = await StompTestSession.StartAsync(
            NewBus(), faults: new ScriptedFaultPlan(Rule(FaultKind.Drop, command: "SEND")), events: events);
        await session.SendAndWaitForReceiptAsync(Subscribe, "s");

        session.Send("SEND\ndestination:/topic/t\nreceipt:x\n\nhi\0");
        await session.EndAsync();

        Assert.AreEqual("RECEIPT\nreceipt-id:s\n\n\0", session.Answers);
        Assert.AreEqual(FaultKind.Drop, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
    }

    [TestMethod]
    public async Task Frame_UnderADelayRule_IsServedAfterTheHold()
    {
        var events = new RecordingSurlEventPublisher();
        var session = await StompTestSession.StartAsync(
            NewBus(), faults: new ScriptedFaultPlan(Rule(FaultKind.Delay)), events: events);

        await session.SendAndWaitForReceiptAsync(Subscribe, "s");
        await session.CloseAsync();

        Assert.AreEqual(FaultKind.Delay, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
    }

    [TestMethod]
    [DataRow(FaultKind.Error, "a\nb", "its reply does not fit the stomp error shape")]
    [DataRow(FaultKind.Error, null, "its reply does not fit the stomp error shape")]
    [DataRow(FaultKind.RefuseLogin, null, "a RefuseLogin rule is not applied at a frame")]
    public async Task Frame_UnderARuleThatDoesNotFit_IsServedAndNoted(FaultKind kind, string? reply, string why)
    {
        var events = new RecordingSurlEventPublisher();
        var session = await StompTestSession.StartAsync(
            NewBus(), faults: new ScriptedFaultPlan(Rule(kind, reply)), events: events);

        await session.SendAndWaitForReceiptAsync(Subscribe, "s");
        await session.CloseAsync();

        Assert.IsEmpty(events.Events);
        Assert.Contains($"Fault rule f1 not applied to SUBSCRIBE: {why}.", session.Log.Notes);
    }

    [TestMethod]
    public async Task Frame_WithNoDestination_NamesItsCommandInTheEvent()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Command = "BEGIN", Reply = "no" };
        var events = new RecordingSurlEventPublisher();
        var session = await StompTestSession.StartAsync(NewBus(), faults: new ScriptedFaultPlan(rule), events: events);

        session.Send("BEGIN\ntransaction:t\n\n\0");
        await session.EndAsync();

        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "BEGIN"), events.Events.Single());
    }
}
