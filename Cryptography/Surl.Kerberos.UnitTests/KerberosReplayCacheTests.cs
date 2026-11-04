namespace Surl.Kerberos;

/// <summary>
/// Pins <see cref="KerberosReplayCache" /> (ADR-0057 decision 7): an entry lives until its
/// <c>ctime</c> plus 300 seconds has passed on the <see cref="TimeProvider" />, and a full cache
/// refuses rather than evicts.
/// </summary>
[TestClass]
public sealed class KerberosReplayCacheTests
{
    private static readonly byte[] CipherText = [1, 2, 3, 4];

    [TestMethod]
    public void TryAdd_NewThenSame_IsAddedThenReplayed()
    {
        KerberosReplayCache cache = new(new SettableTimeProvider(ApRequestBuilder.Now));

        KerberosReplayCacheOutcome first = cache.TryAdd(CipherText, ApRequestBuilder.Now);
        KerberosReplayCacheOutcome second = cache.TryAdd(CipherText, ApRequestBuilder.Now);

        Assert.AreEqual(KerberosReplayCacheOutcome.Added, first);
        Assert.AreEqual(KerberosReplayCacheOutcome.Replayed, second);
    }

    [TestMethod]
    public void TryAdd_AtCtimePlus300Seconds_IsStillAReplay()
    {
        SettableTimeProvider clock = new(ApRequestBuilder.Now);
        KerberosReplayCache cache = new(clock);
        cache.TryAdd(CipherText, ApRequestBuilder.Now);
        clock.Now = ApRequestBuilder.Now.AddSeconds(300);

        KerberosReplayCacheOutcome outcome = cache.TryAdd(CipherText, ApRequestBuilder.Now);

        Assert.AreEqual(KerberosReplayCacheOutcome.Replayed, outcome);
    }

    [TestMethod]
    public void TryAdd_OnceCtimePlus300SecondsHasPassed_HasDroppedTheEntry()
    {
        SettableTimeProvider clock = new(ApRequestBuilder.Now);
        KerberosReplayCache cache = new(clock);
        cache.TryAdd(CipherText, ApRequestBuilder.Now);
        cache.TryAdd([9], ApRequestBuilder.Now.AddSeconds(200));
        clock.Now = ApRequestBuilder.Now.AddSeconds(300).AddTicks(1);

        KerberosReplayCacheOutcome outcome = cache.TryAdd(CipherText, ApRequestBuilder.Now);

        Assert.AreEqual(KerberosReplayCacheOutcome.Added, outcome);
        Assert.AreEqual(2, cache.Count);
    }

    [TestMethod]
    public void TryAdd_Full_RefusesTheNextWithoutEvictingAny()
    {
        KerberosReplayCache cache = new(new SettableTimeProvider(ApRequestBuilder.Now));
        for (int index = 0; index < KerberosReplayCache.Capacity; index++)
        {
            cache.TryAdd(BitConverter.GetBytes(index), ApRequestBuilder.Now);
        }

        KerberosReplayCacheOutcome outcome = cache.TryAdd(CipherText, ApRequestBuilder.Now);

        Assert.AreEqual(KerberosReplayCacheOutcome.Full, outcome);
        Assert.AreEqual(65536, cache.Count);
        Assert.AreEqual(KerberosReplayCacheOutcome.Replayed, cache.TryAdd(BitConverter.GetBytes(0), ApRequestBuilder.Now));
        Assert.AreEqual(KerberosReplayCacheOutcome.Replayed, cache.TryAdd(BitConverter.GetBytes(65535), ApRequestBuilder.Now));
    }

    [TestMethod]
    public void TryAdd_FullButEntriesPassed_AddsAgain()
    {
        SettableTimeProvider clock = new(ApRequestBuilder.Now);
        KerberosReplayCache cache = new(clock);
        for (int index = 0; index < KerberosReplayCache.Capacity; index++)
        {
            cache.TryAdd(BitConverter.GetBytes(index), ApRequestBuilder.Now);
        }

        clock.Now = ApRequestBuilder.Now.AddSeconds(301);

        KerberosReplayCacheOutcome outcome = cache.TryAdd(CipherText, clock.Now);

        Assert.AreEqual(KerberosReplayCacheOutcome.Added, outcome);
        Assert.AreEqual(1, cache.Count);
    }
}
