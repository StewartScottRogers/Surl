using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class TlsApplicationProtocolsTests
{
    [TestMethod]
    [DataRow("https", "h2,http/1.1")]
    [DataRow("wss", "http/1.1")]
    [DataRow("s3", "http/1.1")]
    [DataRow("oci", "http/1.1")]
    [DataRow("ftp", "")]
    [DataRow("ftps", "")]
    [DataRow("imaps", "")]
    [DataRow("http", "")]
    public void ForScheme_OffersH2ThenHttp11ForHttpsAndHttp11OnlyForWssAndS3(string scheme, string expected) =>
        Assert.AreEqual(expected, string.Join(",", TlsApplicationProtocols.ForScheme(scheme)));

    [TestMethod]
    [DataRow("https", true, "http/1.1")]
    [DataRow("http", true, "")]
    [DataRow("https", false, "h2,http/1.1")]
    [DataRow("wss", false, "http/1.1")]
    [DataRow("ftps", false, "")]
    public void ForListener_OffersHttp11OnlyForAnHttpsProxyAndTheSchemesListOtherwise(string scheme, bool isProxy, string expected) =>
        Assert.AreEqual(
            expected,
            string.Join(",", TlsApplicationProtocols.ForListener(new ListenUrl(scheme, "127.0.0.1", 0) { IsProxy = isProxy })));
}
