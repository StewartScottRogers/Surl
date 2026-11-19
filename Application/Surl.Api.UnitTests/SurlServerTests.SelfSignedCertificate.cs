using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Api;

// BL-1392: a test reads the public certificate SelfSigned made and pins it with verification on.
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task SelfSignedCertificatePem_AnHttpsServerOnPortZero_LetsAClientPinItAndKeepVerificationOn()
    {
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.SelfSigned = true;
        await using var server = await SurlServer.StartAsync(options, TestContext.CancellationToken);
        var pem = server.SelfSignedCertificatePem;

        Assert.IsNotNull(pem);
        Assert.DoesNotContain("PRIVATE KEY", pem);
        using var pinned = X509Certificate2.CreateFromPem(pem);
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => TrustsOnly(pinned, certificate),
        };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync(server.BoundUrl, TestContext.CancellationToken);

        Assert.IsTrue(response.StatusCode > 0);
    }

    [TestMethod]
    public async Task SelfSignedCertificatePem_TwoServersSideBySide_EachReportsItsOwnCertificate()
    {
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.SelfSigned = true;
        var otherOptions = ApiTestSeams.Options("https://127.0.0.1:0");
        otherOptions.SelfSigned = true;
        var seams = new ApiTestSeams();
        var otherSeams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await using var other = await SurlServer.StartAsync(otherOptions, otherSeams.Host, TestContext.CancellationToken);

        Assert.IsNotNull(server.SelfSignedCertificatePem);
        Assert.IsNotNull(other.SelfSignedCertificatePem);
        Assert.AreNotEqual(server.SelfSignedCertificatePem, other.SelfSignedCertificatePem);
    }

    [TestMethod]
    public async Task SelfSignedCertificatePem_WithoutSelfSigned_IsNull()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ApiTestSeams.Options(Http), seams.Host, TestContext.CancellationToken);

        Assert.IsNull(server.SelfSignedCertificatePem);
    }

    private static bool TrustsOnly(X509Certificate2 pinned, X509Certificate2? presented)
    {
        if (presented is null)
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(pinned);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(presented);
    }
}
