namespace Surl.TlsSrp;

/// <summary>ADR-0122 decisions 2 and 3: the version, the suite, and each extension the server reads or answers.</summary>
[TestClass]
public sealed class TlsSrpNegotiationTests
{
    [TestMethod]
    public void Negotiate_RecordedUpstreamCurlHello_ChoosesAes256AndAnswersEveryExtensionItOffers()
    {
        var hello = TlsClientHello.Parse(UnitTestFixtures.Fragments(UnitTestFixtures.Read("CurlTlsSrpJsmithAbc", "client.bin"))[0][4..]);

        var negotiation = TlsSrpNegotiation.Negotiate(hello);

        Assert.AreEqual(TlsSrpCipherSuite.SrpShaAes256CbcSha, negotiation.CipherSuite);
        CollectionAssert.AreEqual("jsmith"u8.ToArray(), negotiation.UserName);
        Assert.IsTrue(negotiation.ExtendedMasterSecret);
        Assert.IsTrue(negotiation.EncryptThenMac);
        Assert.IsTrue(negotiation.AnswersRenegotiationInfo);
        Assert.AreEqual("http/1.1", negotiation.ApplicationProtocol);
        Assert.IsNull(negotiation.ServerName);
    }

    [TestMethod]
    public void Negotiate_HelloWithOnlySrpAndNoSignalOfRenegotiation_AnswersNothingElse()
    {
        var negotiation = Negotiate(0x0303, [0xC01A], [0], UnitTestSrpClient.SrpExtension("jsmith"));

        Assert.AreEqual(TlsSrpCipherSuite.SrpSha3DesEdeCbcSha, negotiation.CipherSuite);
        Assert.IsFalse(negotiation.ExtendedMasterSecret);
        Assert.IsFalse(negotiation.EncryptThenMac);
        Assert.IsFalse(negotiation.AnswersRenegotiationInfo);
        Assert.IsNull(negotiation.ApplicationProtocol);
    }

    [TestMethod]
    public void Negotiate_EmptyRenegotiationSignalingSuite_AnswersRenegotiationInfo()
    {
        Assert.IsTrue(Negotiate(0x0303, [0x00FF, 0xC01D], [0], UnitTestSrpClient.SrpExtension("jsmith")).AnswersRenegotiationInfo);
    }

    [TestMethod]
    public void Negotiate_OldClientVersionWithSupportedVersionsListingTls12_IsServed()
    {
        var negotiation = Negotiate(0x0301, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), SupportedVersions("0304", "0303"));

