namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapDuplicateCache"/>: Message IDs remembered until they expire, bounded by its
/// capacity (RFC 7252 section 4.5; ADR-0097 decision 7).
/// </summary>
[TestClass]
public sealed class CoapDuplicateCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void TryRecall_BeforeExpiry_ReturnsTheAnswerAndAfterIt_Nothing()
    {
        var cache = new CoapDuplicateCache(2);
        cache.Remember(1, [9], Now.AddSeconds(10));

        Assert.IsTrue(cache.TryRecall(1, Now, out var answer));
        CollectionAssert.AreEqual(new byte[] { 9 }, answer);
        Assert.IsFalse(cache.TryRecall(1, Now.AddSeconds(10), out answer));
        Assert.IsNull(answer);
        Assert.IsFalse(cache.TryRecall(2, Now, out _));
    }

    [TestMethod]
    public void Remember_AnExpiredId_ReplacesIt()
    {
        var cache = new CoapDuplicateCache(2);
        cache.Remember(1, [9], Now);

        cache.Remember(1, null, Now.AddSeconds(10));

        Assert.IsTrue(cache.TryRecall(1, Now.AddSeconds(5), out var answer));
        Assert.IsNull(answer);
    }

    [TestMethod]
    public void Remember_PastTheCapacity_ForgetsTheOldest()
    {
        var cache = new CoapDuplicateCache(2);

        cache.Remember(1, [1], Now.AddSeconds(10));
        cache.Remember(2, [2], Now.AddSeconds(10));
        cache.Remember(3, [3], Now.AddSeconds(10));

        Assert.IsFalse(cache.TryRecall(1, Now, out _));
        Assert.IsTrue(cache.TryRecall(2, Now, out _));
        Assert.IsTrue(cache.TryRecall(3, Now, out _));
    }
}
