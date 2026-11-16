using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>How a test reads the Redis keyspace back (ADR-0164 decision 9).</summary>
[TestClass]
public sealed class RedisKeyspaceReadBackTests
{
    public TestContext TestContext { get; set; } = null!;

    private static (RedisKeyspace Keyspace, ManualTimeProvider Clock, RecordingSurlEventPublisher Events) Seeded()
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var keyspace = new RedisKeyspace(clock, events);
        keyspace.SeedKey(0, "b"u8.ToArray(), "two"u8.ToArray());
        keyspace.SeedKey(0, "a"u8.ToArray(), "one"u8.ToArray(), TimeSpan.FromSeconds(10));
        keyspace.SeedKey(0, "c:1"u8.ToArray(), "x"u8.ToArray());
        keyspace.SeedKey(3, "z"u8.ToArray(), "other"u8.ToArray());
        return (keyspace, clock, events);
    }

    [TestMethod]
    public void ReadKey_ReturnsTheValueTypeSizeAndExpiry_AndPublishesNothing()
    {
        var (keyspace, clock, events) = Seeded();
        var before = events.Events.Count;

        var read = keyspace.ReadKey(0, "a"u8.ToArray());

        Assert.IsNotNull(read);
        Assert.AreEqual("one", System.Text.Encoding.UTF8.GetString(read.Value!.Value.Span));
        Assert.AreEqual("string", read.Info.Type);
        Assert.AreEqual(3, read.Info.ByteCount);
        Assert.AreEqual(clock.GetUtcNow().AddSeconds(10), read.Info.ExpiresAt);
        Assert.AreEqual(before, events.Events.Count);
    }

    [TestMethod]
    public void ReadKey_AbsentOrOtherDatabaseOrExpired_IsNull_AndRemovesNothing()
    {
        var (keyspace, clock, events) = Seeded();
        Assert.IsNull(keyspace.ReadKey(0, "missing"u8.ToArray()));
        Assert.IsNull(keyspace.ReadKey(1, "a"u8.ToArray()));
        clock.Advance(TimeSpan.FromSeconds(10));
        var before = events.Events.Count;

        Assert.IsNull(keyspace.ReadKey(0, "a"u8.ToArray()));

        Assert.AreEqual(before, events.Events.Count);
        Assert.AreEqual(4, keyspace.KeyCount);
    }


    [TestMethod]
    public async Task ReadKey_HoldingAnotherType_ListsTheTypeWithoutAValue()
    {
        var clock = new ManualTimeProvider();
        var keyspace = new RedisKeyspace(clock, new RecordingSurlEventPublisher());
        var server = new RedisServer(new UnitTestRedisAuthenticationPolicy(true), keyspace, new MessageBus.MessageBus(clock));
        var connection = RedisTestExchanges.Connection([RedisTestExchanges.Command("RPUSH", "l", "v")]);
        await server.ServeAsync(connection, RedisTestExchanges.Context(TestContext.CancellationToken, clock));

        var read = keyspace.ReadKey(0, "l"u8.ToArray());

        Assert.AreEqual("list", read!.Info.Type);
        Assert.IsNull(read.Value);
        Assert.AreEqual("list", keyspace.ListKeys(0, "l", default, 1).Keys[0].Type);
    }

    [TestMethod]
    public void ReadKey_DatabaseOutOfRange_Throws()
    {
        var (keyspace, _, _) = Seeded();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => keyspace.ReadKey(-1, "a"u8.ToArray()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => keyspace.ReadKey(16, "a"u8.ToArray()));
    }

    [TestMethod]
    public void ListKeys_MatchesTheGlobInOrdinalOrderAndSkipsExpired()
    {
        var (keyspace, clock, _) = Seeded();

        var all = keyspace.ListKeys(0, "*", default, 100);
        Assert.AreEqual("a,b,c:1", Names(all));
        Assert.IsFalse(all.HasMore);
        Assert.AreEqual("c:1", Names(keyspace.ListKeys(0, "c:?", default, 100)));
        Assert.AreEqual("z", Names(keyspace.ListKeys(3, "*", default, 100)));

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual("b,c:1", Names(keyspace.ListKeys(0, "*", default, 100)));
    }

    [TestMethod]
    public void ListKeys_PagesWithAfterAndLimit()
    {
        var (keyspace, _, _) = Seeded();

        var first = keyspace.ListKeys(0, "*", default, 2);
        Assert.AreEqual("a,b", Names(first));
        Assert.IsTrue(first.HasMore);
        var second = keyspace.ListKeys(0, "*", "b"u8.ToArray(), 2);
        Assert.AreEqual("c:1", Names(second));
        Assert.IsFalse(second.HasMore);
    }

    [TestMethod]
    public void ListKeys_BadArguments_Throw()
    {
        var (keyspace, _, _) = Seeded();
        Assert.ThrowsExactly<ArgumentNullException>(() => keyspace.ListKeys(0, null!, default, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => keyspace.ListKeys(-1, "*", default, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => keyspace.ListKeys(16, "*", default, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => keyspace.ListKeys(0, "*", default, 0));
    }

    private static string Names(RedisKeyInfoPage page) =>
        string.Join(',', page.Keys.Select(key => System.Text.Encoding.UTF8.GetString(key.Key.Span)));
}
