namespace Surl.Tls13;

[TestClass]
public sealed class CertificateRequestTests
{
    [TestMethod]
    public void Write_EmptyContextAndSignatureAlgorithms_WritesRfc8446Section432Layout()
    {
        var certificateRequest = new CertificateRequest(
            ReadOnlyMemory<byte>.Empty,
            [TlsExtensions.SignatureAlgorithms([TlsSignatureScheme.RsaPssRsaeSha256, TlsSignatureScheme.EcdsaSecp256r1Sha256])]);

        var message = certificateRequest.Write();

        // type 13, length 13; empty context; extensions (10): signature_algorithms (13), data (6): list (4).
        CollectionAssert.AreEqual(Convert.FromHexString("0D00000D" + "00" + "000A" + "000D0006" + "000408040403"), message);
    }
}
