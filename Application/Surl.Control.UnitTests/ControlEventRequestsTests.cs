using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Control.ControlHarness;

namespace Surl.Control;

[TestClass]
public sealed class ControlEventRequestsTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 5, 9, 30, 0, TimeSpan.Zero);
    private static readonly IPEndPoint Client = new(IPAddress.Loopback, 50512);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FindEventsAsync_EveryKind_WritesEachWithItsMembersInSnakeCase()
    {
        var log = new ControlEventLog();
        Add(log, 1, new ExchangeStartedEvent(1, "smtp://127.0.0.1:2525", "smtp", Client));
        Add(log, 2, new ExchangeBytesEvent(1, ExchangeDirection.Received, Encoding.ASCII.GetBytes("EHLO a\r\n")));
        Add(log, 3, new ExchangeBytesEvent(1, ExchangeDirection.Sent, new byte[] { 0, 1, 2 }));
        Add(log, 4, new LoginJudgedEvent(1, "smtp", "alice", "PLAIN", IsAccepted: false, IsRefusedByFault: true));
        Add(log, 5, new StoreChangedEvent(StoreKind.Content, StoreChange.Written, "/b", 9, 1) { PreviousName = "/a" });
        Add(log, 6, new FaultAppliedEvent("f1", FaultKind.RefuseLogin, FaultStage.Login, 1, "alice"));
        Add(log, 7, new ExchangeEndedEvent(1, 8, 3));
        Add(log, 8, new HookFailedEvent("h1", HookFailure.TimedOut, "slow"));

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/events"), new ManualTimeProvider(), TestContext.CancellationToken);

        const string at = "\"at\":\"2026-11-05T09:30:00.0000000\\u002B00:00\"";
        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        Assert.AreEqual(
            "{\"events\":["
            + $"{{\"sequence\":1,{at},\"kind\":\"exchange_started\",\"exchange_id\":1,\"listen_url\":\"smtp://127.0.0.1:2525\",\"scheme\":\"smtp\",\"client\":\"127.0.0.1:50512\"}},"
            + $"{{\"sequence\":2,{at},\"kind\":\"exchange_bytes\",\"exchange_id\":1,\"direction\":\"received\",\"encoding\":\"text\",\"data\":\"EHLO a\\r\\n\",\"byte_count\":8,\"is_cut\":false}},"
            + $"{{\"sequence\":3,{at},\"kind\":\"exchange_bytes\",\"exchange_id\":1,\"direction\":\"sent\",\"encoding\":\"base64\",\"data\":\"AAEC\",\"byte_count\":3,\"is_cut\":false}},"
            + $"{{\"sequence\":4,{at},\"kind\":\"login_judged\",\"exchange_id\":1,\"scheme\":\"smtp\",\"account\":\"alice\",\"mechanism\":\"PLAIN\",\"is_accepted\":false,\"is_refused_by_fault\":true}},"
            + $"{{\"sequence\":5,{at},\"kind\":\"store_changed\",\"exchange_id\":1,\"byte_count\":9,\"store\":\"content\",\"change\":\"written\",\"name\":\"/b\",\"previous_name\":\"/a\"}},"
            + $"{{\"sequence\":6,{at},\"kind\":\"fault_applied\",\"exchange_id\":1,\"rule_id\":\"f1\",\"fault_kind\":\"refuse_login\",\"stage\":\"login\",\"target\":\"alice\"}},"
            + $"{{\"sequence\":7,{at},\"kind\":\"exchange_ended\",\"exchange_id\":1,\"received_byte_count\":8,\"sent_byte_count\":3}},"
            + $"{{\"sequence\":8,{at},\"kind\":\"hook_failed\",\"hook_id\":\"h1\",\"failure\":\"timed_out\",\"message\":\"slow\"}}"
            + "],\"has_more\":false,\"dropped_event_count\":0}",
            Body(response));
    }

    [TestMethod]
    [DataRow("kind=login_judged", "4")]
    [DataRow("scheme=SMTP", "1,2,4,5")]
    [DataRow("listen_url=http%3A%2F%2F127.0.0.1%3A8080", "3")]
    [DataRow("account=alice", "4,6")]
    [DataRow("after=4", "5,6,7")]
    [DataRow("after=1&limit=2", "2,3")]
    public async Task FindEventsAsync_Query_ListsOnlyTheEventsItAsksFor(string query, string sequences)
    {
        var log = new ControlEventLog();
        Add(log, 1, new ExchangeStartedEvent(1, "smtp://127.0.0.1:2525", "smtp", Client));
        Add(log, 2, new ExchangeBytesEvent(1, ExchangeDirection.Received, Encoding.ASCII.GetBytes("QUIT\r\n")));
        Add(log, 3, new ExchangeStartedEvent(2, "http://127.0.0.1:8080", "http", Client));
        Add(log, 4, new LoginJudgedEvent(null, "smtp", "alice", "PLAIN", IsAccepted: true, IsRefusedByFault: false));
        Add(log, 5, new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "INBOX", 3, 1));
        Add(log, 6, new StoreChangedEvent(StoreKind.Accounts, StoreChange.AccountAdded, "alice", null, null));
        Add(log, 7, new StoreChangedEvent(StoreKind.Content, StoreChange.Written, "/x", 1, 99));

        var response = await ServeAsync(EndpointOver(log), Request("GET", $"/v1/events?{query}"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual(sequences, string.Join(',', SequencesIn(Body(response))));
    }

    [TestMethod]
    public async Task FindEventsAsync_MoreThanTheLimit_SaysHasMore()
    {
        var log = new ControlEventLog();
        Add(log, 1, new HookFailedEvent("h1", HookFailure.Threw, "x"));
        Add(log, 2, new HookFailedEvent("h1", HookFailure.Threw, "x"));

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/events?limit=1"), new ManualTimeProvider(), TestContext.CancellationToken);

        StringAssert.EndsWith(Body(response), "],\"has_more\":true,\"dropped_event_count\":0}");
    }

    [TestMethod]
    public async Task FindEventsAsync_UnknownKind_Is400ListingTheKinds()
    {
        var response = await ServeAsync(EndpointOver(new ControlEventLog()), Request("GET", "/v1/events?kind=nope"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual(
            "{\"error\":\"bad_request\",\"message\":\"query key kind must be one of consumer_group_changed, exchange_bytes, exchange_ended, exchange_started, fault_applied, hook_failed, login_judged, store_changed\"}",
            Body(response));
    }

    [TestMethod]
    public async Task WaitForEventAsync_EventAlreadyHeld_AnswersItAtOnce()
    {
        var log = new ControlEventLog();
        Add(log, 1, new HookFailedEvent("h1", HookFailure.Threw, "x"));

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/events/wait?kind=hook_failed"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual(
            "{\"event\":{\"sequence\":1,\"at\":\"2026-11-05T09:30:00.0000000\\u002B00:00\",\"kind\":\"hook_failed\",\"hook_id\":\"h1\",\"failure\":\"threw\",\"message\":\"x\"}}",
            Body(response));
    }

    [TestMethod]
    public async Task WaitForEventAsync_EventAddedWhileWaiting_AnswersIt()
    {
        var log = new ControlEventLog();
        Add(log, 1, new HookFailedEvent("h1", HookFailure.Threw, "old"));
        var time = new ManualTimeProvider();
        var serving = ServeAsync(EndpointOver(log), Request("GET", "/v1/events/wait?after=1&timeout_ms=5000"), time, TestContext.CancellationToken);
        await time.WaitForTimerAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Add(log, 2, new ExchangeEndedEvent(4, 0, 0));
        Add(log, 3, new HookFailedEvent("h1", HookFailure.Threw, "new"));
        var response = await serving;

        StringAssert.Contains(Body(response), "\"sequence\":2,");
    }

    [TestMethod]
    public async Task WaitForEventAsync_NothingArrivesWithinTheTimeout_AnswersTimedOut()
    {
        var time = new ManualTimeProvider();
        var serving = ServeAsync(EndpointOver(new ControlEventLog()), Request("GET", "/v1/events/wait"), time, TestContext.CancellationToken);

        await time.WaitForTimerAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(10));
        var response = await serving;

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        Assert.AreEqual("{\"timed_out\":true}", Body(response));
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("60001")]
    public async Task WaitForEventAsync_TimeoutOutsideItsBound_Is400(string timeout)
    {
        var response = await ServeAsync(EndpointOver(new ControlEventLog()), Request("GET", $"/v1/events/wait?timeout_ms={timeout}"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"query key timeout_ms must be a whole number from 1 to 60000\"}", Body(response));
    }

    [TestMethod]
    public async Task WaitForEventAsync_ExchangeCancelledWhileWaiting_Throws()
    {
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var time = new ManualTimeProvider();
        var serving = ServeAsync(EndpointOver(new ControlEventLog()), Request("GET", "/v1/events/wait"), time, exchange.Token);
        await time.WaitForTimerAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task FindExchangesAsync_HeldExchanges_AnswersTheGetExchangesShapeWithListenUrlAndScheme()
    {
        var log = new ControlEventLog();
        Add(log, 1, new ExchangeStartedEvent(1, "http://127.0.0.1:8080", "http", Client));
        Add(log, 2, new ExchangeBytesEvent(1, ExchangeDirection.Received, Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n")));
        Add(log, 3, new ExchangeBytesEvent(1, ExchangeDirection.Sent, new byte[] { 0xff }));
        Add(log, 4, new ExchangeEndedEvent(1, 16, 1));

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/exchanges"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual(
            "{\"exchanges\":[{\"exchange_id\":1,\"listen_url\":\"http://127.0.0.1:8080\",\"scheme\":\"http\",\"client\":\"127.0.0.1:50512\","
            + "\"started_at\":\"2026-11-05T09:30:00.0000000\\u002B00:00\",\"chunks\":["
            + "{\"direction\":\"received\",\"encoding\":\"text\",\"data\":\"GET / HTTP/1.1\\r\\n\"},"
            + "{\"direction\":\"sent\",\"encoding\":\"base64\",\"data\":\"/w==\"}],"
            + "\"recorded_bytes\":17,\"shown_bytes\":17,\"is_cut\":false,\"is_truncated\":false}],"
            + "\"dropped_exchange_count\":0,\"has_more\":false}",
            Body(response));
    }

    [TestMethod]
    [DataRow("after_exchange_id=1", "2,3")]
    [DataRow("scheme=HTTP", "1,3")]
    [DataRow("listen_url=smtp%3A%2F%2F127.0.0.1%3A25", "2")]
    [DataRow("limit=2", "1,2")]
    public async Task FindExchangesAsync_Query_ListsOnlyTheExchangesItAsksFor(string query, string exchangeIds)
    {
        var log = new ControlEventLog();
        Add(log, 1, new ExchangeStartedEvent(3, "http://127.0.0.1:80", "http", Client));
        Add(log, 2, new ExchangeStartedEvent(1, "http://127.0.0.1:80", "http", Client));
        Add(log, 3, new ExchangeStartedEvent(2, "smtp://127.0.0.1:25", "smtp", Client));

        var response = await ServeAsync(EndpointOver(log), Request("GET", $"/v1/exchanges?{query}"), new ManualTimeProvider(), TestContext.CancellationToken);

        var body = Body(response);
        Assert.AreEqual(exchangeIds, string.Join(',', body.Split("\"exchange_id\":").Skip(1).Select(part => part[..part.IndexOf(',', StringComparison.Ordinal)])));
        Assert.AreEqual(query == "limit=2", body.EndsWith("\"has_more\":true}", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FindExchangesAsync_ExchangePastItsShownBytes_IsCutAtAUtf8SequenceStart()
    {
        var log = new ControlEventLog();
        Add(log, 1, new ExchangeStartedEvent(1, "http://127.0.0.1:80", "http", Client));
        Add(log, 2, new ExchangeBytesEvent(1, ExchangeDirection.Received, Encoding.UTF8.GetBytes(new string('a', 65535) + "é")));
        Add(log, 3, new ExchangeBytesEvent(1, ExchangeDirection.Sent, Encoding.UTF8.GetBytes("never shown")));

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/exchanges"), new ManualTimeProvider(), TestContext.CancellationToken);

        var body = Body(response);
        StringAssert.Contains(body, "\"recorded_bytes\":65548,\"shown_bytes\":65535,\"is_cut\":true,");
        Assert.DoesNotContain("never shown", body);
    }

    [TestMethod]
    public async Task FindExchangesAsync_ChunkThatBacksOffToNothing_IsLeftOut()
    {
        var log = new ControlEventLog();
        Add(log, 1, new ExchangeStartedEvent(1, "http://127.0.0.1:80", "http", Client));
        Add(log, 2, new ExchangeBytesEvent(1, ExchangeDirection.Received, Encoding.UTF8.GetBytes(new string('a', 65535))));
        Add(log, 3, new ExchangeBytesEvent(1, ExchangeDirection.Sent, Encoding.UTF8.GetBytes("é")));

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/exchanges"), new ManualTimeProvider(), TestContext.CancellationToken);

        var body = Body(response);
        Assert.DoesNotContain("\"direction\":\"sent\"", body);
        StringAssert.Contains(body, "\"shown_bytes\":65535,\"is_cut\":true,");
    }

    [TestMethod]
    public async Task FindExchangesAsync_AnswerPastItsShownBytes_LeavesLaterExchangesOutWithHasMore()
    {
        var log = new ControlEventLog();
        var sequence = 0L;
        for (var exchangeId = 1; exchangeId <= 17; exchangeId++)
        {
            Add(log, ++sequence, new ExchangeStartedEvent(exchangeId, "http://127.0.0.1:80", "http", Client));
            Add(log, ++sequence, new ExchangeBytesEvent(exchangeId, ExchangeDirection.Received, new byte[ControlExchangeWriter.MaxShownBytesPerExchange]));
        }

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/exchanges?limit=100"), new ManualTimeProvider(), TestContext.CancellationToken);

        var body = Body(response);
        Assert.AreEqual(16, body.Split("\"exchange_id\":").Length - 1);
        StringAssert.EndsWith(body, "\"has_more\":true}");
    }

    [TestMethod]
    public async Task FindExchangesAsync_SubscriptionSkippedEventsWhileAnExchangeRan_SaysItIsTruncated()
    {
        var log = new ControlEventLog();
        Add(log, 1, new ExchangeStartedEvent(1, "http://127.0.0.1:80", "http", Client));
        Add(log, 2, new ExchangeEndedEvent(1, 0, 0));
        Add(log, 3, new ExchangeStartedEvent(2, "http://127.0.0.1:80", "http", Client));
        Add(log, 6, new ExchangeEndedEvent(2, 0, 0));
        Add(log, 7, new ExchangeStartedEvent(3, "http://127.0.0.1:80", "http", Client));

        var response = await ServeAsync(EndpointOver(log), Request("GET", "/v1/exchanges"), new ManualTimeProvider(), TestContext.CancellationToken);

        var truncated = Body(response).Split("\"is_truncated\":").Skip(1).Select(part => part[..part.IndexOf('}', StringComparison.Ordinal)]);
        Assert.AreEqual("false,true,false", string.Join(',', truncated));
    }

    private static ControlEndpoint EndpointOver(ControlEventLog log) => new(new FakeControlTarget(), new ControlToken(Token), log);

    private static void Add(ControlEventLog log, long sequence, SurlEvent surlEvent) =>
        log.Add(surlEvent with { Sequence = sequence, At = At });

    private static IEnumerable<string> SequencesIn(string body) =>
        body.Split("\"sequence\":").Skip(1).Select(part => part[..part.IndexOf(',', StringComparison.Ordinal)]);
}
