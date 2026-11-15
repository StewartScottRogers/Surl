using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// How a test prepares and returns the Redis keyspace between tests (ADR-0158 decisions 3 to 5):
/// seed a string key, reset every database, and take and restore an in-memory snapshot.
/// </summary>
[TestClass]
public sealed class RedisKeyspaceSeedResetSnapshotRestoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeededKey_IsReadBackByAGetOverAnInMemoryConnection()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "hello"u8.ToArray());
        test.Keyspace.SeedKey(2, "k"u8.ToArray(), "other"u8.ToArray());

        var written = await test.SendAsync(TestContext.CancellationToken, Command("GET", "k"), Command("SELECT", "2"), Command("GET", "k"));

        Assert.AreEqual("$5\r\nhello\r\n+OK\r\n$5\r\nother\r\n", written);
    }

    [TestMethod]
    public async Task SeededKeyWithExpiry_ExpiresOnTheKeyspacesClock()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "v"u8.ToArray(), TimeSpan.FromSeconds(10));

        Assert.AreEqual(":10000\r\n", await test.SendAsync(TestContext.CancellationToken, Command("PTTL", "k")));
        test.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual("$-1\r\n", await test.SendAsync(TestContext.CancellationToken, Command("GET", "k")));
    }

    [TestMethod]
    public async Task Seed_ReplacesAKeyAndClearsItsExpiryAsSetDoes()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "old"u8.ToArray(), TimeSpan.FromSeconds(10));

        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "new"u8.ToArray());

        Assert.AreEqual("$3\r\nnew\r\n:-1\r\n", await test.SendAsync(TestContext.CancellationToken, Command("GET", "k"), Command("PTTL", "k")));
        Assert.AreEqual(1, test.Keyspace.KeyCount);
    }

    [TestMethod]
    public void Seed_IntoAFullKeyspaceWithAnExpiredKey_PublishesTheExpiredKeysDeletionThenTheSeed()
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var keyspace = new RedisKeyspace(1, RedisKeyspace.DefaultMaxByteCount, clock, events);
        keyspace.SeedKey(0, "old"u8.ToArray(), "v"u8.ToArray(), TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(2));
        var before = events.Events.Count;

        keyspace.SeedKey(0, "new"u8.ToArray(), "vv"u8.ToArray());

        CollectionAssert.AreEqual(
            new[]
            {
                new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Deleted, "0/old", null, null),
                new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Seeded, "0/new", 2, null),
            },
            events.Events.Skip(before).OfType<StoreChangedEvent>().ToArray());
    }

    [TestMethod]
    public void Seed_PublishesOneSeededEventWithTheValueBytesAndNoExchange()
    {
        var test = new Rig();

        test.Keyspace.SeedKey(3, new byte[] { 0xFF, 0x61 }, "hello"u8.ToArray());

        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Seeded, "3/\\xFFa", 5, null), test.Changes().Single());
    }

    [TestMethod]
    public async Task Seed_IsNotStoppedByAnOutOfSpaceRuleWhichStillStopsAClientsWrite()
    {
        var test = new Rig();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));

        test.Keyspace.SeedKey(0, "k"u8.ToArray(), "v"u8.ToArray());
        var written = await test.SendAsync(TestContext.CancellationToken, plan, Command("GET", "k"), Command("SET", "k", "x"));

        Assert.AreEqual("$1\r\nv\r\n-OOM command not allowed when used memory > 'maxmemory'.\r\n", written);
        Assert.AreEqual(StoreChange.Seeded, test.Changes().Single().Change);
    }

    [TestMethod]
    public void Seed_OutOfRange_IsRefusedAndWritesNothing()
    {
        var test = new Rig();
        var key = "k"u8.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => test.Keyspace.SeedKey(-1, key, key));
        Assert.Throws<ArgumentOutOfRangeException>(() => test.Keyspace.SeedKey(16, key, key));
        Assert.Throws<ArgumentException>(() => test.Keyspace.SeedKey(0, ReadOnlyMemory<byte>.Empty, key));
        Assert.Throws<ArgumentOutOfRangeException>(() => test.Keyspace.SeedKey(0, key, key, TimeSpan.FromTicks(9_999)));
        Assert.Throws<ArgumentOutOfRangeException>(() => test.Keyspace.SeedKey(0, key, key, RedisKeyspace.MaxSeedExpiry + TimeSpan.FromMilliseconds(1)));
        Assert.Throws<InvalidOperationException>(() => test.Keyspace.SeedKey(0, key, new byte[RedisKeyspace.MaxSeedByteCount]));

        Assert.AreEqual(0, test.Keyspace.KeyCount);
        Assert.IsEmpty(test.Changes());
    }

    [TestMethod]
    public void Seed_AtTheBounds_IsAccepted()
    {
        var test = new Rig();

        test.Keyspace.SeedKey(15, "k"u8.ToArray(), new byte[RedisKeyspace.MaxSeedByteCount - 1], RedisKeyspace.MaxSeedExpiry);
        test.Keyspace.SeedKey(0, "k"u8.ToArray(), Array.Empty<byte>(), TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(2, test.Keyspace.KeyCount);
    }

    [TestMethod]
    public void Seed_IntoAFullKeyspace_IsRefusedUnlessItsKeysHaveExpired()
    {
        var clock = new ManualTimeProvider();
        var keyspace = new RedisKeyspace(1, RedisKeyspace.DefaultMaxByteCount, clock);
        keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray(), TimeSpan.FromMilliseconds(5));

        Assert.Throws<InvalidOperationException>(() => keyspace.SeedKey(0, "b"u8.ToArray(), "2"u8.ToArray()));
        Assert.AreEqual(1, keyspace.KeyCount);
        clock.Advance(TimeSpan.FromMilliseconds(5));
        keyspace.SeedKey(0, "b"u8.ToArray(), "2"u8.ToArray());

        Assert.AreEqual(1, keyspace.KeyCount);
    }

    [TestMethod]
    public void Constructor_RefusesANullClockOrPublisher()
    {
        Assert.Throws<ArgumentNullException>(() => new RedisKeyspace(null!, DiscardingSurlEventPublisher.Instance));
        Assert.Throws<ArgumentNullException>(() => new RedisKeyspace(TimeProvider.System, null!));
    }

    [TestMethod]
    public void RedisServer_OverAKeyspaceTheCallerOwns_RefusesANullKeyspaceOrBus()
    {
        var policy = new UnitTestRedisAuthenticationPolicy(true);
        var bus = new MessageBus.MessageBus(TimeProvider.System);

        Assert.Throws<ArgumentNullException>(() => new RedisServer(policy, (RedisKeyspace)null!, bus));
        Assert.Throws<ArgumentNullException>(() => new RedisServer(policy, new Rig().Keyspace, (MessageBus.MessageBus)null!));
    }

    [TestMethod]
    public async Task Reset_LeavesEveryDatabaseEmptyAndPublishesOneResetEvent()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "a"u8.ToArray(), "1"u8.ToArray());
        test.Keyspace.SeedKey(15, "b"u8.ToArray(), "2"u8.ToArray());
        await test.SendAsync(TestContext.CancellationToken, Command("HSET", "h", "f", "v"), Command("RPUSH", "l", "x"));
        test.ForgetEvents();

        test.Keyspace.Reset();

        Assert.AreEqual(0, test.Keyspace.KeyCount);
        Assert.AreEqual(0, test.Keyspace.ByteCount);
        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Reset, string.Empty, 0, null), test.Changes().Single());
        var written = await test.SendAsync(TestContext.CancellationToken, Command("DBSIZE"), Command("SELECT", "15"), Command("DBSIZE"), Command("SET", "n", "1"), Command("SCAN", "0"));
        Assert.AreEqual(":0\r\n+OK\r\n:0\r\n+OK\r\n*2\r\n$1\r\n0\r\n*1\r\n$1\r\nn\r\n", written);
    }

    [TestMethod]
    public async Task Restore_GivesBackTheSnapshotsKeysValuesAndRemainingExpiries()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "a"u8.ToArray(), "alpha"u8.ToArray());
        test.Keyspace.SeedKey(0, "b"u8.ToArray(), "beta"u8.ToArray(), TimeSpan.FromSeconds(100));
        test.Keyspace.SeedKey(0, "gone"u8.ToArray(), "x"u8.ToArray(), TimeSpan.FromSeconds(1));
        await test.SendAsync(TestContext.CancellationToken, Command("HSET", "h", "f", "v"), Command("RPUSH", "l", "x", "y"), Command("SADD", "s", "m"), Command("SELECT", "4"), Command("SET", "d4", "four"));
        test.Clock.Advance(TimeSpan.FromSeconds(2));
        var snapshot = test.Keyspace.TakeSnapshot();
        var heldBytes = test.Keyspace.ByteCount - "gone".Length - 1;
        Assert.AreEqual(heldBytes, snapshot.ByteCount);

        await test.SendAsync(TestContext.CancellationToken, Command("DEL", "a"), Command("SET", "c", "new"), Command("HSET", "h", "g", "w"), Command("RPUSH", "l", "z"), Command("SADD", "s", "n"), Command("FLUSHALL"));
        test.Clock.Advance(TimeSpan.FromSeconds(10));
        test.ForgetEvents();

        test.Keyspace.Restore(snapshot);

        Assert.AreEqual(new StoreChangedEvent(StoreKind.Keyspace, StoreChange.Restored, string.Empty, heldBytes, null), test.Changes().Single());
        Assert.AreEqual(heldBytes, test.Keyspace.ByteCount);
        var written = await test.SendAsync(
            TestContext.CancellationToken,
            Command("GET", "a"),
            Command("PTTL", "b"),
            Command("EXISTS", "gone", "c"),
            Command("HGETALL", "h"),
            Command("LRANGE", "l", "0", "-1"),
            Command("SMEMBERS", "s"),
            Command("SELECT", "4"),
            Command("GET", "d4"));
        Assert.AreEqual(
            "$5\r\nalpha\r\n:88000\r\n:0\r\n*2\r\n$1\r\nf\r\n$1\r\nv\r\n*2\r\n$1\r\nx\r\n$1\r\ny\r\n*1\r\n$1\r\nm\r\n+OK\r\n$4\r\nfour\r\n",
            written);
    }

    [TestMethod]
    public async Task Restore_TwiceAndAfterTheChangesTheRestoredKeysMadeInPlace_StillGivesTheSnapshotsContents()
    {
        var test = new Rig();
        await test.SendAsync(TestContext.CancellationToken, Command("RPUSH", "l", "x"), Command("SET", "k", "1"));
        var snapshot = test.Keyspace.TakeSnapshot();

        test.Keyspace.Restore(snapshot);
        await test.SendAsync(TestContext.CancellationToken, Command("RPUSH", "l", "more"), Command("SET", "after", "1"));
        test.Keyspace.Restore(snapshot);

        Assert.AreEqual("*1\r\n$1\r\nx\r\n:0\r\n:1\r\n", await test.SendAsync(TestContext.CancellationToken, Command("LRANGE", "l", "0", "-1"), Command("EXISTS", "after"), Command("EXISTS", "k")));
        Assert.AreEqual(2, test.Keyspace.KeyCount);
    }

    [TestMethod]
    public async Task Restore_KeepsScanNumberingSoANewKeyGoesOnFromTheSnapshot()
    {
        var test = new Rig();
        await test.SendAsync(TestContext.CancellationToken, Command("SET", "a", "1"), Command("SET", "b", "2"));
        var snapshot = test.Keyspace.TakeSnapshot();
        await test.SendAsync(TestContext.CancellationToken, Command("SET", "c", "3"), Command("SET", "d", "4"));

        test.Keyspace.Restore(snapshot);

        var written = await test.SendAsync(TestContext.CancellationToken, Command("SET", "e", "5"), Command("KEYS", "*"));
        Assert.AreEqual("+OK\r\n*3\r\n$1\r\na\r\n$1\r\nb\r\n$1\r\ne\r\n", written);
    }

    [TestMethod]
    public void Restore_RefusesASnapshotFromAnotherKeyspaceAndANullOne()
    {
        var test = new Rig();
        var other = new Rig();
        other.Keyspace.SeedKey(0, "k"u8.ToArray(), "v"u8.ToArray());

        Assert.Throws<ArgumentException>(() => test.Keyspace.Restore(other.Keyspace.TakeSnapshot()));
        Assert.Throws<ArgumentNullException>(() => test.Keyspace.Restore(null!));
        Assert.AreEqual(0, test.Keyspace.KeyCount);
        Assert.IsEmpty(test.Changes());
    }

    [TestMethod]
    public void TakeSnapshot_OverTheBound_IsRefusedNeverTruncatedAndAtTheBoundIsMade()
    {
        var test = new Rig();
        test.Keyspace.SeedKey(0, "key"u8.ToArray(), "12345"u8.ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => test.Keyspace.TakeSnapshot(7));
        var snapshot = test.Keyspace.TakeSnapshot(8);

        StringAssert.Contains(exception.Message, "8 bytes");
        Assert.AreEqual(8, snapshot.ByteCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => test.Keyspace.TakeSnapshot(-1));
        Assert.IsEmpty(test.Changes().Where(change => change.Change != StoreChange.Seeded));
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

        private RedisServer Server { get; }

        private int forgotten;

        public void ForgetEvents() => forgotten = Events.Events.Count;

        public StoreChangedEvent[] Changes() => [.. Events.Events.Skip(forgotten).OfType<StoreChangedEvent>()];

        public Task<string> SendAsync(CancellationToken cancellationToken, params string[] commands) => SendAsync(cancellationToken, new ScriptedFaultPlan(), commands);

        public async Task<string> SendAsync(CancellationToken cancellationToken, ScriptedFaultPlan plan, params string[] commands)
        {
            var connection = Connection([string.Concat(commands)]);
            await Server.ServeAsync(connection, Context(cancellationToken, Clock) with { Faults = plan });
            return Written(connection);
        }
    }
}
