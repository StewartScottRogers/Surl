using Surl.Extended.Protocol.Redis;

namespace Surl.Composition;

/// <summary>
/// The Redis server the extended servers compose for <c>redis</c> and <c>rediss</c> (ADR-0106, BL-492):
/// one server claiming both schemes, switched off with the other extended servers.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersRedisAndRedissOnlyWhenOn()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var on = SurlServerComposition.ComposeExtendedProtocolServers(true, null, services);
        var off = SurlServerComposition.ComposeExtendedProtocolServers(false, null, services);

        CollectionAssert.AreEqual(new[] { "redis", "rediss" }, on.OfType<RedisServer>().Single().Schemes.ToArray());
        Assert.IsFalse(off.Any(server => server.Schemes.Contains("redis") || server.Schemes.Contains("rediss")));
    }
}
