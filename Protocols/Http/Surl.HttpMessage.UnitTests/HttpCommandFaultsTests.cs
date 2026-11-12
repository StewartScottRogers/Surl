using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.HttpMessage;

[TestClass]
public sealed class HttpCommandFaultsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FindAndApplyAsync_NoRuleMatches_ReturnsNullAndAsksOneCommandPoint()
    {
        var plan = new ScriptedFaultPlan();

        var rule = await HttpCommandFaults.FindAndApplyAsync(Context(plan, new ListenUrl("http", "127.0.0.1", 0).WithBoundPort(8080)), "GET", "/a", "");

        Assert.IsNull(rule);
        Assert.AreEqual(new FaultPoint(FaultStage.Command) { ExchangeId = 7, ListenUrl = "http://127.0.0.1:8080/", Scheme = "http", Path = "/a", Command = "GET" }, plan.Points.Single());
    }

    [TestMethod]
    public async Task FindAndApplyAsync_AnErrorRuleMatches_ReturnsItWithTheAccountAsked()
    {
        var error = new FaultRule("f2", FaultKind.Error) { Reply = "503" };
        var plan = new ScriptedFaultPlan(error);

        var rule = await HttpCommandFaults.FindAndApplyAsync(Context(plan, new ListenUrl("rtsp", "fe80::1%eth0", 554)), "DESCRIBE", null, "alice");

        Assert.AreSame(error, rule);
        Assert.AreEqual("alice", plan.Points.Single().Account);
        Assert.AreEqual("rtsp://[fe80::1%25eth0]:554/", plan.Points.Single().ListenUrl);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ARuleOfAnotherKind_IsNotAppliedAndIsNoted()
    {
        var log = new RecordingExchangeLog();
        var plan = new ScriptedFaultPlan(new FaultRule("f3", FaultKind.OutOfSpace) { Stage = FaultStage.Command });

        var rule = await HttpCommandFaults.FindAndApplyAsync(Context(plan, log: log), "GET", "/a", null);

        Assert.IsNull(rule);
        Assert.AreEqual("GET /a: fault rule f3 asks for OutOfSpace at the command stage, which this server does not apply; answered as without it.", log.Notes.Single());
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ADropRuleMatches_IsPublishedAndHandedBackForTheServerToEnd()
    {
        var drop = new FaultRule("f4", FaultKind.Drop) { Stage = FaultStage.Command };
        var events = new RecordingSurlEventPublisher();

        var rule = await HttpCommandFaults.FindAndApplyAsync(Context(new ScriptedFaultPlan(drop), events: events), "GET", "/a", null);

        Assert.AreSame(drop, rule);
        Assert.AreEqual(new FaultAppliedEvent("f4", FaultKind.Drop, FaultStage.Command, 7, "/a"), events.Events.Single());
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ADelayRuleMatches_HoldsTheRequestAndReturnsNull()
    {
        var delay = new FaultRule("f5", FaultKind.Delay) { Stage = FaultStage.Command };
        var events = new RecordingSurlEventPublisher();

        var rule = await HttpCommandFaults.FindAndApplyAsync(Context(new ScriptedFaultPlan(delay), events: events), "GET", null, null);

        Assert.IsNull(rule);
        Assert.AreEqual(new FaultAppliedEvent("f5", FaultKind.Delay, FaultStage.Command, 7, "GET"), events.Events.Single());
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_ErrorRuleOrNone_IsReturned()
    {
        var error = new FaultRule("f6", FaultKind.Error) { Reply = "503" };

        Assert.AreSame(error, await HttpCommandFaults.FindChosenErrorAsync(Context(new ScriptedFaultPlan(error)), "GET", "/a", null));
        Assert.IsNull(await HttpCommandFaults.FindChosenErrorAsync(Context(new ScriptedFaultPlan()), "GET", "/a", null));
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_DelayOrDropRule_IsNotAppliedAndIsNoted()
    {
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f7", FaultKind.Drop) { Stage = FaultStage.Command });

        var rule = await HttpCommandFaults.FindChosenErrorAsync(Context(plan, log: log, events: events), "GET", "/a", null);

        Assert.IsNull(rule);
        Assert.IsEmpty(events.Events);
        Assert.AreEqual("GET /a: fault rule f7 asks for Drop at the command stage, which this server does not apply; answered as without it.", log.Notes.Single());
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_NoContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await HttpCommandFaults.FindChosenErrorAsync(null!, "GET", "/a", null));

    [TestMethod]
    public async Task FindAndApplyAsync_NoContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await HttpCommandFaults.FindAndApplyAsync(null!, "GET", "/a", null));

    [TestMethod]
    [DataRow("503", 503, null)]
    [DataRow("400 Bad Request", 400, "Bad Request")]
    [DataRow("599 try\tlater ~", 599, "try\tlater ~")]
    public void TryReadChosenStatus_AFittingReply_ReadsItsCodeAndReasonPhrase(string reply, int code, string? reasonPhrase)
    {
        Assert.IsTrue(HttpCommandFaults.TryReadChosenStatus(reply, out var readCode, out var readReasonPhrase));

        Assert.AreEqual(code, readCode);
        Assert.AreEqual(reasonPhrase, readReasonPhrase);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("399")]
    [DataRow("600 Too High")]
    [DataRow("503 ")]
    [DataRow("503 café")]
    [DataRow("503 a\u007fb")]
    [DataRow("503 a\u0001b")]
    [DataRow(" 503")]
    [DataRow("50x")]
    [DataRow("0000000503")]
    public void TryReadChosenStatus_AReplyThatDoesNotFit_IsFalse(string? reply) =>
        Assert.IsFalse(HttpCommandFaults.TryReadChosenStatus(reply, out _, out _));

    [TestMethod]
    [DataRow("1013 try later", 1013, "try later")]
    [DataRow("1000", 1000, null)]
    [DataRow("000000001 ", 1, "")]
    public void TrySplitCode_AReplyStartingWithDigits_SplitsAtTheFirstSpace(string reply, int code, string? text)
    {
        Assert.IsTrue(HttpCommandFaults.TrySplitCode(reply, out var readCode, out var readText));

        Assert.AreEqual(code, readCode);
        Assert.AreEqual(text, readText);
    }

    [TestMethod]
    public void PublishApplied_WithAPath_PublishesThePathAsTheTargetAndNotesTheAnswer()
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();

        HttpCommandFaults.PublishApplied(Context(new ScriptedFaultPlan(), events: events, log: log), new FaultRule("f1", FaultKind.Error), "GET", "/a", "503 Service Unavailable");

        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 7, "/a"), events.Events.Single());
        Assert.AreEqual("GET /a: answered 503 Service Unavailable by fault rule f1.", log.Notes.Single());
    }

    [TestMethod]
    public void PublishApplied_WithNoPath_PublishesTheMethodAsTheTarget()
    {
        var events = new RecordingSurlEventPublisher();

        HttpCommandFaults.PublishApplied(Context(new ScriptedFaultPlan(), events: events), new FaultRule("f1", FaultKind.Error), "OPTIONS", null, "503");

        Assert.AreEqual("OPTIONS", ((FaultAppliedEvent)events.Events.Single()).Target);
    }

    [TestMethod]
    public void PublishApplied_NoContextOrRule_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpCommandFaults.PublishApplied(null!, new FaultRule("f1", FaultKind.Error), "GET", null, "503"));
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpCommandFaults.PublishApplied(Context(new ScriptedFaultPlan()), null!, "GET", null, "503"));
    }

    private ExchangeContext Context(IFaultPlan plan, ListenUrl? listenUrl = null, RecordingSurlEventPublisher? events = null, RecordingExchangeLog? log = null) => new(
        7,
        listenUrl ?? new ListenUrl("http", "127.0.0.1", 80),
        new IPEndPoint(IPAddress.Loopback, 80),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log ?? new RecordingExchangeLog(),
        TimeProvider.System,
        TestContext.CancellationToken)
    {
        Faults = plan,
        Events = events ?? new RecordingSurlEventPublisher(),
    };
}
