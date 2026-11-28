using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The configured state a Redis keyspace reset returns to (ADR-0158 decision 4 and amendment 4):
/// the keys seeded with <c>keepOnReset</c>.
/// </summary>
[TestClass]
public sealed class RedisKeyspaceKeptSeedTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Reset_AfterClientChangesAndAnUnkeptSeed_LeavesExactlyTheKeptKeyReadableOnAnOpenConnection()
    {
        var test = new Rig();
        var value = "hello"u8.ToArray();
        test.Keyspace.SeedKey(2, "k"u8.ToArray(), value, keepOnReset: true);
        value[0] = (byte)'J';
        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);
        await client.ExpectAsync("+OK\r\n", "SELECT", "2");
        await client.ExpectAsync(":9\r\n", "APPEND", "k", " you");
        await client.ExpectAsync("+OK\r\n", "SET", "other", "1");
        test.Keyspace.SeedKey(0, "unkept"u8.ToArray(), "2"u8.ToArray());

        test.Keyspace.Reset();

        await client.ExpectAsync("$5\r\nhello\r\n", "GET", "k");
        await client.ExpectAsync(":1\r\n", "DBSIZE");
        await client.ExpectAsync("+OK\r\n", "SELECT", "0");
        await client.ExpectAsync(":0\r\n", "DBSIZE");
        Assert.AreEqual(1, test.Keyspace.KeyCount);
        Assert.AreEqual(6, test.Keyspace.ByteCount);
    }

    [TestMethod]
    public async Task Reset_AfterTheClientDeletedTheKeptKey_WritesItBack()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "v"u8.ToArray(), keepOnReset: true);
        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);
        await client.ExpectAsync(":1\r\n", "DEL", "k");

        test.Keyspace.Reset();

        await client.ExpectAsync("$1\r\nv\r\n", "GET", "k");
        await client.ExpectAsync(":-1\r\n", "PTTL", "k");
    }

    [TestMethod]
    [DataRow(4, DisplayName = "before the key expired")]
    [DataRow(25, DisplayName = "after the key had expired")]
    public async Task Reset_KeptKeyWithAnExpiry_LivesThatLongAgainFromTheReset(int secondsBeforeReset)
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "v"u8.ToArray(), TimeSpan.FromSeconds(10), keepOnReset: true);
        test.Clock.Advance(TimeSpan.FromSeconds(secondsBeforeReset));
        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);

        test.Keyspace.Reset();

        await client.ExpectAsync(":10000\r\n", "PTTL", "k");
        test.Clock.Advance(TimeSpan.FromMilliseconds(9999));
        await client.ExpectAsync("$1\r\nv\r\n", "GET", "k");
        test.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await client.ExpectAsync("$-1\r\n", "GET", "k");
    }

    [TestMethod]
    public async Task Reset_WithKeptKeys_PublishesOnlyOneResetEventAndLeavesNothingForTheNextCommandToPublish()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray(), keepOnReset: true);
        test.Keyspace.SeedKey(15, "b"u8.ToArray(), "2"u8.ToArray(), keepOnReset: true);
        test.ForgetEvents();

        test.Keyspace.Reset();
        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);
        await client.ExpectAsync("$1\r\n1\r\n", "GET", "a");
        await client.ExpectAsync("+PONG\r\n", "PING");

        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Reset, string.Empty, 0, null), test.Changes().Single());
    }

    [TestMethod]
    public async Task Reset_Twice_GivesTheSameContentsInTheOrderTheKeysWereFirstKept()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "b"u8.ToArray(), "2"u8.ToArray(), keepOnReset: true);
        test.Keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray(), keepOnReset: true);
        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);

        test.Keyspace.Reset();
        await client.ExpectAsync("*2\r\n$1\r\nb\r\n$1\r\na\r\n", "KEYS", "*");
        await client.ExpectAsync(":2\r\n", "APPEND", "a", "x");
        await client.ExpectAsync("+OK\r\n", "SET", "c", "3");
        test.Keyspace.Reset();

        await client.ExpectAsync("*2\r\n$1\r\nb\r\n$1\r\na\r\n", "KEYS", "*");
        await client.ExpectAsync("*2\r\n$1\r\n2\r\n$1\r\n1\r\n", "MGET", "b", "a");
        Assert.AreEqual(4, test.Keyspace.ByteCount);
    }

    [TestMethod]
    public async Task Reset_AfterALaterKeptSeedOfTheSameKey_ReturnsTheLaterValueAndLifetime()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "first"u8.ToArray(), TimeSpan.FromSeconds(10), keepOnReset: true);
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "later"u8.ToArray(), keepOnReset: true);
        test.Keyspace.SeedKey(1, "k"u8.ToArray(), "one"u8.ToArray(), keepOnReset: true);
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "unkept"u8.ToArray(), TimeSpan.FromSeconds(1));

        test.Keyspace.Reset();

        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);
        await client.ExpectAsync("$5\r\nlater\r\n", "GET", "k");
        await client.ExpectAsync(":-1\r\n", "PTTL", "k");
        await client.ExpectAsync("+OK\r\n", "SELECT", "1");
        await client.ExpectAsync("$3\r\none\r\n", "GET", "k");
        Assert.AreEqual(2, test.Keyspace.KeyCount);
    }

    [TestMethod]
    public async Task Reset_AfterARefusedKeptSeed_ReturnsOnlyWhatWasKeptBefore()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "v"u8.ToArray(), keepOnReset: true);
        var key = "k"u8.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => test.Keyspace.SeedKey(0, key, "w"u8.ToArray(), TimeSpan.Zero, keepOnReset: true));
        Assert.Throws<InvalidOperationException>(() => test.Keyspace.SeedKey(0, key, new byte[RedisKeyspace.MaxSeedByteCount], keepOnReset: true));
        test.Keyspace.Reset();

        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);
        await client.ExpectAsync("$1\r\nv\r\n", "GET", "k");
        Assert.AreEqual(1, test.Keyspace.KeyCount);
    }

    [TestMethod]
    public void SeedKey_KeptIntoAFullKeyspace_IsRefusedAndKeepsNothing()
    {
        var keyspace = new RedisKeyspace(1, RedisKeyspace.DefaultMaxByteCount);
        keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray());

        Assert.Throws<InvalidOperationException>(() => keyspace.SeedKey(0, "b"u8.ToArray(), "2"u8.ToArray(), keepOnReset: true));
        keyspace.Reset();

        Assert.AreEqual(0, keyspace.KeyCount);
    }

    [TestMethod]
    [DataRow(1L, RedisKeyspace.DefaultMaxByteCount, DisplayName = "the key bound")]
    [DataRow(RedisKeyspace.DefaultMaxKeyCount, 2L, DisplayName = "the byte bound")]
    public void SeedKey_KeptWhileTheKeptKeysAreAtABound_IsRefusedAndWritesNothing(long maxKeyCount, long maxByteCount)
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var keyspace = new RedisKeyspace(maxKeyCount, maxByteCount, clock, events);
        keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray(), TimeSpan.FromSeconds(1), keepOnReset: true);
        clock.Advance(TimeSpan.FromSeconds(1));
        var before = events.Events.Count;

        Assert.Throws<InvalidOperationException>(() => keyspace.SeedKey(0, "b"u8.ToArray(), "2"u8.ToArray(), keepOnReset: true));

        Assert.AreEqual(0, keyspace.KeyCount);
        Assert.AreEqual(
            new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Deleted, "0/a", null, null),
            events.Events.Skip(before).Single());
        keyspace.Reset();
        Assert.AreEqual(1, keyspace.KeyCount);
        Assert.IsNotNull(keyspace.ReadKey(0, "a"u8.ToArray()));
    }

    [TestMethod]
    public void SeedKey_UnkeptWhileTheKeptKeysAreAtABound_IsAccepted()
    {
        var clock = new ManualTimeProvider();
        var keyspace = new RedisKeyspace(1, RedisKeyspace.DefaultMaxByteCount, clock);
        keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray(), TimeSpan.FromSeconds(1), keepOnReset: true);
        clock.Advance(TimeSpan.FromSeconds(1));

        keyspace.SeedKey(0, "b"u8.ToArray(), "2"u8.ToArray());

        Assert.IsNotNull(keyspace.ReadKey(0, "b"u8.ToArray()));
    }

    [TestMethod]
    public void SeedKey_RefusedAfterRemovingExpiredKeys_PublishesTheirDeletionsAtOnce()
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var keyspace = new RedisKeyspace(RedisKeyspace.DefaultMaxKeyCount, 4, clock, events);
        keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray(), TimeSpan.FromSeconds(1));
        keyspace.SeedKey(0, "b"u8.ToArray(), "234"u8.ToArray());
        clock.Advance(TimeSpan.FromSeconds(1));
        var before = events.Events.Count;

        Assert.Throws<InvalidOperationException>(() => keyspace.SeedKey(0, "c"u8.ToArray(), "5"u8.ToArray()));

        Assert.AreEqual(
            new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Deleted, "0/a", null, null),
            events.Events.Skip(before).Single());
    }

    [TestMethod]
    public void SeedKey_OverTheSameKeyAlreadyExpired_PublishesItsDeletionThenTheSeed()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "old"u8.ToArray(), TimeSpan.FromSeconds(1));
        test.Clock.Advance(TimeSpan.FromSeconds(1));
        test.ForgetEvents();

        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "new"u8.ToArray());

        CollectionAssert.AreEqual(
            new[]
            {
                new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Deleted, "0/k", null, null),
                new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Seeded, "0/k", 3, null),
            },
            test.Changes());
    }

    [TestMethod]
    public async Task Reset_AfterRestoringASnapshot_StillReturnsToTheKeptKeys()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "kept"u8.ToArray(), keepOnReset: true);
        await using var client = ConversationalRedisClient.Connect(test.Server, test.Clock, test.Events, TestContext.CancellationToken);
        await client.ExpectAsync("+OK\r\n", "SET", "k", "changed");
        await client.ExpectAsync("+OK\r\n", "SET", "other", "1");
        var snapshot = test.Keyspace.TakeSnapshot();

        test.Keyspace.Restore(snapshot);
        await client.ExpectAsync("$7\r\nchanged\r\n", "GET", "k");
        test.Keyspace.Reset();

        await client.ExpectAsync("$4\r\nkept\r\n", "GET", "k");
        await client.ExpectAsync(":0\r\n", "EXISTS", "other");
        Assert.AreEqual(1, test.Keyspace.KeyCount);
    }

    private sealed class Rig
    {
        public Rig()
        {
            Keyspace = new RedisKeyspace(Clock, Events);
            Server = new RedisServer(new UnitTestRedisAuthenticationPolicy(true), Keyspace, new MessageBus.MessageBus(Clock));
        }

        public ManualTimeProvider Clock { get; } = new();

        public RecordingSurlEventPublisher Events { get; } = new();

        public RedisKeyspace Keyspace { get; }

        public RedisServer Server { get; }

        private int forgotten;

        public void ForgetEvents() => forgotten = Events.Events.Count;

        public StoreChangedEvent[] Changes() => [.. Events.Events.Skip(forgotten).OfType<StoreChangedEvent>()];
    }
}
