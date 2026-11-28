using Surl.Output;

namespace Surl.Api;

[TestClass]
public sealed class SurlServerOptionsTests
{
    [TestMethod]
    public void New_Defaults_AreSurlsAndTheHostsDefaults()
    {
        var options = new SurlServerOptions();

        Assert.IsEmpty(options.ListenUrls);
        Assert.IsNull(options.DataDirectory);
        Assert.IsEmpty(options.Files);
        Assert.IsFalse(options.AllowUploads || options.ListDirectories || options.FollowSymlinks || options.ServeDotFiles);
        Assert.IsEmpty(options.AdditionalArguments);
        Assert.IsNull(options.Log);
        Assert.AreEqual(LogLevel.Info, options.LogLevel);
        Assert.AreSame(TimeProvider.System, options.TimeProvider);
        Assert.IsNull(options.AreExtensionsTurnedOn);
    }

    [TestMethod]
    public void New_StartTimeSeeds_AreNone()
    {
        var options = new SurlServerOptions();

        Assert.IsEmpty(options.RedisKeys);
        Assert.IsNull(options.DirectoryLdif);
        Assert.IsEmpty(options.DnsZones);
    }

    [TestMethod]
    public void StartTimeSeedLists_SetToNull_AreLeftEmpty()
    {
        var options = new SurlServerOptions
        {
            RedisKeys = [new SurlRedisKey("k", "v")],
            DnsZones = [new SurlDnsZone("example.com", "text")],
        };

        options.RedisKeys = null!;
        options.DnsZones = null!;

        Assert.IsEmpty(options.RedisKeys);
        Assert.IsEmpty(options.DnsZones);
    }

    [TestMethod]
    public void TimeProvider_SetToNull_ThrowsArgumentNull()
    {
        var options = new SurlServerOptions();

        Assert.ThrowsExactly<ArgumentNullException>(() => options.TimeProvider = null!);
    }

    [TestMethod]
    public void TimeProvider_Set_IsKept()
    {
        var options = new SurlServerOptions();
        var timeProvider = new FixedTimeProvider();

        options.TimeProvider = timeProvider;

        Assert.AreSame(timeProvider, options.TimeProvider);
    }

    private sealed class FixedTimeProvider : TimeProvider;
}
