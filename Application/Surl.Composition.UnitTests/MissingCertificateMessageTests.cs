using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class MissingCertificateMessageTests
{
    [TestMethod]
    public void FormatMissingCertificate_CoapsListenUrl_NamesTheCertificateTheSelfSignedOneAndTheAccount()
    {
        var message = SurlServerComposition.FormatMissingCertificate(new ListenUrl("coaps", "127.0.0.1", 0));

        Assert.AreEqual(
            "(58) coaps://127.0.0.1:0/ needs a certificate or an account: give --cert <file>, --self-signed for a throwaway one, or --user <name:key> for a pre-shared key",
            message);
    }

    [TestMethod]
    [DataRow("https")]
    [DataRow("xmpps")]
    [DataRow("webtransport")]
    public void FormatMissingCertificate_OtherScheme_KeepsTheCertificateOnlyText(string scheme)
    {
        var message = SurlServerComposition.FormatMissingCertificate(new ListenUrl(scheme, "127.0.0.1", 0));

        Assert.AreEqual(
            $"(58) {scheme}://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one",
            message);
    }
}
