using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9; BL-1065): a CONNECT an <see cref="FaultKind.Error"/>
/// rule matches is answered with the rule's status and text in place of the session, and one
/// <see cref="FaultAppliedEvent"/> is published.
/// </summary>
[TestClass]
public sealed class WebTransportConnectFaultTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    private static FaultRule Rule(FaultKind kind = FaultKind.Error, string? path = "/echo", string? reply = "503 try later") =>
        new("f1", kind) { Scheme = "webtransport", Command = "CONNECT", Path = path, Reply = reply, DelayMilliseconds = kind == FaultKind.Delay ? 1 : null };

    private async Task<(Http3TestResponse Response, RecordingSurlEventPublisher Events, RecordingExchangeLog Log, ScriptedFaultPlan Faults)> ConnectAsync(FaultRule rule)
    {
        using var client = new Http3TestQuicClient();
        var events = new RecordingSurlEventPublisher();
        var faults = new ScriptedFaultPlan(rule);
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token, faults: faults, events: events);
        var response = await UnitTestWebTransport.ConnectAsync(client, Token);
        await client.UntilAsync(() => client.Response(0).Fin || response.Status == "200", Token);

        return (client.Response(0), events, log, faults);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectUnderAnErrorRule_GetsTheChosenStatusAndText()
    {
        var (response, events, log, faults) = await ConnectAsync(Rule());

        Assert.AreEqual("503", response.Status);
        Assert.AreEqual("try later", System.Text.Encoding.UTF8.GetString(response.Body.ToArray()));
        Assert.IsTrue(response.Fin);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/echo"), events.Events.Single());
        Assert.AreEqual("CONNECT", faults.Points.Single().Command);
        Assert.AreEqual("webtransport", faults.Points.Single().Scheme);
        await UnitTestWebTransport.WaitForNoteAsync(log, "WebTransport CONNECT to /echo was answered 503 by fault rule f1.", Token);
    }

    private async Task<(Http3TestResponse Response, ScriptedFaultPlan Faults)> ConnectLoggedInAsync(FaultRule rule, bool allowAnonymous, params (string Name, string Value)[] login)
    {
        using var client = new Http3TestQuicClient();
        var faults = new ScriptedFaultPlan(rule);
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: new UnitTestAuthenticationPolicy(allowAnonymous), faults: faults);
        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo", login);
        await client.UntilAsync(() => client.Response(0).Fin || response.Status == "200", Token);

        return (client.Response(0), faults);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectLoggedInAsTheRulesAccount_GetsTheChosenStatusAndNamesTheAccount()
    {
        var rule = Rule(reply: "503") with { Account = "user" };

        var (response, faults) = await ConnectLoggedInAsync(rule, false, ("authorization", UnitTestAuthenticationPolicy.GoodAuthorization));

        Assert.AreEqual("503", response.Status);
        Assert.AreEqual("user", faults.Points.Single().Account);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectLoggedInAsAnotherAccount_IsServed()
    {
        var rule = Rule(reply: "503") with { Account = "alice" };

        var (response, _) = await ConnectLoggedInAsync(rule, false, ("authorization", UnitTestAuthenticationPolicy.GoodAuthorization));

        Assert.AreEqual("200", response.Status);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectWithNoLogin_LeavesThePointsAccountNull()
    {
        var (response, faults) = await ConnectLoggedInAsync(Rule(path: "/other"), true);

        Assert.AreEqual("200", response.Status);
        Assert.IsNull(faults.Points.Single().Account);
    }

    [TestMethod]
    [DataRow("404", DisplayName = "No space")]
    [DataRow("404 ", DisplayName = "A space and no text")]
    public async Task ServeSessionAsync_ConnectUnderAStatusOnlyRule_GetsAnEmptyBody(string reply)
    {
        var (response, _, _, _) = await ConnectAsync(Rule(reply: reply));

        Assert.AreEqual("404", response.Status);
        Assert.IsEmpty(response.Body);
        Assert.IsTrue(response.Fin);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectTheRuleDoesNotName_IsServed()
    {
        var (response, events, _, _) = await ConnectAsync(Rule(path: "/other"));

        Assert.AreEqual("200", response.Status);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow(FaultKind.Error, "200 ok", "its reply is not a status 400 to 599", DisplayName = "Status out of range")]
    [DataRow(FaultKind.Error, "boom", "its reply is not a status 400 to 599", DisplayName = "Not a status")]
    [DataRow(FaultKind.Error, null, "its reply is not a status 400 to 599", DisplayName = "No reply")]
    [DataRow(FaultKind.RefuseLogin, null, "a RefuseLogin rule is not applied at a CONNECT", DisplayName = "Another kind")]
    public async Task ServeSessionAsync_RuleItCannotApply_IsNotedAndTheSessionIsServed(FaultKind kind, string? reply, string why)
    {
        var (response, events, log, _) = await ConnectAsync(Rule(kind, reply: reply));

        Assert.AreEqual("200", response.Status);
        Assert.IsEmpty(events.Events);
        await UnitTestWebTransport.WaitForNoteAsync(log, $"Fault rule f1 not applied to CONNECT /echo: {why}.", Token);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectUnderADelayRule_IsHeldForTheDelayThenAnswered200()
    {
        using var client = new Http3TestQuicClient();
        var events = new RecordingSurlEventPublisher();
        var rule = new FaultRule("f1", FaultKind.Delay) { Scheme = "webtransport", Command = "CONNECT", Path = "/echo", DelayMilliseconds = 5000 };
        var (_, time) = await UnitTestWebTransport.StartAsync(client, Token, faults: new ScriptedFaultPlan(rule), events: events);

        client.Send(0, UnitTestWebTransport.Connect(client));
        await UnitTestWebTransport.AdvanceUntilAsync(client, time, () => client.StreamSent(0).Data.Length > 0, Token);

        Assert.AreEqual("200", client.Response(0).Status);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "/echo"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectUnderADelayRule_SendsNothingBeforeTheDelayPasses()
    {
        using var client = new Http3TestQuicClient();
        var rule = new FaultRule("f1", FaultKind.Delay) { Scheme = "webtransport", Command = "CONNECT", Path = "/echo", DelayMilliseconds = 5000 };
        var (_, time) = await UnitTestWebTransport.StartAsync(client, Token, faults: new ScriptedFaultPlan(rule));

        client.Send(0, UnitTestWebTransport.Connect(client));
        await UnitTestWebTransport.AdvanceUntilAsync(client, time, () => time.GetUtcNow() >= new DateTimeOffset(2026, 11, 5, 12, 0, 4, TimeSpan.Zero), Token);

        Assert.IsEmpty(client.StreamSent(0).Data);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectUnderADropRule_ResetsTheStreamWithNoResponse()
    {
        using var client = new Http3TestQuicClient();
        var events = new RecordingSurlEventPublisher();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token, faults: new ScriptedFaultPlan(Rule(FaultKind.Drop)), events: events);

        client.Send(0, UnitTestWebTransport.Connect(client));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.IsEmpty(client.StreamSent(0).Data);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "/echo"), events.Events.Single());
        await UnitTestWebTransport.WaitForNoteAsync(log, "CONNECT dropped the connection, unanswered, by fault rule f1.", Token);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectWithNoRule_IsServed()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token);

        Assert.AreEqual("200", response.Status);
    }
}
