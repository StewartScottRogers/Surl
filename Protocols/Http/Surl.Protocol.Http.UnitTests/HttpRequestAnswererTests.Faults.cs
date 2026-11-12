using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// A fault rule's chosen error (ADR-0124 decision 4, ADR-0134 decision 1), replaying the two
/// requests pinned upstream curl 8.21.0 sent on one connection in <c>Fixtures/fault-503-then-get</c>
/// and the one it sent with <c>--fail</c> in <c>Fixtures/fault-503-fail</c>.
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    private const string BasicAlice = "Basic YWxpY2U6c2VjcmV0";

    [TestMethod]
    public async Task ServeAsync_RecordedGetOfAThenB_WithA503RuleOnA_Sends503ThenServesB()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Scheme = "http", Path = "/a", Reply = "503" };

        var (connection, log, events, plan) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("fault-503-then-get"), new ScriptedFaultPlan(rule));

        Assert.AreEqual(Latin1(RecordedResponse("fault-503-then-get")), Latin1(connection.WrittenBytes));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/a"), events.Events.OfType<FaultAppliedEvent>().Single());
        Assert.AreEqual(new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "http://127.0.0.1:18050/", Scheme = "http", Path = "/a", Command = "GET" }, plan.Points[0]);
        Assert.AreEqual("/b", plan.Points[1].Path);
        Assert.Contains("GET /a: answered 503 Service Unavailable by fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedFailingGet_WithA503Rule_SendsTheResponseCurlExited22On()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Path = "/a", Reply = "503 Service Unavailable" };

        var (connection, _, _, _) = await ServeWithFaultsAsync(RecordedFixture.ReadRequestBytes("fault-503-fail"), new ScriptedFaultPlan(rule));

        Assert.AreEqual(Latin1(RecordedResponse("fault-503-fail")), Latin1(connection.WrittenBytes));
        Assert.AreEqual("22", Latin1(RecordedFixture.ReadBytes("fault-503-fail", "exitcode.txt")));
    }

    [TestMethod]
    [DataRow("429", "429 Too Many Requests")]
    [DataRow("599", "599 ")]
    [DataRow("500 try later", "500 try later")]
    public async Task AnswerAsync_ChosenStatus_IsAnEmptyAnswerThatKeepsTheConnection(string reply, string statusLine)
    {
        var answerer = FaultAnswerer(new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply }));

        var answer = await answerer.AnswerAsync(Request("GET", "/file.txt?x=1"), TestContext.CancellationToken);

        Assert.AreEqual(statusLine, $"{answer.Status.Code} {answer.Status.ReasonPhrase}");
        Assert.IsFalse(answer.IsRefusal);
        Assert.AreEqual(new KeyValuePair<string, string>("Content-Length", "0"), answer.Fields[^1]);
    }

    [TestMethod]
    public async Task AnswerAsync_ChosenErrorForARequestWithABody_IsARefusalThatLeavesTheBodyUnread()
    {
        var log = new RecordingExchangeLog();
        var answerer = FaultAnswerer(new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "POST", Reply = "503" }), log: log);

        var answer = await answerer.AnswerAsync(Request("POST", "/file.txt", new UnitTestRequestBody(isReadable: true, isAnnounced: true)), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.ServiceUnavailable, answer.Status);
        Assert.IsTrue(answer.IsRefusal);
        Assert.Contains("POST /file.txt: fault rule f1 chose the error, and the body announced was not read; answered 503 and closed.", log.Notes);
    }

    [TestMethod]
    [DataRow("399")]
    [DataRow("600")]
    [DataRow("503 ")]
    [DataRow("503 bad\rphrase")]
    [DataRow("five hundred")]
    [DataRow("1234567890")]
    [DataRow(null)]
    public async Task AnswerAsync_ChosenErrorThatDoesNotFit_IsServedAsWithoutItAndNoted(string? reply)
    {
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var answerer = FaultAnswerer(new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply }), events, log);

        var answer = await answerer.AnswerAsync(Request("GET", "/file.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.IsEmpty(events.Events.ToList());
        Assert.Contains("GET /file.txt: fault rule f1's reply does not fit the HTTP error shape; answered as without it.", log.Notes);
    }

    [TestMethod]
    public async Task AnswerAsync_OtherKindAtTheCommandStage_IsNotAppliedAndIsNoted()
    {
        var log = new RecordingExchangeLog();
        var answerer = FaultAnswerer(new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Stage = FaultStage.Command }), log: log);

        var answer = await answerer.AnswerAsync(Request("GET", "/file.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.Contains("GET /file.txt: fault rule f1 asks for OutOfSpace at the command stage, which this server does not apply; answered as without it.", log.Notes);
    }

    [TestMethod]
    public async Task AnswerAsync_DropRuleAtTheCommandStage_AnswersWithAnAnswerThatDropsTheExchange()
    {
        var events = new RecordingSurlEventPublisher();
        var answerer = FaultAnswerer(new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command }), events);

        var answer = await answerer.AnswerAsync(Request("GET", "/file.txt"), TestContext.CancellationToken);

        Assert.IsTrue(answer.DropsExchange);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "/file.txt"), events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task ServeAsync_DelayRuleAtTheCommandStage_HoldsTheRequestForItsMillisecondsThenAnswersAsWithoutIt()
    {
        var rule = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 500 };
        var time = new ManualTimeProvider(Now);
        var idleClock = new RecordingIdleClock();
        var request = Ascii("GET /file.txt HTTP/1.1\r\nHost: 127.0.0.1:18050\r\nConnection: close\r\n\r\n");

        var serving = ServeWithFaultsAsync(request, new ScriptedFaultPlan(rule), time: time, idleClock: idleClock);
        await idleClock.FirstPause;
        var completedWhileHeld = serving.IsCompleted;
        time.Advance(TimeSpan.FromMilliseconds(499));
        var completedEarly = serving.IsCompleted;
        time.Advance(TimeSpan.FromMilliseconds(1));
        var (connection, _, events, _) = await serving;

        Assert.IsFalse(completedWhileHeld);
        Assert.IsFalse(completedEarly);
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", Latin1(connection.WrittenBytes));
        Assert.IsFalse(idleClock.IsPaused);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "/file.txt"), events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleAtTheCommandStage_AbortsTheConnectionBeforeAnyByteOfTheAnswer()
    {
        var rule = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command };
        var request = Ascii("GET /file.txt HTTP/1.1\r\nHost: 127.0.0.1:18050\r\n\r\n");

        var (connection, _, events, _) = await ServeWithFaultsAsync(request, new ScriptedFaultPlan(rule));

        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual(0, connection.WrittenBytes.Length);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "/file.txt"), events.Events.OfType<FaultAppliedEvent>().Single());
    }
    [TestMethod]
    public async Task ServeAsync_ChosenErrorScopedToAlice_FiresOnlyOnceAliceLoggedIn()
    {
        var rule = new FaultRule("f1", FaultKind.Error) { Account = "alice", Reply = "503" };
        var policy = new UnitTestAuthenticationPolicy(request => request.Fields.Any(field => field.Key == "Authorization")
            ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice")
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, ["Basic realm=\"surl\""], null));
        var request = Ascii(
            "GET /file.txt HTTP/1.1\r\nHost: 127.0.0.1:18050\r\n\r\n"
            + $"GET /file.txt HTTP/1.1\r\nHost: 127.0.0.1:18050\r\nAuthorization: {BasicAlice}\r\n\r\n");

        var (connection, _, events, plan) = await ServeWithFaultsAsync(request, new ScriptedFaultPlan(rule), policy);

        var written = Latin1(connection.WrittenBytes);
        Assert.StartsWith("HTTP/1.1 401 Unauthorized\r\n", written);
        Assert.Contains("\r\n\r\nHTTP/1.1 503 Service Unavailable\r\n", written);
        Assert.AreEqual("alice", plan.Points.Single().Account);
        Assert.HasCount(1, events.Events.OfType<FaultAppliedEvent>().ToList());
    }

    [TestMethod]
    public async Task AnswerAsync_PutWhoseLoginBindsTheBody_IsAskedOnlyOnceTheBodyCheckAcceptsAndStoresNothing()
    {
        var (fileSystem, store) = UploadStore();
        var bodyCheck = new UnitTestRequestBodyCheck(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice"));
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Account = "alice", Reply = "507" });
        var answerer = new HttpRequestAnswerer(
            Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken) with { Faults = plan },
            store,
            BodyBoundLogin(bodyCheck).StartHttpConnection(null));

        var answer = await answerer.AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(Encoding.ASCII.GetBytes("abc"))), TestContext.CancellationToken);

        Assert.AreEqual(507, answer.Status.Code);
        Assert.AreEqual("alice", plan.Points.Single().Account);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(UploadRoot, "up.bin")));
    }

    private HttpRequestAnswerer FaultAnswerer(ScriptedFaultPlan plan, RecordingSurlEventPublisher? events = null, RecordingExchangeLog? log = null)
    {
        var contentStore = new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
        var context = Context(log ?? new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken) with
        {
            Faults = plan,
            Events = events ?? new RecordingSurlEventPublisher(),
        };

        return new HttpRequestAnswerer(context, contentStore, new AnonymousAuthenticationPolicy().StartHttpConnection(null));
    }

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log, RecordingSurlEventPublisher Events, ScriptedFaultPlan Plan)> ServeWithFaultsAsync(
        byte[] request, ScriptedFaultPlan plan, IAuthenticationPolicy? policy = null, ManualTimeProvider? time = null, RecordingIdleClock? idleClock = null)
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "b"), Ascii(FileBody), FileTime);
        var store = new ContentStore(Root, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot);
        var connection = new InMemoryConnection(RecordedFixture.Whole(request));
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var context = Context(log, time ?? new ManualTimeProvider(Now), TestContext.CancellationToken) with { Faults = plan, Events = events, IdleClock = idleClock ?? (IIdleClock)NoIdleClock.Instance };

        await new HttpProtocolServer(store, policy ?? new AnonymousAuthenticationPolicy()).ServeAsync(connection, context);

        return (connection, log, events, plan);
    }
}
