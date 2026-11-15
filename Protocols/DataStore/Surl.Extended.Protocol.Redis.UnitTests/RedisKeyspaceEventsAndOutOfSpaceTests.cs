using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The keyspace's two stand-in seams (ADR-0158 decisions 2 and 7): each key a command writes or
/// deletes is one <see cref="StoreChangedEvent"/> of <see cref="StoreKind.Keyspace"/>, and an
/// <see cref="FaultKind.OutOfSpace"/> rule makes a write answer <c>-OOM</c> and change nothing.
/// </summary>
[TestClass]
public sealed class RedisKeyspaceEventsAndOutOfSpaceTests
{
    private const string Oom = "-OOM command not allowed when used memory > 'maxmemory'.\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Set_PublishesOneWrittenEventWithNameBytesAndExchange()
    {
        var (_, events) = await ServeAsync(null, Command("SET", "k", "hello"));

        CollectionAssert.AreEqual(new[] { new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Written, "0/k", 5, 1) }, Changes(events));
    }

    [TestMethod]
    public async Task SetOverAnExistingKey_PublishesOneWrittenEventWithTheNewBytes()
    {
        var (_, events) = await ServeAsync(null, Command("SET", "k", "hello"), Command("SET", "k", "hi"));

        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Written, "0/k", 2, 1), Changes(events)[1]);
        Assert.HasCount(2, Changes(events));
    }

    [TestMethod]
    public async Task Del_PublishesOneDeletedEventAndAMissingKeyNone()
    {
        var (_, events) = await ServeAsync(null, Command("SET", "k", "v"), Command("DEL", "k", "absent"));

        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Deleted, "0/k", null, 1), Changes(events)[1]);
        Assert.HasCount(2, Changes(events));
    }

    [TestMethod]
    public async Task Reads_PublishNothing()
    {
        var (_, events) = await ServeAsync(null, Command("SET", "k", "v"), Command("GET", "k"), Command("EXISTS", "k"), Command("TTL", "k"));

        Assert.HasCount(1, Changes(events));
    }

    [TestMethod]
    public async Task MSet_PublishesOneEventPerKey()
    {
        var (_, events) = await ServeAsync(null, Command("MSET", "a", "1", "b", "22", "a", "333"));

        CollectionAssert.AreEqual(
            new[]
            {
                new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Written, "0/a", 3, 1),
                new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Written, "0/b", 2, 1),
            },
            Changes(events));
    }

    [TestMethod]
    [DataRow("HSET", "f", "v")]
    [DataRow("LPUSH", "v")]
    [DataRow("SADD", "v")]
    public async Task CollectionWrite_PublishesOneWrittenEvent(params string[] arguments)
    {
        var (_, events) = await ServeAsync(null, Command([arguments[0], "c", .. arguments[1..]]));

        var change = Changes(events).Single();
        Assert.AreEqual(StoreChange.Written, change.Change);
        Assert.AreEqual("0/c", change.Name);
        Assert.IsGreaterThan(0, change.ByteCount!.Value);
    }

    [TestMethod]
    public async Task CollectionEmptiedByAPop_PublishesDeleted()
    {
        var (_, events) = await ServeAsync(null, Command("RPUSH", "l", "x"), Command("LPOP", "l"));

        Assert.AreEqual(StoreChange.Written, Changes(events)[0].Change);
        Assert.AreEqual(StoreChange.Deleted, Changes(events)[1].Change);
    }

    [TestMethod]
    public async Task CollectionPartlyPopped_PublishesWritten()
    {
        var (_, events) = await ServeAsync(null, Command("RPUSH", "l", "x", "y"), Command("LPOP", "l"));

        Assert.AreEqual(StoreChange.Written, Changes(events)[1].Change);
    }

    [TestMethod]
    public async Task FlushDb_PublishesOneDeletedEventNamingTheDatabase_AndAnEmptyOneNone()
    {
        var (_, events) = await ServeAsync(null, Command("SELECT", "3"), Command("FLUSHDB"), Command("SET", "a", "1"), Command("SET", "b", "2"), Command("FLUSHALL"));

        var changes = Changes(events);
        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Deleted, "3", null, 1), changes[^1]);
        Assert.HasCount(3, changes);
    }

    [TestMethod]
    public async Task Rename_PublishesDeletedForTheOldNameAndWrittenForTheNew()
    {
        var (_, events) = await ServeAsync(null, Command("SET", "a", "v"), Command("RENAME", "a", "b"));

        var changes = Changes(events);
        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Deleted, "0/a", null, 1), changes[1]);
        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Written, "0/b", 1, 1), changes[2]);
    }

    [TestMethod]
    public async Task ExpiredKeyObservedByACommand_PublishesDeleted()
    {
        var events = new RecordingSurlEventPublisher();
        var server = new RedisTestServer();
        await SendAsync(server, events, Command("SET", "k", "v", "PX", "10"));
        server.Clock.Advance(TimeSpan.FromSeconds(1));
        await SendAsync(server, events, Command("GET", "k"));

        Assert.AreEqual(StoreChange.Deleted, Changes(events)[1].Change);
    }

    [TestMethod]
    public void Name_IsUtf8WithEscapedInvalidBytesTruncatedAt256()
    {
        Assert.AreEqual("2/café", RedisKeyspaceChange.NameOf(2, "cafÃ©"));
        Assert.AreEqual("0/a\\xFFb", RedisKeyspaceChange.NameOf(0, "aÿb"));
        Assert.AreEqual(256, RedisKeyspaceChange.NameOf(0, new string('k', 400)).Length);
        Assert.AreEqual(256, RedisKeyspaceChange.NameOf(0, new string('ÿ', 400)).Length);
    }

    [TestMethod]
    public async Task OutOfSpaceRuleOnTheKey_RefusesTheWriteLeavesTheKeyAndPublishesOneFault()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "0/k" });

        var (written, events) = await ServeAsync(plan, Command("SET", "k", "v"), Command("EXISTS", "k"));

        Assert.AreEqual(Oom + ":0\r\n", written);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, 1, "0/k"), events.Events.Single());
    }

    [TestMethod]
    public async Task OutOfSpaceRuleForAnotherKey_ChangesNothing()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "0/other" });

        var (written, events) = await ServeAsync(plan, Command("SET", "k", "v"), Command("GET", "k"));

        Assert.AreEqual("+OK\r\n$1\r\nv\r\n", written);
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task OutOfSpaceRuleOnOneKeyOfAnMSet_RefusesTheWholeCommand()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "0/b" });

        var (written, _) = await ServeAsync(plan, Command("MSET", "a", "1", "b", "2"), Command("EXISTS", "a", "b"));

        Assert.AreEqual(Oom + ":0\r\n", written);
    }

    [TestMethod]
    public async Task OutOfSpaceRule_DoesNotRefuseAReadOrADelete()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));

        var (written, _) = await ServeAsync(plan, Command("GET", "k"), Command("DEL", "k"));

        Assert.AreEqual("$-1\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task OtherFaultKindAtAStoreWrite_LeavesTheWriteAlone()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "ERR no", Stage = FaultStage.StoreWrite });

        var (written, _) = await ServeAsync(plan, Command("SET", "k", "v"));

        Assert.AreEqual("+OK\r\n", written);
    }

    private static StoreChangedEvent[] Changes(RecordingSurlEventPublisher events) => [.. events.Events.OfType<StoreChangedEvent>()];

    private async Task<(string Written, RecordingSurlEventPublisher Events)> ServeAsync(ScriptedFaultPlan? plan, params string[] commands)
    {
        var events = new RecordingSurlEventPublisher();
        var connection = Connection(commands);
        var server = new RedisServer(new UnitTestRedisAuthenticationPolicy(true), new RedisKeyspace(), new RedisPubSub(new MessageBus.MessageBus(TimeProvider.System)));
        await server.ServeAsync(connection, Context(TestContext.CancellationToken) with { Faults = plan ?? new ScriptedFaultPlan(), Events = events });
        return (Written(connection), events);
    }

    private async Task SendAsync(RedisTestServer server, RecordingSurlEventPublisher events, string command)
    {
        var connection = Connection([command]);
        var served = new RedisServer(new UnitTestRedisAuthenticationPolicy(true), server.Keyspace, new RedisPubSub(server.Bus));
        await served.ServeAsync(connection, Context(TestContext.CancellationToken, server.Clock) with { Events = events });
    }
}
