namespace Surl.Tls13;

[TestClass]
public sealed class CertificateVerifyTests
{
    [TestMethod]
    public void Write_Rfc8448Section3CertificateVerify_WritesItByteForByte()
    {
        var certificateVerify = new CertificateVerify(TlsSignatureScheme.RsaPssRsaeSha256, Rfc8448.CertificateVerify[8..]);

        var message = certificateVerify.Write();

        CollectionAssert.AreEqual(Rfc8448.CertificateVerify, message);
    }
}
