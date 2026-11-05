namespace Surl.Tls13;

[TestClass]
public sealed class CertificateMessageTests
{
    [TestMethod]
    public void Write_Rfc8448Section3Certificate_WritesItByteForByte()
    {
        var certificateDer = Rfc8448.Certificate[11..(11 + 0x01B0)];
        var certificate = new CertificateMessage(
            ReadOnlyMemory<byte>.Empty,
            [new TlsCertificateEntry(certificateDer, [])]);

        var message = certificate.Write();

        CollectionAssert.AreEqual(Rfc8448.Certificate, message);
    }

    [TestMethod]
    public void ReadBody_Rfc8448Section3Certificate_ReadsTheContextTheCertificateAndItsExtensions()
    {
        var certificate = CertificateMessage.ReadBody(Rfc8448.Certificate.AsSpan(4));

        Assert.AreEqual(0, certificate.CertificateRequestContext.Length);
        Assert.HasCount(1, certificate.Entries);
        CollectionAssert.AreEqual(Rfc8448.Certificate[11..(11 + 0x01B0)], certificate.Entries[0].CertificateData.ToArray());
        Assert.IsEmpty(certificate.Entries[0].Extensions);
    }

    [TestMethod]
    public void ReadBody_EmptyCertificateData_FailsWithDecodeError()
    {
        var exception = Assert.ThrowsExactly<TlsAlertException>(
            () => CertificateMessage.ReadBody(Convert.FromHexString("00" + "000005" + "000000" + "0000")));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }
}
