namespace Surl.Dtls;

/// <summary>
/// Every handshake message body, written from RFC 5246 section 7.4, RFC 6347 section 4.2.1,
/// RFC 8422 sections 5.4 and 5.7, RFC 4279 section 2 and RFC 7250 section 3, then read back.
/// </summary>
[TestClass]
public sealed class DtlsHandshakeMessagesTests
{
    private const string RandomHex = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";

    // A DTLS 1.2 ClientHello carrying a 4-byte cookie, two suites, null compression and three
    // extensions: supported_groups [secp256r1], extended_master_secret, and an empty renegotiation_info.
    private static readonly byte[] ClientHelloWithCookie = Convert.FromHexString(
        "FEFD" + RandomHex + "00" + "04DEADBEEF" + "0004C0AEC02F" + "0100"
        + "0011" + "000A00040002" + "0017" + "00170000" + "FF01000100");

    [TestMethod]
    public void ClientHello_Read_WithCookie_DecodesEveryFieldAndListsItsExtensions()
    {
        var hello = DtlsClientHello.Read(ClientHelloWithCookie);

        Assert.AreEqual(DtlsRecordHeader.Dtls12Version, hello.ClientVersion);
        CollectionAssert.AreEqual(Convert.FromHexString(RandomHex), hello.Random);
        Assert.IsEmpty(hello.SessionId);
        CollectionAssert.AreEqual(Convert.FromHexString("DEADBEEF"), hello.Cookie);
        CollectionAssert.AreEqual(new ushort[] { 0xC0AE, 0xC02F }, hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0 }, hello.CompressionMethods);
        CollectionAssert.AreEqual(
            new[] { DtlsExtensionType.SupportedGroups, DtlsExtensionType.ExtendedMasterSecret, DtlsExtensionType.RenegotiationInfo },
            hello.Extensions.Select(extension => extension.Type).ToArray());
        CollectionAssert.AreEqual(Convert.FromHexString("00020017"), hello.Extensions[0].Data);
        Assert.IsEmpty(hello.Extensions[1].Data);
        CollectionAssert.AreEqual(new byte[] { 0 }, hello.Extensions[2].Data);
    }

    [TestMethod]
    public void ClientHello_RoundTrip_WritesTheSameBytes()
    {
        CollectionAssert.AreEqual(ClientHelloWithCookie, DtlsClientHello.Read(ClientHelloWithCookie).Write());
    }

    [TestMethod]
    public void ClientHello_Read_WithoutExtensions_HasNone()
    {
        var bytes = Convert.FromHexString("FEFD" + RandomHex + "00" + "00" + "0002C0A8" + "0100");

        var hello = DtlsClientHello.Read(bytes);

        Assert.IsEmpty(hello.Extensions);
        CollectionAssert.AreEqual(bytes, hello.Write());
    }

    [TestMethod]
    public void ClientHello_Read_AnExtensionTwice_ThrowsDecodeError()
    {
        var bytes = Convert.FromHexString("FEFD" + RandomHex + "00" + "00" + "0002C0A8" + "0100" + "0008" + "00170000" + "00170000");

        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => DtlsClientHello.Read(bytes));
    }

    [TestMethod]
    public void ClientHello_Read_BytesAfterTheExtensions_ThrowsDecodeError()
    {
        byte[] bytes = [.. ClientHelloWithCookie, 0x00];

        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => DtlsClientHello.Read(bytes));
    }

    [TestMethod]
    public void HelloVerifyRequest_RoundTrip_IsDtls10VersionThenTheCookie()
    {
        var request = new DtlsHelloVerifyRequest(DtlsHelloVerifyRequest.Dtls10Version, Convert.FromHexString("0102030405"));

        var bytes = request.Write();
        var read = DtlsHelloVerifyRequest.Read(bytes);

        CollectionAssert.AreEqual(Convert.FromHexString("FEFF" + "050102030405"), bytes);
        Assert.AreEqual(DtlsHelloVerifyRequest.Dtls10Version, read.ServerVersion);
        CollectionAssert.AreEqual(request.Cookie, read.Cookie);
    }

    [TestMethod]
    public void ServerHello_RoundTrip_WithExtensions()
    {
        var hello = new DtlsServerHello(
            DtlsRecordHeader.Dtls12Version, Convert.FromHexString(RandomHex), [], 0xC02F, 0,
            [new DtlsExtension(DtlsExtensionType.ExtendedMasterSecret, []), new DtlsExtension(DtlsExtensionType.EcPointFormats, [0x01, 0x00])]);

        var bytes = hello.Write();
        var read = DtlsServerHello.Read(bytes);

        CollectionAssert.AreEqual(
            Convert.FromHexString("FEFD" + RandomHex + "00" + "C02F" + "00" + "000A" + "00170000" + "000B00020100"), bytes);
        Assert.AreEqual((ushort)0xC02F, read.CipherSuite);
        Assert.AreEqual((byte)0, read.CompressionMethod);
        Assert.IsEmpty(read.SessionId);
        CollectionAssert.AreEqual(hello.Random, read.Random);
        Assert.AreEqual(read.ServerVersion, hello.ServerVersion);
        Assert.HasCount(2, read.Extensions);
    }

    [TestMethod]
    public void ServerHello_Write_NoExtensions_EndsAtTheCompressionMethod()
    {
        var hello = new DtlsServerHello(DtlsRecordHeader.Dtls12Version, Convert.FromHexString(RandomHex), [], 0xC0A8, 0, []);

        var bytes = hello.Write();

        CollectionAssert.AreEqual(Convert.FromHexString("FEFD" + RandomHex + "00" + "C0A8" + "00"), bytes);
        Assert.IsEmpty(DtlsServerHello.Read(bytes).Extensions);
    }

    [TestMethod]
    public void Certificate_RoundTrip_ListsEachCertificateInOrder()
    {
        var certificate = new DtlsCertificate([[0x30, 0x01], [0x30, 0x02, 0x03]]);

        var bytes = certificate.Write();
        var read = DtlsCertificate.Read(bytes);

        CollectionAssert.AreEqual(Convert.FromHexString("00000B" + "0000023001" + "000003300203"), bytes);
        Assert.HasCount(2, read.Certificates);
        CollectionAssert.AreEqual(certificate.Certificates[1], read.Certificates[1]);
    }

    [TestMethod]
    public void Certificate_RoundTrip_Empty_IsAnEmptyList()
    {
        var bytes = new DtlsCertificate([]).Write();

        CollectionAssert.AreEqual(Convert.FromHexString("000000"), bytes);
        Assert.IsEmpty(DtlsCertificate.Read(bytes).Certificates);
    }

    [TestMethod]
    public void Certificate_Read_EmptyCertificate_ThrowsDecodeError()
    {
        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => DtlsCertificate.Read(Convert.FromHexString("000003000000")));
    }

    [TestMethod]
    public void RawPublicKeyCertificate_RoundTrip_IsTheKeyAlone()
    {
        var certificate = new DtlsRawPublicKeyCertificate([0x30, 0x59, 0x30]);

        var bytes = certificate.Write();

        CollectionAssert.AreEqual(Convert.FromHexString("000003305930"), bytes);
        CollectionAssert.AreEqual(certificate.SubjectPublicKeyInfo, DtlsRawPublicKeyCertificate.Read(bytes).SubjectPublicKeyInfo);
    }

    [TestMethod]
    public void EcdheServerKeyExchange_RoundTrip_IsNamedCurveGroupPointThenSignature()
    {
        var exchange = new DtlsEcdheServerKeyExchange(23, [0x04, 0xAA], 0x0403, [0x30, 0x00]);

        var bytes = exchange.Write();
        var read = DtlsEcdheServerKeyExchange.Read(bytes);

        CollectionAssert.AreEqual(Convert.FromHexString("03" + "0017" + "0204AA" + "0403" + "00023000"), bytes);
        Assert.AreEqual((ushort)23, read.NamedGroup);
        CollectionAssert.AreEqual(exchange.PublicPoint, read.PublicPoint);
        Assert.AreEqual((ushort)0x0403, read.SignatureScheme);
        CollectionAssert.AreEqual(exchange.Signature, read.Signature);
    }

    [TestMethod]
    public void EcdheServerKeyExchange_SignedContent_IsBothRandomsThenTheParameters()
    {
        var clientRandom = Enumerable.Repeat((byte)0x11, 32).ToArray();
        var serverRandom = Enumerable.Repeat((byte)0x22, 32).ToArray();
        var exchange = new DtlsEcdheServerKeyExchange(29, [0x09], 0x0804, [0xFF]);

        var content = exchange.SignedContent(clientRandom, serverRandom);

        byte[] expected = [.. clientRandom, .. serverRandom, .. Convert.FromHexString("03001D0109")];
        CollectionAssert.AreEqual(expected, content);
    }

    [TestMethod]
    public void EcdheServerKeyExchange_Read_ExplicitCurve_ThrowsDecodeError()
    {
        Assert.ThrowsExactly<DtlsDecodeErrorException>(
            () => DtlsEcdheServerKeyExchange.Read(Convert.FromHexString("01" + "0017" + "0104" + "0403" + "0000")));
    }

    [TestMethod]
    public void CertificateRequest_RoundTrip_TypesSchemesAndAuthorities()
    {
        var request = new DtlsCertificateRequest([0x40, 0x01], [0x0403, 0x0804], [[0x30, 0x00]]);

        var bytes = request.Write();
        var read = DtlsCertificateRequest.Read(bytes);

        CollectionAssert.AreEqual(Convert.FromHexString("024001" + "000404030804" + "0004" + "00023000"), bytes);
        CollectionAssert.AreEqual(request.CertificateTypes, read.CertificateTypes);
        CollectionAssert.AreEqual(new ushort[] { 0x0403, 0x0804 }, read.SignatureSchemes.ToArray());
        Assert.HasCount(1, read.CertificateAuthorities);
        CollectionAssert.AreEqual(request.CertificateAuthorities[0], read.CertificateAuthorities[0]);
    }

    [TestMethod]
    public void ServerHelloDone_RoundTrip_IsEmpty()
    {
        var bytes = DtlsServerHelloDone.Write();

        Assert.IsEmpty(bytes);
        DtlsServerHelloDone.Read(bytes);
        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => DtlsServerHelloDone.Read([0x00]));
    }

    [TestMethod]
    public void EcdheClientKeyExchange_RoundTrip_IsThePointWithAOneByteLength()
    {
        var exchange = new DtlsEcdheClientKeyExchange([0x04, 0x01, 0x02]);

        var bytes = exchange.Write();

        CollectionAssert.AreEqual(Convert.FromHexString("03040102"), bytes);
        CollectionAssert.AreEqual(exchange.PublicPoint, DtlsEcdheClientKeyExchange.Read(bytes).PublicPoint);
    }

    [TestMethod]
    public void PskClientKeyExchange_RoundTrip_IsTheIdentityWithATwoByteLength()
    {
        var exchange = new DtlsPskClientKeyExchange("alice"u8.ToArray());

        var bytes = exchange.Write();

        CollectionAssert.AreEqual(Convert.FromHexString("0005616C696365"), bytes);
        CollectionAssert.AreEqual(exchange.Identity, DtlsPskClientKeyExchange.Read(bytes).Identity);
    }

    [TestMethod]
    public void CertificateVerify_RoundTrip_IsSchemeThenSignature()
    {
        var verify = new DtlsCertificateVerify(0x0804, [0xAB, 0xCD]);

        var bytes = verify.Write();
        var read = DtlsCertificateVerify.Read(bytes);

        CollectionAssert.AreEqual(Convert.FromHexString("08040002ABCD"), bytes);
        Assert.AreEqual((ushort)0x0804, read.SignatureScheme);
        CollectionAssert.AreEqual(verify.Signature, read.Signature);
    }

    [TestMethod]
    public void Finished_RoundTrip_IsTwelveBytesOfVerifyData()
    {
        var finished = new DtlsFinished(Enumerable.Range(1, 12).Select(value => (byte)value).ToArray());

        var bytes = finished.Write();

        CollectionAssert.AreEqual(finished.VerifyData, bytes);
        CollectionAssert.AreEqual(finished.VerifyData, DtlsFinished.Read(bytes).VerifyData);
    }

    [TestMethod]
    [DataRow(11)]
    [DataRow(13)]
    public void Finished_Read_NotTwelveBytes_ThrowsDecodeError(int length)
    {
        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => DtlsFinished.Read(new byte[length]));
    }
}
