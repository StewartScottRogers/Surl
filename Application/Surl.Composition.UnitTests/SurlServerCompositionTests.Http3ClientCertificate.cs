using Surl.Protocol.Http;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    // --http3 with --cacert (BL-741, ADR-0082 amendment 5): the HTTP/3 server requires a client
    // certificate as the TCP listener does; without --cacert it asks for none.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ComposeAsync_Http3_RequiresAClientCertificateExactlyWithCacert(bool givesCacert)
    {
        using var files = TestCertificateFiles.Create();
        string[] cacert = givesCacert ? ["--cacert", files.CertificateFile] : [];

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. cacert, "--self-signed", "--http3", "https://127.0.0.1:0"]),
            new StartTestSeams().Seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        Assert.AreEqual(givesCacert, serving.Servers.OfType<Http3ProtocolServer>().Single().RequiresClientCertificate);
    }
}
