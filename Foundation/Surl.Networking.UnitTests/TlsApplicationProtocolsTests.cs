namespace Surl.Networking;

[TestClass]
public sealed class TlsApplicationProtocolsTests
{
    [TestMethod]
    [DataRow("https", "http/1.1")]
    [DataRow("wss", "http/1.1")]
    [DataRow("ftp", "")]
    [DataRow("ftps", "")]
    [DataRow("imaps", "")]
    [DataRow("http", "")]
    public void ForScheme_OffersHttp11OnlyForHttpOverTls(string scheme, string expected) =>
        Assert.AreEqual(expected, string.Join(",", TlsApplicationProtocols.ForScheme(scheme)));
}
