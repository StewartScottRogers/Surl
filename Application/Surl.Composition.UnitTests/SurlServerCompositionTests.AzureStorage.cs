using Surl.Content;
using Surl.Extended.Protocol.AzureStorage;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ftp;
using Surl.Protocol.Http;

namespace Surl.Composition;

/// <summary>
/// The Azure Storage server the extended servers compose for <c>az</c>, <c>abfs</c> and <c>abfss</c> (ADR-0109, BL-1023):
/// one server over the shared content store, TLS on <c>az</c> and <c>abfss</c> only, switched off with the other extended servers.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersAzAbfsAndAbfssOnlyWhenOn()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var on = SurlServerComposition.ComposeExtendedProtocolServers(true, null, services);
        var off = SurlServerComposition.ComposeExtendedProtocolServers(false, null, services);

        CollectionAssert.AreEqual(new[] { "az", "abfs", "abfss" }, on.OfType<AzureStorageProtocolServer>().Single().Schemes.ToArray());
        Assert.IsFalse(off.Any(server => server.Schemes.Contains("az") || server.Schemes.Contains("abfs") || server.Schemes.Contains("abfss")));
    }

    [TestMethod]
    public async Task ComposeAsync_AzureStorageBesideFtpAndHttp_HandsAllThreeOneAndTheSameContentStore()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithAzureStorage("--self-signed", "http://127.0.0.1:0", "ftp://127.0.0.1:0", "az://127.0.0.1:0"),
            seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var azure = serving.Servers.OfType<AzureStorageProtocolServer>().Single();
        var contentStore = ComposedServerFields.Single<ContentStore>(azure);
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<FtpProtocolServer>().Single()));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<HttpProtocolServer>().First()));
    }

    [TestMethod]
    [DataRow("az")]
    [DataRow("abfss")]
    public async Task ComposeAsync_TlsAzureSchemeWithoutACertificate_FailsWithCertificateProblem(string scheme)
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithAzureStorage(scheme + "://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
    }

    [TestMethod]
    public async Task ComposeAsync_AbfsWithoutACertificate_StartsPlain()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithAzureStorage("abfs://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
    }
}
