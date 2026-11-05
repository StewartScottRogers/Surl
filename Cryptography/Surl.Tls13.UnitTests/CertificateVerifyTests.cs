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

    [TestMethod]
    public void ReadBody_Rfc8448Section3CertificateVerify_ReadsTheSchemeAndTheSignature()
    {
        var certificateVerify = CertificateVerify.ReadBody(Rfc8448.CertificateVerify.AsSpan(4));

        Assert.AreEqual(TlsSignatureScheme.RsaPssRsaeSha256, certificateVerify.Algorithm);
        CollectionAssert.AreEqual(Rfc8448.CertificateVerify[8..], certificateVerify.Signature.ToArray());
    }

    [TestMethod]
    public void ReadBody_TrailingBytes_FailsWithDecodeError()
    {
        var exception = Assert.ThrowsExactly<TlsAlertException>(() => CertificateVerify.ReadBody(Convert.FromHexString("0403" + "0001" + "aa" + "bb")));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }
}
