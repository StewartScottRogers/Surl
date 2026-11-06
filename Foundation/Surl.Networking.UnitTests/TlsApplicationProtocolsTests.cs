namespace Surl.Networking;

[TestClass]
public sealed class TlsApplicationProtocolsTests
{
    [TestMethod]
    [DataRow("https", "h2,http/1.1")]
    [DataRow("wss", "http/1.1")]
    [DataRow("s3", "http/1.1")]
    [DataRow("ftp", "")]
    [DataRow("ftps", "")]
    [DataRow("imaps", "")]
    [DataRow("http", "")]
    public void ForScheme_OffersH2ThenHttp11ForHttpsAndHttp11OnlyForWssAndS3(string scheme, string expected) =>
        Assert.AreEqual(expected, string.Join(",", TlsApplicationProtocols.ForScheme(scheme)));
}
