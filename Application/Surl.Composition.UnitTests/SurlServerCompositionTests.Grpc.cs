using Surl.Extended.Protocol.Grpc;

namespace Surl.Composition;

/// <summary>
/// The gRPC server the extended servers compose for <c>grpc</c> and <c>grpcs</c> (ADR-0116, BL-1093):
/// one server claiming both schemes, switched off with the other extended servers.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersGrpcAndGrpcsOnlyWhenOn()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var on = SurlServerComposition.ComposeExtendedProtocolServers(true, null, services);
        var off = SurlServerComposition.ComposeExtendedProtocolServers(false, null, services);

        CollectionAssert.AreEqual(new[] { "grpc", "grpcs" }, on.OfType<GrpcProtocolServer>().Single().Schemes.ToArray());
        Assert.IsFalse(off.Any(server => server.Schemes.Contains("grpc") || server.Schemes.Contains("grpcs")));
    }
}
