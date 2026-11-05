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
}
