namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerNegotiation"/> against hellos built from RFC 5246 section 7.4.1.2 and
/// RFC 8422 section 5.1, one rule of ADR-0087 decisions 1, 2 and 5 at a time.
/// </summary>
[TestClass]
public sealed class DtlsServerNegotiationTests
{
    private static readonly byte[] Random = new byte[DtlsClientHello.RandomLength];

    [TestMethod]
    public void Negotiate_FullHello_ChoosesTheServersFirstSuiteAndAnswersEachExtension()
    {
        var hello = Hello([0xC0AE, 0xC02B], Extensions());

        var negotiation = DtlsServerNegotiation.Negotiate(hello, UnitTestCertificates.EcdsaP256, aesCcmSupported: true);

        Assert.AreEqual((ushort)0xC02B, negotiation.Suite.Code);
        Assert.AreEqual(DtlsNamedGroup.Secp256r1, negotiation.Group);
        Assert.AreEqual(DtlsSignatureScheme.EcdsaSecp256r1Sha256, negotiation.SignatureScheme);
        Assert.IsTrue(negotiation.UsesExtendedMasterSecret);
        CollectionAssert.AreEqual(
            new[] { DtlsExtensionType.EcPointFormats, DtlsExtensionType.ExtendedMasterSecret, DtlsExtensionType.RenegotiationInfo },
            negotiation.ServerExtensions.Select(extension => extension.Type).ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, negotiation.ServerExtensions[0].Data);
        CollectionAssert.AreEqual(new byte[] { 0 }, negotiation.ServerExtensions[2].Data);
    }

    [TestMethod]
    public void Negotiate_AesCcmUnsupported_SkipsTheCcmSuites()
    {
        var hello = Hello([0xC0AE, 0xC02C], Extensions());

        var negotiation = DtlsServerNegotiation.Negotiate(hello, UnitTestCertificates.EcdsaP256, aesCcmSupported: false);

        Assert.AreEqual((ushort)0xC02C, negotiation.Suite.Code);
    }

    [TestMethod]
    public void Negotiate_OnlyExtensionsTheServerNeeds_AnswersNoneAndTakesSecp256r1()
    {
        var hello = Hello([0xC02B], [Extension(DtlsExtensionType.SignatureAlgorithms, UnitTestDtlsClient.UInt16List([0x0403]))]);

        var negotiation = DtlsServerNegotiation.Negotiate(hello, UnitTestCertificates.EcdsaP256, aesCcmSupported: true);

        Assert.AreEqual(DtlsNamedGroup.Secp256r1, negotiation.Group);
        Assert.IsFalse(negotiation.UsesExtendedMasterSecret);
        Assert.IsEmpty(negotiation.ServerExtensions);
    }

    [TestMethod]
    public void Negotiate_RenegotiationSignalingSuite_AnswersAnEmptyRenegotiationInfo()
    {
        var hello = Hello([0xC02B, DtlsServerNegotiation.EmptyRenegotiationInfoScsv], [Extension(DtlsExtensionType.SignatureAlgorithms, UnitTestDtlsClient.UInt16List([0x0403]))]);

        var negotiation = DtlsServerNegotiation.Negotiate(hello, UnitTestCertificates.EcdsaP256, aesCcmSupported: true);

        Assert.AreEqual(DtlsExtensionType.RenegotiationInfo, negotiation.ServerExtensions.Single().Type);
    }

    [TestMethod]
    [DataRow((ushort)29, DtlsNamedGroup.X25519)]
    [DataRow((ushort)24, DtlsNamedGroup.Secp384r1)]
    public void Negotiate_ClientGroups_TakesTheFirstInTheServersOrder(ushort offered, DtlsNamedGroup expected)
    {
        var hello = Hello([0xC02B], Extensions(groups: [0x0017 + 100, offered]));

        Assert.AreEqual(expected, DtlsServerNegotiation.Negotiate(hello, UnitTestCertificates.EcdsaP256, true).Group);
    }

    [TestMethod]
    public void Negotiate_ClientVersionBelowDtls12_IsProtocolVersion()
    {
        var hello = Hello([0xC02B], Extensions()) with { ClientVersion = DtlsHelloVerifyRequest.Dtls10Version };

        AssertAlert(DtlsAlertDescription.ProtocolVersion, hello);
    }

