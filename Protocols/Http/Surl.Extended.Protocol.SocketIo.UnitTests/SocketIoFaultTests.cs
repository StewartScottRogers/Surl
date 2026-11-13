using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Fault rules at a namespace connect and at an event (ADR-0124 decisions 4 and 9; BL-1064).
/// </summary>
[TestClass]
public sealed class SocketIoFaultTests
{
    private const string TokenPayload = "{\"token\":\"" + UnitTestPasswordPolicy.Token + "\"}";

    private static FaultRule Rule(FaultKind kind, string? reply = null, string? command = "connect", string? path = "/") =>
        new("f1", kind) { Scheme = "socketio", Command = command, Path = path, Reply = reply, DelayMilliseconds = kind == FaultKind.Delay ? 1 : null };

    private static async Task<(SocketIoClient Client, ScriptedFaultPlan Faults, RecordingSurlEventPublisher Events, SocketIoServerHarness Harness)> OpenAsync(
        FaultRule rule, bool connect = true)
    {
        var harness = new SocketIoServerHarness();
        var faults = new ScriptedFaultPlan(rule);
        var events = new RecordingSurlEventPublisher();
        var context = harness.Context(faults: faults, events: events);
        var client = connect
            ? await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload, context)
            : await SocketIoClient.ConnectAsync(harness, context);

        return (client, faults, events, harness);
    }

    [TestMethod]
    public async Task Connect_UnderAnErrorRule_IsAnsweredWithTheChosenConnectErrorMessage()
    {
        var (client, faults, events, _) = await OpenAsync(Rule(FaultKind.Error, "Try \"later\""));

        Assert.AreEqual("44{\"message\":\"Try \\u0022later\\u0022\"}", client.Messages()[1]);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/"), events.Events.Single());
        var point = faults.Points.First();
        Assert.AreEqual("socketio", point.Scheme);
        Assert.AreEqual("connect", point.Command);
        Assert.AreEqual("/", point.Path);
        Assert.AreEqual(string.Empty, point.Account);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Connect_TheRuleDoesNotName_IsServed()
    {
        var (client, _, events, _) = await OpenAsync(Rule(FaultKind.Error, "no", path: "/other"));

        StringAssert.StartsWith(client.Messages()[1], "40{\"sid\":");
        Assert.IsEmpty(events.Events);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Connect_UnderADropRule_EndsTheSession()
    {
        var (client, _, events, _) = await OpenAsync(Rule(FaultKind.Drop), connect: false);

        client.SendText("40" + TokenPayload);
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(FaultKind.Drop, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
        Assert.HasCount(1, client.Messages());
    }

    [TestMethod]
    public async Task Connect_UnderADelayRule_IsServedAfterTheHold()
    {
        var harness = new SocketIoServerHarness();
        var events = new RecordingSurlEventPublisher();
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(faults: new ScriptedFaultPlan(Rule(FaultKind.Delay)), events: events));

        client.SendText("40" + TokenPayload);
        var waited = DateTime.UtcNow;
        while (events.Events.Count == 0 && DateTime.UtcNow - waited < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(10);
        }

        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        StringAssert.StartsWith((await client.WaitForMessagesAsync(2))[1], "40{\"sid\":");
        Assert.AreEqual(FaultKind.Delay, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow(FaultKind.Error, null)]
    [DataRow(FaultKind.RefuseLogin, null)]
    public async Task Connect_UnderARuleThatDoesNotFit_IsServedAndNoted(FaultKind kind, string? reply)
    {
        var (client, _, events, harness) = await OpenAsync(Rule(kind, reply));

        StringAssert.StartsWith(client.Messages()[1], "40{\"sid\":");
        Assert.IsEmpty(events.Events);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("Fault rule f1 not applied to connect on /: ", StringComparison.Ordinal)));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_WithAnAckIdUnderAnErrorRule_IsAnsweredWithAnErrorAck()
    {
        var (client, faults, events, _) = await OpenAsync(Rule(FaultKind.Error, "boom", "hello"), connect: false);
        client.SendText("40" + TokenPayload);
        await client.WaitForMessagesAsync(2);

        client.SendText("427[\"hello\",1]");

        Assert.AreEqual("437[{\"error\":\"boom\"}]", (await client.WaitForMessagesAsync(3))[2]);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/"), events.Events.Single());
        Assert.AreEqual("hello", faults.Points.Last().Command);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_WithoutAnAckIdUnderAnErrorRule_IsSwallowedAndAnotherEventIsServed()
    {
        var (client, _, events, _) = await OpenAsync(Rule(FaultKind.Error, "boom", "hello"));

        client.SendText("42[\"hello\"]");
        client.SendText("42[\"other\"]");

        Assert.AreEqual("42[\"other\"]", (await client.WaitForMessagesAsync(3))[2]);
        Assert.HasCount(1, events.Events);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_UnderADropRule_EndsTheSession()
    {
        var (client, _, events, _) = await OpenAsync(Rule(FaultKind.Drop, command: "hello"));

        client.SendText("42[\"hello\"]");
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(FaultKind.Drop, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
    }

    [TestMethod]
    public void ErrorArguments_TextWithQuotes_IsEscaped() =>
        Assert.AreEqual("[{\"error\":\"a\\u0022b\"}]", System.Text.Encoding.UTF8.GetString(SocketIoFaults.ErrorArguments("a\"b")));
}
