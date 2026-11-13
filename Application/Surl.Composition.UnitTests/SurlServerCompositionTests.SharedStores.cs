using Surl.Content;
using Surl.Extended.Protocol.OciRegistry;
using Surl.Extended.Protocol.S3;
using Surl.Protocol.Ftp;
using Surl.Protocol.Http;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public async Task ComposeAsync_S3BesideFtpAndHttp_HandsAllThreeOneAndTheSameContentStore()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithS3("--self-signed", "http://127.0.0.1:0", "ftp://127.0.0.1:0", "s3://127.0.0.1:0"),
            seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var http = serving.Servers.OfType<HttpProtocolServer>().First();
        var ftp = serving.Servers.OfType<FtpProtocolServer>().Single();
        var s3 = serving.Servers.OfType<S3ProtocolServer>().Single();
        var contentStore = ComposedServerFields.Single<ContentStore>(s3);
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(ftp));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(http));
    }

    [TestMethod]
    public async Task ComposeAsync_S3WithoutACertificate_FailsWithCertificateProblem()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithS3("s3://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(Protocol.Abstractions.SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(58) s3://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" },
            start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_OciBesideFtpAndHttp_HandsAllThreeOneAndTheSameContentStore()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithOci("--self-signed", "http://127.0.0.1:0", "ftp://127.0.0.1:0", "oci://127.0.0.1:0"),
            seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var http = serving.Servers.OfType<HttpProtocolServer>().First();
        var ftp = serving.Servers.OfType<FtpProtocolServer>().Single();
        var oci = serving.Servers.OfType<OciRegistryProtocolServer>().Single();
        var contentStore = ComposedServerFields.Single<ContentStore>(oci);
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(ftp));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(http));
    }

    [TestMethod]
    public async Task ComposeAsync_OciWithoutACertificate_FailsWithCertificateProblem()
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithOci("oci://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(Protocol.Abstractions.SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(58) oci://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" },
            start.Failure.Lines.ToArray());
    }
}