        Assert.AreEqual(TlsSrpCipherSuite.SrpShaAes256CbcSha, negotiation.CipherSuite);
    }

    [TestMethod]
    public void Negotiate_OldClientVersionWithoutSupportedVersions_IsProtocolVersion()
    {
        AssertAlert(TlsAlertDescription.ProtocolVersion, 0x0302, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"));
    }

    [TestMethod]
    public void Negotiate_OldClientVersionWithSupportedVersionsWithoutTls12_IsProtocolVersion()
    {
        AssertAlert(TlsAlertDescription.ProtocolVersion, 0x0301, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), SupportedVersions("0304"));
    }

    [TestMethod]
    public void Negotiate_MalformedSupportedVersions_IsDecodeError()
    {
        AssertAlert(TlsAlertDescription.DecodeError, 0x0301, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), UnitTestSrpClient.Extension(TlsExtensionType.SupportedVersions, [2, 3, 3, 0]));
    }

    [TestMethod]
    public void Negotiate_NoNullCompression_IsIllegalParameter()
    {
        AssertAlert(TlsAlertDescription.IllegalParameter, 0x0303, [0xC020], [1], UnitTestSrpClient.SrpExtension("jsmith"));
    }

    [TestMethod]
    public void Negotiate_OnlySignedOrNonSrpSuites_IsHandshakeFailure()
    {
        AssertAlert(TlsAlertDescription.HandshakeFailure, 0x0303, [0x1301, 0xC021, 0xC022, 0xC01B, 0x002F], [0], UnitTestSrpClient.SrpExtension("jsmith"));
    }

    [TestMethod]
    public void Negotiate_NoSrpExtension_IsUnknownPskIdentity()
    {
        AssertAlert(TlsAlertDescription.UnknownPskIdentity, 0x0303, [0xC020], [0]);
    }

    [TestMethod]
    [DataRow("00", DisplayName = "empty user name")]
    [DataRow("016A00", DisplayName = "bytes after the user name")]
    [DataRow("02", DisplayName = "user name cut short")]
    public void Negotiate_MalformedSrpExtension_IsDecodeError(string data)
    {
        AssertAlert(TlsAlertDescription.DecodeError, 0x0303, [0xC020], [0], UnitTestSrpClient.Extension(TlsExtensionType.Srp, Convert.FromHexString(data)));
    }

    [TestMethod]
    public void Negotiate_NonEmptyRenegotiationInfo_IsHandshakeFailure()
    {
        AssertAlert(
            TlsAlertDescription.HandshakeFailure, 0x0303, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), UnitTestSrpClient.Extension(TlsExtensionType.RenegotiationInfo, [1, 7]));
    }

    [TestMethod]
    public void Negotiate_AlpnWithoutHttp11_IsNoApplicationProtocol()
    {
        AssertAlert(TlsAlertDescription.NoApplicationProtocol, 0x0303, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), Alpn([2, .. "h2"u8]));
    }

    [TestMethod]
    [DataRow("0004026832", DisplayName = "list longer than its data")]
    [DataRow("000100", DisplayName = "empty protocol name")]
    [DataRow("000908687474702F312E3100", DisplayName = "bytes after the list")]
    public void Negotiate_MalformedAlpn_IsDecodeError(string data)
    {
        AssertAlert(
            TlsAlertDescription.DecodeError, 0x0303, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), UnitTestSrpClient.Extension(TlsExtensionType.ApplicationLayerProtocolNegotiation, Convert.FromHexString(data)));
    }

    [TestMethod]
    public void Negotiate_ServerNameAfterAnotherNameType_KeepsTheHostName()
    {
        byte[] list = [9, .. UnitTestSrpClient.Vector16([1, 2]), 0, .. UnitTestSrpClient.Vector16("srp.surl.test"u8.ToArray()), 0, .. UnitTestSrpClient.Vector16("other.test"u8.ToArray())];

        var negotiation = Negotiate(0x0303, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), UnitTestSrpClient.Extension(TlsExtensionType.ServerName, UnitTestSrpClient.Vector16(list)));

        Assert.AreEqual("srp.surl.test", negotiation.ServerName);
    }

    [TestMethod]
    public void Negotiate_ServerNameWithOnlyAnotherNameType_HasNoServerName()
    {
        byte[] list = [9, .. UnitTestSrpClient.Vector16([1, 2])];

        var negotiation = Negotiate(0x0303, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), UnitTestSrpClient.Extension(TlsExtensionType.ServerName, UnitTestSrpClient.Vector16(list)));

        Assert.IsNull(negotiation.ServerName);
    }

    [TestMethod]
    public void Negotiate_MalformedServerName_IsDecodeError()
    {
        AssertAlert(
            TlsAlertDescription.DecodeError, 0x0303, [0xC020], [0], UnitTestSrpClient.SrpExtension("jsmith"), UnitTestSrpClient.Extension(TlsExtensionType.ServerName, [0, 1, 0, 0]));
    }

    [TestMethod]
    public void Negotiate_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrpNegotiation.Negotiate(null!));
    }

    [TestMethod]
    public void Negotiate_RecordedUpstreamCurlHelloAndAnRsaCertificate_ChoosesRsaAes256SignedWithRsaPssSha256()
    {
        var hello = RecordedHello();

        var negotiation = TlsSrpNegotiation.Negotiate(hello, UnitTestCertificates.RsaCertificate());

        Assert.AreEqual(TlsSrpCipherSuite.SrpShaRsaAes256CbcSha, negotiation.CipherSuite);
        Assert.AreEqual(TlsSignatureScheme.RsaPssRsaeSha256, negotiation.SignatureScheme);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public void Negotiate_RecordedUpstreamCurlHelloAndADsaCertificate_ChoosesDssAes256SignedWithDsaSha256()
    {
        var negotiation = TlsSrpNegotiation.Negotiate(RecordedHello(), UnitTestCertificates.DsaCertificate());

        Assert.AreEqual(TlsSrpCipherSuite.SrpShaDssAes256CbcSha, negotiation.CipherSuite);
        Assert.AreEqual(TlsSignatureScheme.DsaSha256, negotiation.SignatureScheme);
    }

    [TestMethod]
    public void Negotiate_RecordedUpstreamCurlHelloWithoutACertificate_StillChoosesTheUnsignedSuite()
    {
        var negotiation = TlsSrpNegotiation.Negotiate(RecordedHello());

        Assert.AreEqual(TlsSrpCipherSuite.SrpShaAes256CbcSha, negotiation.CipherSuite);
        Assert.IsNull(negotiation.SignatureScheme);
    }

    [TestMethod]
    public void Negotiate_CertificateAndAnUnsignedSuiteFirst_KeepsTheClientsOrderAndSignsNothing()
    {
        var negotiation = Negotiate(0x0303, [0xC01D, 0xC021], [0], UnitTestCertificates.RsaCertificate(), UnitTestSrpClient.SrpExtension("jsmith"));

        Assert.AreEqual(TlsSrpCipherSuite.SrpShaAes128CbcSha, negotiation.CipherSuite);
        Assert.IsNull(negotiation.SignatureScheme);
    }

    [TestMethod]
    public void Negotiate_CertificateAndNoSignatureAlgorithmsExtension_SignsWithSha1()
    {
        var negotiation = Negotiate(0x0303, [0xC01E], [0], UnitTestCertificates.RsaCertificate(), UnitTestSrpClient.SrpExtension("jsmith"));

        Assert.AreEqual(TlsSrpCipherSuite.SrpShaRsaAes128CbcSha, negotiation.CipherSuite);
        Assert.AreEqual(TlsSignatureScheme.RsaPkcs1Sha1, negotiation.SignatureScheme);
    }

    [TestMethod]
    public void Negotiate_SignedSuiteOfTheOtherKeyKind_SendsHandshakeFailure()
    {
        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => Negotiate(0x0303, [0xC022], [0], UnitTestCertificates.RsaCertificate(), UnitTestSrpClient.SrpExtension("jsmith")));

        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, alert.Description);
    }

    [TestMethod]
    public void Negotiate_SignedSuiteButNoSchemeTheKeyCanMake_SendsHandshakeFailure()
    {
        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => Negotiate(0x0303, [0xC021], [0], UnitTestCertificates.RsaCertificate(), UnitTestSrpClient.SrpExtension("jsmith"), SignatureAlgorithms(0x0403)));

        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, alert.Description);
    }

    [TestMethod]
    public void Negotiate_EmptySignatureAlgorithmsList_SendsDecodeError()
    {
        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => Negotiate(0x0303, [0xC021], [0], UnitTestCertificates.RsaCertificate(), UnitTestSrpClient.SrpExtension("jsmith"), SignatureAlgorithms()));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    [TestMethod]
    public void Negotiate_SignatureAlgorithmsWithTrailingBytes_SendsDecodeError()
    {
        var extension = UnitTestSrpClient.Extension(TlsExtensionType.SignatureAlgorithms, [0, 2, 8, 4, 0]);

        var alert = Assert.ThrowsExactly<TlsAlertException>(
            () => Negotiate(0x0303, [0xC021], [0], UnitTestCertificates.RsaCertificate(), UnitTestSrpClient.SrpExtension("jsmith"), extension));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    private static TlsClientHello RecordedHello() =>
        TlsClientHello.Parse(UnitTestFixtures.Fragments(UnitTestFixtures.Read("CurlTlsSrpJsmithAbc", "client.bin"))[0][4..]);

    private static byte[] SignatureAlgorithms(params ushort[] schemes) =>
        UnitTestSrpClient.Extension(
            TlsExtensionType.SignatureAlgorithms,
            UnitTestSrpClient.Vector16([.. schemes.SelectMany(scheme => new[] { (byte)(scheme >> 8), (byte)scheme })]));

    private static TlsSrpNegotiation Negotiate(
        ushort version, ushort[] suites, byte[] compression, TlsSrpServerCertificate certificate, params byte[][] extensions) =>
        TlsSrpNegotiation.Negotiate(TlsClientHello.Parse(UnitTestSrpClient.ClientHelloBody(version, suites, compression, extensions)), certificate);

    private static TlsSrpNegotiation Negotiate(ushort version, ushort[] suites, byte[] compression, params byte[][] extensions) =>
        TlsSrpNegotiation.Negotiate(TlsClientHello.Parse(UnitTestSrpClient.ClientHelloBody(version, suites, compression, extensions)));

    private static void AssertAlert(TlsAlertDescription expected, ushort version, ushort[] suites, byte[] compression, params byte[][] extensions)
    {
        var alert = Assert.ThrowsExactly<TlsAlertException>(() => Negotiate(version, suites, compression, extensions));

        Assert.AreEqual(expected, alert.Description);
    }

    private static byte[] SupportedVersions(params string[] versions) =>
        UnitTestSrpClient.Extension(TlsExtensionType.SupportedVersions, UnitTestSrpClient.Vector8(Convert.FromHexString(string.Concat(versions))));

    private static byte[] Alpn(byte[] list) =>
        UnitTestSrpClient.Extension(TlsExtensionType.ApplicationLayerProtocolNegotiation, UnitTestSrpClient.Vector16(list));
}
