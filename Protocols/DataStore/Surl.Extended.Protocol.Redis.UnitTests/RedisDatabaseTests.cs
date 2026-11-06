namespace Surl.Extended.Protocol.Redis;

/// <summary>One database's keys, expiry and counts (ADR-0106 decisions 1, 3 and 6).</summary>
[TestClass]
public sealed class RedisDatabaseTests
{
    private const long Now = 1_000_000;

    [TestMethod]
    public void Put_NewKeyThenReplacedValue_CountsKeysAndBytesInTheKeyspace()
    {
        var keyspace = new RedisKeyspace();

        keyspace[0].Put("key", new RedisStringValue("12345"u8.ToArray()), Now);
        keyspace[3].Put("k", new RedisStringValue("1"u8.ToArray()), Now);
        keyspace[0].Put("key", new RedisStringValue("12"u8.ToArray()), Now);

        Assert.AreEqual(2, keyspace.KeyCount);
        Assert.AreEqual(3 + 2 + 1 + 1, keyspace.ByteCount);
    }

    [TestMethod]
    public void Put_OverAnExpiredKey_CreatesItAfreshWithoutExpiryAndANewSequenceNumber()
    {
        var keyspace = new RedisKeyspace();
        var expired = keyspace[0].Put("k", new RedisStringValue([1]), Now);
        expired.ExpiresAtMilliseconds = Now + 10;

        var created = keyspace[0].Put("k", new RedisStringValue([2]), Now + 10);

        Assert.AreNotSame(expired, created);
        Assert.IsNull(created.ExpiresAtMilliseconds);
        Assert.AreEqual(2, created.SequenceNumber);
        Assert.AreEqual(1, keyspace.KeyCount);
    }

    [TestMethod]
    public void CountKeys_RemovesExpiredKeysAndTheirCounts()
    {
        var keyspace = new RedisKeyspace();
        keyspace[0].Put("a", new RedisStringValue([1]), Now).ExpiresAtMilliseconds = Now + 5;
        keyspace[0].Put("b", new RedisStringValue([1]), Now);

        var count = keyspace[0].CountKeys(Now + 5);

        Assert.AreEqual(1, count);
        Assert.AreEqual(1, keyspace.KeyCount);
        Assert.AreEqual(2, keyspace.ByteCount);
    }

    [TestMethod]
    public void Scan_CountLargerThanAnyList_ReturnsEveryKeyAndCursorZero()
    {
        var keyspace = new RedisKeyspace();
        keyspace[0].Put("a", new RedisStringValue([1]), Now);

        var (entries, nextCursor) = keyspace[0].Scan(0, long.MaxValue, Now);

        Assert.HasCount(1, entries);
        Assert.AreEqual(0UL, nextCursor);
    }

    [TestMethod]
    public void Clear_EveryDatabase_ReturnsTheCountsToZero()
    {
        var keyspace = new RedisKeyspace();
        keyspace[0].Put("a", new RedisStringValue([1]), Now);
        keyspace[15].Put("b", new UnitTestRedisValue(), Now);

        keyspace.Clear();

        Assert.AreEqual(0, keyspace.KeyCount);
        Assert.AreEqual(0, keyspace.ByteCount);
    }
}
