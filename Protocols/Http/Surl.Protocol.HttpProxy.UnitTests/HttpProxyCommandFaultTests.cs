using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// A fault rule at the proxy's command stage (ADR-0160): a chosen error, a delay or a drop of a
/// <c>CONNECT</c> or a forwarded request on HTTP/1.1 and of a stream on HTTP/2, the point the proxy
/// asks with, and the rules that must miss it.
/// </summary>
[TestClass]
public sealed class HttpProxyCommandFaultTests
{
    private const string Connect = "CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\n\r\n";

    private const string ForwardedGet = "GET http://origin.test/a?b=c HTTP/1.1\r\nHost: origin.test\r\n\r\n";

    private const string OriginResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    private const string NoChallengeNote = "fault rule f1 chose 407, and the proxy offers no challenge on this connection";

    [TestMethod]
    public async Task ServeAsync_ConnectMatchingAnErrorRule_AnswersExactlyTheChosenStatusAndOpensNoTunnel()
    {
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Command = "CONNECT", Reply = "503" });
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(Connection(Connect), origins, plan, events: events);

        Assert.AreEqual(Answer(503, "Service Unavailable"), written);
        Assert.IsEmpty(origins.Requests);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "origin.test:443"), events.Events.OfType<FaultAppliedEvent>().Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "http://127.0.0.1:3128/", Scheme = "http-proxy", Path = "origin.test:443", Command = "CONNECT" },
            plan.Points.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ConnectChosen407_CarriesTheSessionsChallengesInOrderAndContactsNoOrigin()
    {
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "407" });
        var log = new RecordingExchangeLog();

        var (written, _) = await ServeAsync(Connection(LoggedIn(Connect)), origins, plan, policy: new UnitTestProxyAuthenticationPolicy(), log: log);

        Assert.AreEqual(ChallengedAnswer("407 Proxy Authentication Required", UnitTestProxyAuthenticationPolicy.Challenges), written);
        Assert.IsEmpty(origins.Requests);
        Assert.DoesNotContain(NoChallengeNote, log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedGetChosen407WithItsOwnReason_CarriesTheSessionsChallengesInOrderAndContactsNoOrigin()
    {
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "407 Who Are You" });

        var (written, client) = await ServeAsync(Connection(LoggedIn(ForwardedGet)), origins, plan, policy: new UnitTestProxyAuthenticationPolicy());

        Assert.AreEqual(ChallengedAnswer("407 Who Are You", UnitTestProxyAuthenticationPolicy.Challenges), written);
        Assert.IsEmpty(origins.Requests);
        Assert.IsFalse(client.WritesCompleted);
    }

    [TestMethod]
    [DataRow(Connect)]
    [DataRow(ForwardedGet)]
    public async Task ServeAsync_Chosen407FromASessionOfferingNoChallenge_CarriesNoneAndNotesIt(string request)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "407" });
        var log = new RecordingExchangeLog();

        var (written, _) = await ServeAsync(Connection(request), new InMemoryOriginConnector(), plan, log: log);

        Assert.AreEqual(Answer(407, "Proxy Authentication Required"), written);
        Assert.Contains(NoChallengeNote, log.Notes);
    }

    [TestMethod]
    [DataRow("503")]
    [DataRow("401")]
    public async Task ServeAsync_ChosenStatusOtherThan407_CarriesNoChallenge(string reply)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = reply });
        var log = new RecordingExchangeLog();

        var (written, _) = await ServeAsync(Connection(LoggedIn(Connect)), new InMemoryOriginConnector(), plan, policy: new UnitTestProxyAuthenticationPolicy(), log: log);

        Assert.StartsWith($"HTTP/1.1 {reply} ", written);
        Assert.DoesNotContain("Proxy-Authenticate", written);
        Assert.DoesNotContain("WWW-Authenticate", written);
        Assert.DoesNotContain(NoChallengeNote, log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedGetMatchingAnErrorRule_AnswersTheChosenStatusAndNeverContactsTheOrigin()
    {
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Path = "http://origin.test/a", Reply = "502 Gone Fishing" });
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();

        var (written, _) = await ServeAsync(Connection(ForwardedGet), origins, plan, events: events, log: log);

        Assert.AreEqual(Answer(502, "Gone Fishing"), written);
        Assert.IsEmpty(origins.Requests);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "http://origin.test/a"), events.Events.OfType<FaultAppliedEvent>().Single());
        Assert.AreEqual("http://origin.test/a", plan.Points.Single().Path);
        Assert.AreEqual("GET", plan.Points.Single().Command);
        Assert.Contains("GET http://origin.test/a: answered 502 Gone Fishing by fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedPostMatchingAnErrorRule_ClosesBecauseItsBodyWasNotRead()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "599" });
        var request = "POST http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 3\r\n\r\nabc";

        var (written, client) = await ServeAsync(Connection(request), new InMemoryOriginConnector(), plan);

        Assert.AreEqual(Answer(599, string.Empty, closes: true), written);
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleOnAnHttpsProxyListener_IsAskedWithTheHttpsProxyScheme()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "https-proxy", Reply = "503" });

        var (written, _) = await ServeAsync(Connection(Connect), new InMemoryOriginConnector(), plan, listenScheme: "https");

        Assert.AreEqual(Answer(503, "Service Unavailable"), written);
        Assert.AreEqual("https-proxy", plan.Points.Single().Scheme);
    }

    [TestMethod]
    [DataRow("http")]
    [DataRow("https")]
    public async Task ServeAsync_RuleForTheHttpServersScheme_ChangesNothingAboutTheProxy(string ruleScheme)
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = ruleScheme, Reply = "503" });
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(Connection(ForwardedGet), origins, plan, events: events);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", written);
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleScopedToAnAccount_IsAskedWithTheAccountTheLoginJudged()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Account = "u", Reply = "503" });
        var request = "CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\nProxy-Authorization: Basic dTpw\r\n\r\n";

        var (written, _) = await ServeAsync(Connection(request), new InMemoryOriginConnector(), plan, policy: new UnitTestProxyAuthenticationPolicy());

        Assert.AreEqual(Answer(503, "Service Unavailable"), written);
        Assert.AreEqual("u", plan.Points.Single().Account);
    }

    [TestMethod]
    public async Task ServeAsync_RequestTheLoginRefused_IsNeverAskedOfTheFaultPlan()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "503" });

        var (written, _) = await ServeAsync(Connection(Connect), new InMemoryOriginConnector(), plan, policy: new UnitTestProxyAuthenticationPolicy());

        Assert.StartsWith("HTTP/1.1 407 ", written);
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task ServeAsync_MalformedConnectTarget_IsNotFaultEligible()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "503" });

        var (written, _) = await ServeAsync(Connection("CONNECT origin.test HTTP/1.1\r\nHost: origin.test\r\n\r\n"), new InMemoryOriginConnector(), plan);

        Assert.StartsWith("HTTP/1.1 400 ", written);
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleWhoseReplyDoesNotFit_IsNotedAndTheRequestGoesOn()
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "boom" });
        var log = new RecordingExchangeLog();

        var (written, _) = await ServeAsync(Connection(ForwardedGet), origins, plan, log: log);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", written);
        Assert.Contains("GET http://origin.test/a: fault rule f1's reply does not fit the HTTP error shape; answered as without it.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RuleOfAnotherKindAtTheCommandStage_ChangesNothing()
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Scheme = "http-proxy" });

        var (written, _) = await ServeAsync(Connection(ForwardedGet), origins, plan);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_DelayAtTheCommandStage_HoldsTheRequestOnTheClockWithTheIdleClockPausedThenServesIt()
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Scheme = "http-proxy", DelayMilliseconds = 500 });
        var time = new ManualTimeProvider(Now);
        var idleClock = new RecordingIdleClock();
        var events = new RecordingSurlEventPublisher();

        var serving = ServeAsync(Connection(ForwardedGet), origins, plan, events: events, clock: time, idleClock: idleClock);
        await idleClock.FirstPause;
        var completedWhileHeld = serving.IsCompleted;
        var contactedWhileHeld = origins.Requests.Count;
        time.Advance(TimeSpan.FromMilliseconds(499));
        var completedEarly = serving.IsCompleted;
        time.Advance(TimeSpan.FromMilliseconds(1));
        var (written, _) = await serving;

        Assert.IsFalse(completedWhileHeld);
        Assert.IsFalse(completedEarly);
        Assert.AreEqual(0, contactedWhileHeld);
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", written);
        Assert.IsFalse(idleClock.IsPaused);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "http://origin.test/a"), events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    [DataRow(Connect)]
    [DataRow(ForwardedGet)]
    public async Task ServeAsync_DropAtTheCommandStage_AbortsTheConnectionWithNothingSentAndNoOriginContacted(string request)
    {
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Scheme = "http-proxy" });
        var events = new RecordingSurlEventPublisher();

        var (written, client) = await ServeAsync(Connection(request), origins, plan, events: events);

        Assert.AreEqual(string.Empty, written);
        Assert.IsTrue(client.Aborted);
        Assert.IsEmpty(origins.Requests);
        Assert.HasCount(1, events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task ServeStreamAsync_ConnectStreamMatchingAnErrorRule_AnswersTheChosenStatusOnTheStream()
    {
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "503 Busy" });

        var (response, stream) = await ServeStreamAsync(new("CONNECT", "origin.test:443", "origin.test:443", [], null, false), origins, plan);

        Assert.AreEqual("503", response.Status);
        Assert.AreEqual("503 Busy\n", response.BodyText);
        Assert.IsEmpty(origins.Requests);
        Assert.IsEmpty(stream.Resets);
        Assert.AreEqual("origin.test:443", plan.Points.Single().Path);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardedStreamMatchingADropRule_ResetsTheStreamWithInternalErrorAndContactsNoOrigin()
    {
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Scheme = "http-proxy" });

        var (_, stream) = await ServeStreamAsync(new("GET", "/a?b", "origin.test", [], null, false) { Scheme = "http" }, origins, plan);

        Assert.AreEqual(Http2ErrorCode.InternalError, stream.Resets.Single().Code);
        Assert.IsEmpty(origins.Requests);
        Assert.AreEqual("http://origin.test/a", plan.Points.Single().Path);
    }

    [TestMethod]
    public async Task ServeStreamAsync_StreamChosen407_CarriesAnNtlmAndNegotiateOnlyListWholeAndResetsNothing()
    {
        string[] offered = ["NTLM", "Negotiate"];
        var origins = new InMemoryOriginConnector();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "407" });
        var head = new Http2RequestHead("CONNECT", "origin.test:443", "origin.test:443", [new("proxy-authorization", "Basic dTpw")], null, false);

        var (response, stream) = await ServeStreamAsync(head, origins, plan, new UnitTestProxyAuthenticationPolicy { OfferedChallenges = offered });

        Assert.AreEqual("407", response.Status);
        CollectionAssert.AreEqual(offered, ProxyAuthenticateValues(response));
        Assert.IsEmpty(stream.Resets);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeStreamAsync_StreamChosen407FromASessionOfferingNoChallenge_CarriesNoneAndNotesIt()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "http-proxy", Reply = "407" });
        var log = new RecordingExchangeLog();
        var head = new Http2RequestHead("GET", "/a", "origin.test", [], null, false) { Scheme = "http" };

        var (response, _) = await ServeStreamAsync(head, new InMemoryOriginConnector(), plan, log: log);

        Assert.AreEqual("407", response.Status);
        Assert.IsEmpty(ProxyAuthenticateValues(response));
        Assert.Contains(NoChallengeNote, log.Notes);
    }

    // The request with the Basic login UnitTestProxyAuthenticationPolicy lets in.
    private static string LoggedIn(string request) =>
        request.Replace("\r\n\r\n", "\r\nProxy-Authorization: Basic dTpw\r\n\r\n", StringComparison.Ordinal);

    // The proxy's own answer with one Proxy-Authenticate per challenge, on a connection kept.
    private static string ChallengedAnswer(string status, IEnumerable<string> challenges) =>
        $"HTTP/1.1 {status}\r\n{DateField}Server: surl\r\n"
        + string.Concat(challenges.Select(challenge => $"Proxy-Authenticate: {challenge}\r\n"))
        + $"Content-Type: text/plain; charset=us-ascii\r\nContent-Length: {status.Length + 1}\r\n\r\n{status}\n";

    private static string[] ProxyAuthenticateValues(Http2TestResponse response) =>
        response.Fields.Where(field => field.Name == "proxy-authenticate").Select(field => field.Value).ToArray();

    private static async Task<(string Written, InMemoryConnection Client)> ServeAsync(
        InMemoryConnection client,
        InMemoryOriginConnector origins,
        ScriptedFaultPlan plan,
        IAuthenticationPolicy? policy = null,
        RecordingSurlEventPublisher? events = null,
        RecordingExchangeLog? log = null,
        TimeProvider? clock = null,
        RecordingIdleClock? idleClock = null,
        string listenScheme = "http")
    {
        var context = Context(log ?? new RecordingExchangeLog(), clock: clock) with
        {
            ListenUrl = new ListenUrl(listenScheme, "127.0.0.1", 3128),
            Faults = plan,
            Events = events ?? new RecordingSurlEventPublisher(),
            IdleClock = idleClock ?? (IIdleClock)NoIdleClock.Instance,
        };
        await new HttpProxyProtocolServer(origins, policy ?? new AnonymousAuthenticationPolicy()).ServeAsync(client, context);

        return (Latin1(client.WrittenBytes), client);
    }

    private static async Task<(Http2TestResponse Response, Http2TestStream Stream)> ServeStreamAsync(
        Http2RequestHead head,
        InMemoryOriginConnector origins,
        ScriptedFaultPlan plan,
        IAuthenticationPolicy? policy = null,
        RecordingExchangeLog? log = null)
    {
        var stream = new Http2TestStream(head, isBodyAnnounced: false, CancellationToken.None);
        var context = Context(log ?? new RecordingExchangeLog()) with { Faults = plan };
        using var handler = new Http2ProxyStreamHandler(context, origins, (policy ?? new AnonymousAuthenticationPolicy()).StartHttpConnection(null));

        await handler.ServeStreamAsync(stream.Stream, CancellationToken.None);

        return (Http2TestClient.Responses(stream.Connection.WrittenBytes).GetValueOrDefault(1) ?? new Http2TestResponse(), stream);
    }
}
