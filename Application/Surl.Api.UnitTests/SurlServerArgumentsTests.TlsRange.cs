using System.Security.Authentication;
using Surl.Cli;

namespace Surl.Api;

#pragma warning disable SYSLIB0039 // Tls and Tls11 name the old versions on purpose.
public sealed partial class SurlServerArgumentsTests
{
    [TestMethod]
    public void Compose_Defaults_ComposeNoTlsRangeCertificateTypeOrHeadLimitWord()
    {
        var arguments = SurlServerArguments.Compose(ApiTestSeams.Options(HttpListenUrl));

        CollectionAssert.AreEqual(new[] { HttpListenUrl }, arguments.ToArray());
    }

    [TestMethod]
    [DataRow(SslProtocols.Tls, "--tlsv1.0")]
    [DataRow(SslProtocols.Tls11, "--tlsv1.1")]
    [DataRow(SslProtocols.Tls12, "--tlsv1.2")]
    [DataRow(SslProtocols.Tls13, "--tlsv1.3")]
    public void Compose_ALowestTlsVersion_ComposesItsTlsvFlag(SslProtocols version, string flag)
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.LowestTlsVersion = version;

        CollectionAssert.AreEqual(new[] { HttpListenUrl, flag }, SurlServerArguments.Compose(options).ToArray());
    }

    [TestMethod]
    [DataRow(SslProtocols.Tls, "1.0")]
    [DataRow(SslProtocols.Tls11, "1.1")]
    [DataRow(SslProtocols.Tls12, "1.2")]
    [DataRow(SslProtocols.Tls13, "1.3")]
    public void Compose_AHighestTlsVersion_ComposesTlsMax(SslProtocols version, string word)
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.HighestTlsVersion = version;

        CollectionAssert.AreEqual(new[] { HttpListenUrl, "--tls-max", word }, SurlServerArguments.Compose(options).ToArray());
    }

    [TestMethod]
    public void Compose_CertificateAndKeyTypesAndHeadLimit_ComposeTheirOptions()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.CertificateFile = "server.p12";
        options.CertificateType = "P12";
        options.KeyFile = "server.key";
        options.KeyType = "DER";
        options.MaxRequestHeadBytes = 2048;

        var arguments = SurlServerArguments.Compose(options).ToArray();

        CollectionAssert.AreEqual(
            new[] { HttpListenUrl, "--cert", "server.p12", "--cert-type", "P12", "--key", "server.key", "--key-type", "DER", "--max-request-head", "2048" },
            arguments);
    }

    [TestMethod]
    public void Parse_TheTlsRangeTypesAndHeadLimit_ReadBackAsTheCommandLineFields()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.LowestTlsVersion = SslProtocols.Tls13;
        options.HighestTlsVersion = SslProtocols.Tls13;
        options.MaxRequestHeadBytes = 2048;

        var actual = SurlServerArguments.Parse(options, []);

        Assert.AreEqual(SslProtocols.Tls13, actual.LowestTlsVersion);
        Assert.AreEqual(2048, actual.Limits.MaxRequestHeadBytes);
    }

    [TestMethod]
    public void ThrowIfMisused_ALowestTlsVersionThatIsNotOneVersion_ThrowsArgument()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.LowestTlsVersion = SslProtocols.Tls12 | SslProtocols.Tls13;

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_AHighestTlsVersionThatIsNone_ThrowsArgument()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.HighestTlsVersion = SslProtocols.None;

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_AValidTlsRange_Passes()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.LowestTlsVersion = SslProtocols.Tls12;
        options.HighestTlsVersion = SslProtocols.Tls13;
        options.MaxRequestHeadBytes = 1;

        SurlServerArguments.ThrowIfMisused(options);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-5L)]
    public void ThrowIfMisused_AHeadLimitBelowOne_ThrowsArgument(long bytes)
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.MaxRequestHeadBytes = bytes;

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void Parse_ALowestTlsVersionAboveTheHighest_IsSurlsOwnRefusal()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.LowestTlsVersion = SslProtocols.Tls13;
        options.HighestTlsVersion = SslProtocols.Tls12;
        var expected = CommandLineParser.Parse([HttpListenUrl, "--tlsv1.3", "--tls-max", "1.2"]).Failure!;

        var exception = Assert.ThrowsExactly<SurlServerStartException>(() => SurlServerArguments.Parse(options, []));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
        Assert.AreEqual(expected.Message, exception.Lines[0]);
    }

    [TestMethod]
    public void Parse_ACertificateTypeSurlDoesNotKnow_IsSurlsOwnRefusal()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.CertificateType = "JKS";

        Assert.ThrowsExactly<SurlServerStartException>(() => SurlServerArguments.Parse(options, []));
    }
}
