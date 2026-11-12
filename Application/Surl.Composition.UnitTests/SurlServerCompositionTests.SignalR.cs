using Surl.Extended.Protocol.SignalR;

namespace Surl.Composition;

/// <summary>
/// The SignalR server the extended servers compose for <c>signalr</c> and <c>signalrs</c> (ADR-0119, BL-450):
/// one server claiming both schemes, switched off with the other extended servers.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersSignalRAndSignalRsOnlyWhenOn()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var on = SurlServerComposition.ComposeExtendedProtocolServers(true, null, services);
        var off = SurlServerComposition.ComposeExtendedProtocolServers(false, null, services);

        CollectionAssert.AreEqual(new[] { "signalr", "signalrs" }, on.OfType<SignalRProtocolServer>().Single().Schemes.ToArray());
        Assert.IsFalse(off.Any(server => server.Schemes.Contains("signalr") || server.Schemes.Contains("signalrs")));
    }
}
