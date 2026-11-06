namespace Surl.Tls13;

[TestClass]
public sealed class EncryptedExtensionsTests
{
    [TestMethod]
    public void Write_Rfc8448Section3EncryptedExtensions_WritesItByteForByte()
    {
        var encryptedExtensions = new EncryptedExtensions(
        [
            TlsExtensions.SupportedGroups(
            [
                TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1,
                TlsNamedGroup.Ffdhe2048, (TlsNamedGroup)0x0101, (TlsNamedGroup)0x0102, (TlsNamedGroup)0x0103,
                (TlsNamedGroup)0x0104,
            ]),
            TlsExtensions.RecordSizeLimit(0x4001),
            TlsExtensions.ServerNameAcknowledged(),
        ]);

        var message = encryptedExtensions.Write();

        CollectionAssert.AreEqual(Rfc8448.EncryptedExtensions, message);
    }

    [TestMethod]
    public void Write_AlpnH3AndTransportParameters_WritesTheSelectedProtocolAndTheOpaqueParameters()
    {
        var encryptedExtensions = new EncryptedExtensions(
        [
            TlsExtensions.ApplicationProtocolSelected("h3"),
            TlsExtensions.QuicTransportParameters([0x0F, 0x00]),
        ]);

        var message = encryptedExtensions.Write();

        CollectionAssert.AreEqual(
            Convert.FromHexString("08000011" + "000F" + "0010000500030268 33".Replace(" ", "") + "003900020F00"),
            message);
    }
}
