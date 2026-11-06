using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The chosen-error fault (ADR-0124, decisions 4 and 9; ADR-0144; BL-835): an invocation an
/// <see cref="FaultKind.Error"/> rule matches is completed with the rule's text as the Completion's
/// <c>error</c> in place of the hub method, and one <see cref="FaultAppliedEvent"/> is published.
/// Records written by hand from HubProtocol.md's "Completion" examples.
/// </summary>
[TestClass]
public sealed class SignalRInvocationFaultsTests
{
    private static FaultRule ErrorRule(string? command = "Echo", string? reply = "try later") =>
        new("f1", FaultKind.Error) { Scheme = "signalr", Command = command, Reply = reply };

    private static async Task<(SignalRHubClient Client, RecordingSurlEventPublisher Events)> ConnectAsync(SignalRServerHarness harness, IFaultPlan faults)
    {
        var events = new RecordingSurlEventPublisher();
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context() with { Faults = faults, Events = events });

        return (client, events);
    }

    [TestMethod]
    public async Task RunAsync_InvocationUnderARule_IsCompletedWithTheChosenError()
    {
        var harness = new SignalRServerHarness();
        var faults = new ScriptedFaultPlan(ErrorRule());
        var (client, events) = await ConnectAsync(harness, faults);

        client.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[\"hi\"]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"1\",\"error\":\"try later\"}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/chat"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "signalr://127.0.0.1:5000/", Scheme = "signalr", Path = "/chat", Command = "Echo" },
            faults.Points.Single());
        Assert.Contains("The invocation was completed with the chosen error of fault rule f1.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task RunAsync_StreamInvocationUnderARule_IsCompletedWithTheChosenErrorAndNoItem()
    {
        var harness = new SignalRServerHarness();
        var (client, events) = await ConnectAsync(harness, new ScriptedFaultPlan(ErrorRule(command: "Count")));

        client.SendRecords("{\"type\":4,\"invocationId\":\"7\",\"target\":\"Count\",\"arguments\":[3]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"7\",\"error\":\"try later\"}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
        Assert.HasCount(2, client.Records());
        Assert.HasCount(1, events.Events);
    }

    [TestMethod]
    public async Task RunAsync_InvocationTheRuleDoesNotName_IsServed()
    {
        var harness = new SignalRServerHarness();
        var (client, events) = await ConnectAsync(harness, new ScriptedFaultPlan(ErrorRule(command: "Fail")));

        client.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[\"hi\"]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"1\",\"result\":\"hi\"}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task RunAsync_NonBlockingInvocation_AsksNoFaultPoint()
    {
        var harness = new SignalRServerHarness();
        var faults = new ScriptedFaultPlan(ErrorRule());
        var (client, events) = await ConnectAsync(harness, faults);

        client.SendRecords(
            "{\"type\":1,\"target\":\"Echo\",\"arguments\":[\"quiet\"]}",
            "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Fail\",\"arguments\":[]}");

        await client.WaitForRecordsAsync(2);
        await client.EndAsync();
        Assert.AreEqual("Fail", faults.Points.Single().Command);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    public async Task RunAsync_ReplyThatDoesNotFit_IsNotAppliedAndIsNoted(string? reply)
    {
        var harness = new SignalRServerHarness();
        var (client, events) = await ConnectAsync(harness, new ScriptedFaultPlan(ErrorRule(reply: reply)));

        client.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[\"hi\"]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"1\",\"result\":\"hi\"}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the invocation: its reply does not fit the signalr error shape.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task RunAsync_CommandStageDropRule_IsNotAppliedAndIsNoted()
    {
        var harness = new SignalRServerHarness();
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command };
        var (client, events) = await ConnectAsync(harness, new ScriptedFaultPlan(drop));

        client.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[\"hi\"]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"1\",\"result\":\"hi\"}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the invocation: a Drop rule is not applied at an invocation.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task FindErrorAsync_NamedAccount_IsThePointsAccount()
    {
        var harness = new SignalRServerHarness();
        var faults = new ScriptedFaultPlan(ErrorRule() with { Account = "alice" });
        var invocation = new HubInvocation(false, "1", "Echo", [], []);

        var error = await new SignalRInvocationFaults(harness.Context() with { Faults = faults }, "/chat", "alice").FindErrorAsync(invocation);

        Assert.AreEqual("try later", error);
        Assert.AreEqual("alice", faults.Points.Single().Account);
    }
}
