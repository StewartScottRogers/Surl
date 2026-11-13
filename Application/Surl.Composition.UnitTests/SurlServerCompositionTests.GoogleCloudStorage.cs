using Surl.Content;
using Surl.Extended.Protocol.GoogleCloudStorage;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ftp;
using Surl.Protocol.Http;

namespace Surl.Composition;

/// <summary>
/// The Google Cloud Storage server the extended servers compose for <c>gs</c> (ADR-0110, BL-1026):
/// one server over the shared content store, TLS from the first byte, switched off with the other extended servers.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersGsOnlyWhenOn()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var on = SurlServerComposition.ComposeExtendedProtocolServers(true, null, services);
        var off = SurlServerComposition.ComposeExtendedProtocolServers(false, null, services);

        CollectionAssert.AreEqual(new[] { "gs" }, on.OfType<GoogleCloudStorageProtocolServer>().Single().Schemes.ToArray());
        Assert.IsFalse(off.Any(server => server.Schemes.Contains("gs")));
    }

    [TestMethod]
    public async Task ComposeAsync_GsBesideFtpAndHttp_HandsAllThreeOneAndTheSameContentStore()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithGs("--self-signed", "http://127.0.0.1:0", "ftp://127.0.0.1:0", "gs://127.0.0.1:0"),
            seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var gs = serving.Servers.OfType<GoogleCloudStorageProtocolServer>().Single();
        var contentStore = ComposedServerFields.Single<ContentStore>(gs);
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<FtpProtocolServer>().Single()));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<HttpProtocolServer>().First()));
    }

    [TestMethod]
    public async Task ComposeAsync_GsWithoutACertificate_FailsWithCertificateProblem()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithGs("gs://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
    }
}