    [TestMethod]
    public void Negotiate_NoNullCompression_IsIllegalParameter()
    {
        var hello = Hello([0xC02B], Extensions()) with { CompressionMethods = [1] };

        AssertAlert(DtlsAlertDescription.IllegalParameter, hello);
    }

    [TestMethod]
    public void Negotiate_NoSuiteTheCertificateServes_IsHandshakeFailure()
    {
        AssertAlert(DtlsAlertDescription.HandshakeFailure, Hello([0xC02F], Extensions()));
    }

    [TestMethod]
    public void Negotiate_NoUncompressedPointFormat_IsIllegalParameter()
    {
        var hello = Hello([0xC02B], [.. Extensions().Where(e => e.Type != DtlsExtensionType.EcPointFormats), Extension(DtlsExtensionType.EcPointFormats, [1, 1])]);

        AssertAlert(DtlsAlertDescription.IllegalParameter, hello);
    }

    [TestMethod]
    public void Negotiate_NoCommonGroup_IsHandshakeFailure()
    {
        AssertAlert(DtlsAlertDescription.HandshakeFailure, Hello([0xC02B], Extensions(groups: [30])));
    }

    [TestMethod]
    public void Negotiate_NoSignatureAlgorithms_IsHandshakeFailure()
    {
        var hello = Hello([0xC02B], [.. Extensions().Where(e => e.Type != DtlsExtensionType.SignatureAlgorithms)]);

        AssertAlert(DtlsAlertDescription.HandshakeFailure, hello);
    }

    [TestMethod]
    public void Negotiate_NoSchemeTheKeyCanMake_IsHandshakeFailure()
    {
        AssertAlert(DtlsAlertDescription.HandshakeFailure, Hello([0xC02B], Extensions(schemes: [0x0804])));
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 9 })]
    [DataRow(new byte[] { 5 })]
    [DataRow(new byte[] { })]
    public void Negotiate_NonEmptyRenegotiationInfo_IsHandshakeFailure(byte[] data)
    {
        var hello = Hello([0xC02B], [.. Extensions().Where(e => e.Type != DtlsExtensionType.RenegotiationInfo), Extension(DtlsExtensionType.RenegotiationInfo, data)]);

        AssertAlert(DtlsAlertDescription.HandshakeFailure, hello);
    }

    [TestMethod]
    [DataRow(DtlsExtensionType.EcPointFormats)]
    [DataRow(DtlsExtensionType.SupportedGroups)]
    public void Negotiate_ExtensionWithBytesAfterItsList_IsADecodeError(DtlsExtensionType type)
    {
        var data = Extensions().Single(e => e.Type == type).Data;
        var hello = Hello([0xC02B], [.. Extensions().Where(e => e.Type != type), Extension(type, [.. data, 0])]);

        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => DtlsServerNegotiation.Negotiate(hello, UnitTestCertificates.EcdsaP256, true));
    }

    private static void AssertAlert(DtlsAlertDescription expected, DtlsClientHello hello)
    {
        var alert = Assert.ThrowsExactly<DtlsAlertException>(() => DtlsServerNegotiation.Negotiate(hello, UnitTestCertificates.EcdsaP256, true));
        Assert.AreEqual(expected, alert.Description);
    }

    private static DtlsClientHello Hello(ushort[] suites, IReadOnlyList<DtlsExtension> extensions) =>
        new(DtlsRecordHeader.Dtls12Version, Random, [], [], suites, [0], extensions);

    private static DtlsExtension Extension(DtlsExtensionType type, byte[] data) => new(type, data);

    private static List<DtlsExtension> Extensions(ushort[]? groups = null, ushort[]? schemes = null) =>
    [
        Extension(DtlsExtensionType.SupportedGroups, UnitTestDtlsClient.UInt16List(groups ?? [23])),
        Extension(DtlsExtensionType.EcPointFormats, [1, 0]),
        Extension(DtlsExtensionType.SignatureAlgorithms, UnitTestDtlsClient.UInt16List(schemes ?? [0x0403])),
        Extension(DtlsExtensionType.ExtendedMasterSecret, []),
        Extension(DtlsExtensionType.RenegotiationInfo, [0]),
    ];
}
