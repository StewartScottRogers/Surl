using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The chosen-error fault (ADR-0124, decisions 4 and 9): a command a matching
/// <see cref="FaultKind.Error"/> rule names is answered with the rule's reply as a RESP simple
/// error (the RESP page, "Simple errors": <c>-</c>, the text, CR LF) in place of running, and one
/// <see cref="FaultAppliedEvent"/> is published. The expected bytes are written from the RESP page.
/// </summary>
[TestClass]
public sealed class RedisCommandFaultsTests
{
    private const string Reply = "BUSY fault injected";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_GetUnderAnErrorRule_GetsExactlyTheChosenRespErrorAndPublishesOneEvent()
    {
        var events = new RecordingSurlEventPublisher();
        var keyspace = new RedisKeyspace();

        var (written, _) = await ServeAsync(
            Plan(ErrorRule(command: "GET", path: "greeting")), events, keyspace,
            Command("SET", "greeting", "hello"), Command("GET", "greeting"));

        Assert.AreEqual("+OK\r\n-BUSY fault injected\r\n", written);
        var applied = (FaultAppliedEvent)events.Events.Single();
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "greeting"), applied);
    }

    [TestMethod]
    public async Task ServeAsync_CommandTheRuleDoesNotName_IsAnsweredAndPublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(
            Plan(ErrorRule(command: "GET", path: "greeting")), events, null,
            Command("SET", "other", "hello"), Command("GET", "other"), Command("ECHO", "hi"));

        Assert.AreEqual("+OK\r\n$5\r\nhello\r\n$2\r\nhi\r\n", written);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleOnSet_LeavesTheKeyUnwritten()
    {
        var keyspace = new RedisKeyspace();

        var (written, _) = await ServeAsync(
            Plan(ErrorRule(command: "SET")), new RecordingSurlEventPublisher(), keyspace,
            Command("SET", "k", "v"), Command("EXISTS", "k"));

        Assert.AreEqual("-BUSY fault injected\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_EachCommand_IsAskedAboutWithItsSchemeListenerCommandAndKey()
    {
        var plan = Plan();

        await ServeAsync(plan, new RecordingSurlEventPublisher(), null,
            Command("ping"), Command("client", "setname", "me"), Command("hget", "h", "f"), Command("unsubscribe"));

        var points = plan.Points;
        Assert.HasCount(4, points);
        Assert.IsTrue(points.All(point => point is { Stage: FaultStage.Command, Scheme: "redis", ListenUrl: "redis://127.0.0.1:6379/", ExchangeId: 1, Account: null }));
        CollectionAssert.AreEqual(new[] { "PING", "CLIENT|SETNAME", "HGET", "UNSUBSCRIBE" }, points.Select(point => point.Command).ToArray());
        CollectionAssert.AreEqual(new[] { null, null, "h", null }, points.Select(point => point.Path).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_CommandAfterALogin_NamesTheAccount()
    {
        var plan = Plan();
        var connection = Connection([Command("AUTH", "alice", "secret"), Command("GET", "k")]);

        await new RedisServer(new UnitTestRedisAuthenticationPolicy(false)).ServeAsync(
            connection, Context(TestContext.CancellationToken) with { Faults = plan });

        Assert.AreEqual("alice", plan.Points.Last().Account);
        Assert.AreEqual("+OK\r\n$-1\r\n", Written(connection));
    }

    [TestMethod]
    public async Task ServeAsync_RefusedCommands_KeepTheirOwnErrorsUnderARuleNamingEveryCommand()
    {
        var plan = Plan(ErrorRule());
        var connection = Connection([Command("NOSUCH"), Command("GET"), Command("GET", "k")]);

        await new RedisServer(new UnitTestRedisAuthenticationPolicy(false)).ServeAsync(
            connection, Context(TestContext.CancellationToken) with { Faults = plan });

        Assert.AreEqual(
            "-ERR unknown command 'NOSUCH', with args beginning with: \r\n"
            + "-ERR wrong number of arguments for 'get' command\r\n"
            + "-NOAUTH Authentication required.\r\n",
            Written(connection));
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("ERR a\r\nb", DisplayName = "CR LF")]
    [DataRow("ERR a\nb", DisplayName = "LF")]
    public async Task ServeAsync_ErrorRuleWhoseReplyIsNotASimpleError_IsNotedAndTheCommandAnswered(string? reply)
    {
        var events = new RecordingSurlEventPublisher();

        var (written, log) = await ServeAsync(
            Plan(new FaultRule("f1", FaultKind.Error) { Reply = reply }), events, null, Command("ECHO", "hi"));

        Assert.AreEqual("$2\r\nhi\r\n", written);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to ECHO: its reply does not fit the redis error shape.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleAtACommand_AbortsTheConnectionUnansweredAndPublishesOneEvent()
    {
        var events = new RecordingSurlEventPublisher();
        var connection = Connection([Command("SET", "k", "v"), Command("GET", "k"), Command("ECHO", "hi")]);

        await new RedisServer(new UnitTestRedisAuthenticationPolicy(true)).ServeAsync(
            connection,
            Context(TestContext.CancellationToken) with { Faults = Plan(new FaultRule("f1", FaultKind.Drop) { Command = "GET" }), Events = events });

        Assert.AreEqual("+OK\r\n", Written(connection));
        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "k"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_DelayRuleAtACommand_AnswersTheCommandAfterTheHold()
    {
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(
            Plan(new FaultRule("f1", FaultKind.Delay) { Command = "PING", DelayMilliseconds = 0 }), events, null, Command("PING"));

        Assert.AreEqual("+PONG\r\n", written);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "PING"), events.Events.Single());
    }

    private static ScriptedFaultPlan Plan(params FaultRule[] rules) => new(rules);

    private static FaultRule ErrorRule(string? command = null, string? path = null) =>
        new("f1", FaultKind.Error) { Scheme = "redis", Command = command, Path = path, Reply = Reply };

    private async Task<(string Written, RecordingExchangeLog Log)> ServeAsync(
        ScriptedFaultPlan plan, RecordingSurlEventPublisher events, RedisKeyspace? keyspace, params string[] commands)
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(commands);
        var server = new RedisServer(new UnitTestRedisAuthenticationPolicy(true), keyspace ?? new RedisKeyspace(), new RedisPubSub(new MessageBus.MessageBus(TimeProvider.System)));
        await server.ServeAsync(connection, Context(TestContext.CancellationToken, log: log) with { Faults = plan, Events = events });
        return (Written(connection), log);
    }
}
