using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// A fault rule's chosen error (ADR-0124 decision 4, ADR-0134 decision 2), replaying the
/// <c>DESCRIBE</c> pinned upstream libcurl 8.21.0 sent in <c>Fixtures/libcurl-describe-fault-503</c>.
/// </summary>
[TestClass]
public sealed class RtspFaultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LibcurlDescribe_WithAChosenErrorRule_IsAnsweredTheChosenStatusAndPublishesOneEvent()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Scheme = "rtsp", Command = "DESCRIBE", Path = "/clip.bin", Reply = "503" };

        var (connection, log, events, plan) = await ServeWithFaultsAsync([RecordedRequest("libcurl-describe-fault-503")], new ScriptedFaultPlan(rule));

        Assert.AreEqual(Latin1(RecordedResponse("libcurl-describe-fault-503")), Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/clip.bin"), events.Events.OfType<FaultAppliedEvent>().Single());
        var point = plan.Points.Single();
        Assert.AreEqual("rtsp://127.0.0.1:18554/", point.ListenUrl);
        Assert.AreEqual("DESCRIBE", point.Command);
        Assert.Contains("DESCRIBE /clip.bin: answered 503 Service Unavailable by fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task LibcurlDescribe_WithARuleOnAnotherPath_IsServedAsWithoutIt()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Path = "/other", Reply = "503" };

        var (connection, _, events, _) = await ServeWithFaultsAsync([RecordedRequest("libcurl-describe")], new ScriptedFaultPlan(rule));

        Assert.AreEqual(Latin1(RecordedResponse("libcurl-describe")), Latin1(connection.WrittenBytes));
        Assert.IsEmpty(events.Events.ToList());
    }

    [TestMethod]
    [DataRow("453 Not Enough Bandwidth", "453 Not Enough Bandwidth")]
    [DataRow("453", "453 Not Enough Bandwidth")]
    [DataRow("599", "599 ")]
    [DataRow("500 try later", "500 try later")]
    public async Task OptionsStar_WithAChosenError_IsThatStatusWithNoPathAndKeepsTheConnection(string reply, string statusLine)
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Command = "OPTIONS", Reply = reply };
        var request = Ascii("OPTIONS * RTSP/1.0\r\nCSeq: 2\r\n\r\nOPTIONS * RTSP/1.0\r\nCSeq: 3\r\n\r\n");

        var (connection, _, _, plan) = await ServeWithFaultsAsync([request], new ScriptedFaultPlan(rule));

        var expected = ResponseHead(statusLine, "2") + ResponseHead(statusLine, "3");
        Assert.AreEqual(expected, Latin1(connection.WrittenBytes));
        Assert.IsNull(plan.Points[0].Path);
    }

    [TestMethod]
    [DataRow("399")]
    [DataRow("503 bad\u0001phrase")]
    [DataRow("503 ")]
    [DataRow("x503")]
    [DataRow(null)]
    public async Task ChosenErrorThatDoesNotFit_IsNotAppliedAndIsNoted(string? reply)
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Reply = reply };

        var (connection, log, events, _) = await ServeWithFaultsAsync([RecordedRequest("libcurl-describe")], new ScriptedFaultPlan(rule));

        Assert.AreEqual(Latin1(RecordedResponse("libcurl-describe")), Latin1(connection.WrittenBytes));
        Assert.IsEmpty(events.Events.ToList());
        Assert.Contains("RTSP DESCRIBE: fault rule f1's reply does not fit the RTSP error shape; answered as without it.", log.Notes);
    }

    [TestMethod]
    public async Task ChosenErrorScopedToAlice_FiresOnlyOnceAliceLoggedIn()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Account = "alice", Reply = "503" };
        var policy = new UnitTestAuthenticationPolicy(request => request.Fields.Any(field => field.Key == "Authorization")
            ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice")
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, ["Basic realm=\"surl\""], null));
        var request = Ascii(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n"
            + "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nAuthorization: Basic YWxpY2U6c2VjcmV0\r\n\r\n");

        var (connection, _, events, plan) = await ServeWithFaultsAsync([request], new ScriptedFaultPlan(rule), policy);

        var expected = ResponseHead("401 Unauthorized", "1", "WWW-Authenticate: Basic realm=\"surl\"")
            + ResponseHead("503 Service Unavailable", "2");
        Assert.AreEqual(expected, Latin1(connection.WrittenBytes));
        Assert.AreEqual("alice", plan.Points.Single().Account);
        Assert.HasCount(1, events.Events.OfType<FaultAppliedEvent>().ToList());
    }

    [TestMethod]
    public async Task AnnounceWithAChosenError_StoresNothing()
    {
        var fileSystem = new Content.InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(Root);
        var rule = new FaultRule("f1", FaultKind.Error) { Command = "ANNOUNCE", Reply = "507" };
        var request = Ascii("ANNOUNCE rtsp://h/rec.bin RTSP/1.0\r\nCSeq: 1\r\nContent-Type: application/sdp\r\nContent-Length: 5\r\n\r\nv=0\r\n");

        var (connection, _, _, _) = await ServeWithFaultsAsync([request], new ScriptedFaultPlan(rule), fileSystem: fileSystem, exposureOptions: new() { AllowUploads = true });

        Assert.AreEqual(ResponseHead("507 ", "1"), Latin1(connection.WrittenBytes));
        Assert.AreEqual(Content.ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(Root, "rec.bin.sdp")));
    }

    [TestMethod]
    public async Task DelayRuleAtTheCommandStage_IsNotAppliedAndIsNoted()
    {
        var rule = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 500 };

        var (connection, log, events, _) = await ServeWithFaultsAsync([RecordedRequest("libcurl-describe")], new ScriptedFaultPlan(rule));

        Assert.AreEqual(Latin1(RecordedResponse("libcurl-describe")), Latin1(connection.WrittenBytes));
        Assert.IsEmpty(events.Events.ToList());
        Assert.Contains("DESCRIBE /clip.bin: fault rule f1 asks for Delay at the command stage, which this server does not apply; answered as without it.", log.Notes);
    }

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log, RecordingSurlEventPublisher Events, ScriptedFaultPlan Plan)> ServeWithFaultsAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks, ScriptedFaultPlan plan, IAuthenticationPolicy? policy = null, Content.IContentFileSystem? fileSystem = null, Content.ContentExposureOptions? exposureOptions = null)
    {
        var connection = new InMemoryConnection(chunks);
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var context = Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken) with { Faults = plan, Events = events };

        await Server(fileSystem, policy, exposureOptions).ServeAsync(connection, context);

        return (connection, log, events, plan);
    }
}
