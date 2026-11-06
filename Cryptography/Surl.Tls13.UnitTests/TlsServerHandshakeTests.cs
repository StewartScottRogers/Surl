using System.Security.Cryptography;

namespace Surl.Tls13;

[TestClass]
public sealed class TlsServerHandshakeTests
{
    private static readonly byte[] HelloRetryRequestRandom = Convert.FromHexString(
        "cf21ad74e59a6111be1d8c021e65b891c2a211167abb8c5e079e09e2c8a8339c");

    /// <summary>
    /// EncryptedExtensions (RFC 8446 section 4.3.1) carrying ALPN h3 (RFC 7301 section 3.1) and
    /// quic_transport_parameters 0f01ab (RFC 9001 section 8.2), written from those sections.
    /// </summary>
    private static readonly byte[] EncryptedExtensionsWithH3AndTransportParameters = Convert.FromHexString(
        "08000012" + "0010" + "001000050003026833" + "003900030f01ab");

    [TestMethod]
    public void Receive_Rfc8448Section3ClientHello_WritesTheTracesServerHelloEncryptedExtensionsCertificateAndHandshakeSecrets()
    {
        using var key = RSA.Create(Rfc8448.ServerRsaKey);
        using var handshake = Rfc8448Handshake(key);

        var output = handshake.Receive(TlsEncryptionLevel.Initial, Rfc8448.ClientHello);

        CollectionAssert.AreEqual(Rfc8448.ServerHello, output.Initial);
        byte[] expectedStart = [.. Rfc8448.EncryptedExtensions, .. Rfc8448.Certificate];
        CollectionAssert.AreEqual(expectedStart, output.Handshake[..expectedStart.Length]);
        CollectionAssert.AreEqual(Rfc8448.ClientHandshakeTrafficSecret, output.HandshakeSecrets!.Client);
        CollectionAssert.AreEqual(Rfc8448.ServerHandshakeTrafficSecret, output.HandshakeSecrets.Server);
        Assert.AreEqual(TlsCipherSuite.Aes128GcmSha256, handshake.CipherSuite);
        Assert.AreEqual(TlsNamedGroup.X25519, handshake.KeyExchangeGroup);
        Assert.IsNull(handshake.ApplicationProtocol);
    }

    [TestMethod]
    public void Receive_Rfc8448Section3ClientHello_SignsCertificateVerifyWithRsaPssAndFinishesAsTheClientVerifies()
    {
        using var key = RSA.Create(Rfc8448.ServerRsaKey);
        using var handshake = Rfc8448Handshake(key);
        var client = UnitTestTlsClient.WithRfc8448ClientShare();

        var output = handshake.Receive(TlsEncryptionLevel.Initial, Rfc8448.ClientHello);
        var verified = client.VerifyServerFlight([Rfc8448.ClientHello], output.Initial, output.Handshake);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0x04 }, verified.CertificateVerify[4..6]);
        CollectionAssert.AreEqual(Rfc8448.ClientHandshakeTrafficSecret, verified.HandshakeSecrets.Client);
        CollectionAssert.AreEqual(verified.ApplicationSecrets.Client, output.ApplicationSecrets!.Client);
        CollectionAssert.AreEqual(verified.ApplicationSecrets.Server, output.ApplicationSecrets.Server);
    }

    [TestMethod]
    public void Receive_Rfc8448Section3ClientFinishedForTheFlight_CompletesTheHandshake()
    {
        using var key = RSA.Create(Rfc8448.ServerRsaKey);
        using var handshake = Rfc8448Handshake(key);
        var output = handshake.Receive(TlsEncryptionLevel.Initial, Rfc8448.ClientHello);
        var verified = UnitTestTlsClient.WithRfc8448ClientShare().VerifyServerFlight([Rfc8448.ClientHello], output.Initial, output.Handshake);

        var finishedOutput = handshake.Receive(TlsEncryptionLevel.Handshake, verified.ClientFinished);

        Assert.IsTrue(handshake.IsComplete);
        Assert.IsEmpty(finishedOutput.Initial);
        Assert.IsEmpty(finishedOutput.Handshake);
        Assert.IsNull(finishedOutput.HandshakeSecrets);
        Assert.IsNull(finishedOutput.ApplicationSecrets);
    }

    [TestMethod]
    [DataRow("static-8.21.0-win.bin")]
    [DataRow("curlse-8.22.0_2-win.bin")]
    [DataRow("static-8.21.0-linux.bin")]
    public void Receive_RecordedUpstreamCurlClientHello_CompletesAHandshakeTheTestClientVerifies(string fixture)
    {
        using var key = RSA.Create(2048);
        var options = UnitTestCertificates.QuicOptions(key);
        using var handshake = new TlsServerHandshake(options, RandomNumberGenerator.Create());
        var recorded = UnitTestFixtures.Read(fixture);
        var recordedShare = ClientHello.Read(recorded).KeyShares!.Single(share => share.Group == TlsNamedGroup.X25519);
        var client = UnitTestTlsClient.WithX25519Share();
        var clientHello = client.WithShareReplaced(recorded, recordedShare.KeyExchange.Span);

        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);
        var verified = client.VerifyServerFlight([clientHello], output.Initial, output.Handshake);
        handshake.Receive(TlsEncryptionLevel.Handshake, verified.ClientFinished);

        Assert.IsTrue(handshake.IsComplete);
        Assert.AreEqual((ushort)0x1302, verified.CipherSuite);
        Assert.AreEqual("h3", handshake.ApplicationProtocol);
        CollectionAssert.AreEqual(EncryptedExtensionsWithH3AndTransportParameters, verified.EncryptedExtensions);
        CollectionAssert.AreEqual(verified.HandshakeSecrets.Client, output.HandshakeSecrets!.Client);
        CollectionAssert.AreEqual(verified.HandshakeSecrets.Server, output.HandshakeSecrets.Server);
        CollectionAssert.AreEqual(verified.ApplicationSecrets.Client, output.ApplicationSecrets!.Client);
        CollectionAssert.AreEqual(verified.ApplicationSecrets.Server, output.ApplicationSecrets.Server);
        Assert.AreEqual(TlsNamedGroup.X25519, handshake.KeyExchangeGroup);
        CollectionAssert.AreEqual(ClientHello.Read(clientHello).QuicTransportParameters, handshake.ClientHello!.QuicTransportParameters);
    }

    [TestMethod]
    public void Receive_KeyShareForAnUnsupportedGroup_SendsAHelloRetryRequestThenFinishesOnTheSecondClientHello()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var handshake = new TlsServerHandshake(UnitTestCertificates.QuicOptions(key), RandomNumberGenerator.Create());
        var clientHello1 = new UnitTestClientHelloBuilder
        {
            SupportedGroups = [0x001E, 0x0017],
            KeyShares = [(0x001E, new byte[56])],
        }.Write();
        var client = UnitTestTlsClient.WithEllipticCurveShare(0x0017, ECCurve.NamedCurves.nistP256);
        var clientHello2 = new UnitTestClientHelloBuilder
        {
            SupportedGroups = [0x001E, 0x0017],
            KeyShares = [(0x0017, client.PublicShare)],
        }.Write();
        byte[] expectedRetry =
        [
            .. Convert.FromHexString("020000340303"), .. HelloRetryRequestRandom,
            .. Convert.FromHexString("00130100000c003300020017002b00020304"),
        ];

        var retry = handshake.Receive(TlsEncryptionLevel.Initial, clientHello1);
        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello2);
        var verified = client.VerifyServerFlight(
            [UnitTestTlsClient.MessageHash(HashAlgorithmName.SHA256, clientHello1), retry.Initial, clientHello2],
            output.Initial,
            output.Handshake);
        handshake.Receive(TlsEncryptionLevel.Handshake, verified.ClientFinished);

        CollectionAssert.AreEqual(expectedRetry, retry.Initial);
        Assert.IsEmpty(retry.Handshake);
        Assert.IsNull(retry.HandshakeSecrets);
        Assert.IsTrue(handshake.IsComplete);
        Assert.AreEqual(TlsNamedGroup.Secp256r1, handshake.KeyExchangeGroup);
    }

    [TestMethod]
    public void Receive_NoKeyShareExtension_SendsAHelloRetryRequestForTheFirstSupportedGroupTheServerPrefers()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var handshake = new TlsServerHandshake(UnitTestCertificates.QuicOptions(key), RandomNumberGenerator.Create());
        var clientHello = new UnitTestClientHelloBuilder { SupportedGroups = [0x0018, 0x001D] }.Write();

        var retry = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);

        CollectionAssert.AreEqual(Convert.FromHexString("00330002001d"), retry.Initial[44..50]);
        Assert.IsFalse(handshake.IsComplete);
    }

    [TestMethod]
    [DataRow(0x0017, 0x0403)]
    [DataRow(0x0018, 0x0503)]
    [DataRow(0x0019, 0x0603)]
    public void Receive_NistCurveShareAndEcdsaKey_CompletesWithTheCurvesSignatureScheme(int group, int scheme)
    {
        var curve = UnitTestTlsClient.CurveOf((ushort)group);
        using var key = ECDsa.Create(curve);
        using var handshake = new TlsServerHandshake(UnitTestCertificates.QuicOptions(key), RandomNumberGenerator.Create());
        var client = UnitTestTlsClient.WithEllipticCurveShare((ushort)group, curve);
        var clientHello = new UnitTestClientHelloBuilder
        {
            CipherSuites = [0x1303],
            SupportedGroups = [(ushort)group],
            KeyShares = [((ushort)group, client.PublicShare)],
        }.Write();

        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);
        var verified = client.VerifyServerFlight([clientHello], output.Initial, output.Handshake);
        handshake.Receive(TlsEncryptionLevel.Handshake, verified.ClientFinished);

        Assert.IsTrue(handshake.IsComplete);
        Assert.AreEqual(TlsCipherSuite.ChaCha20Poly1305Sha256, handshake.CipherSuite);
        Assert.AreEqual(scheme, (verified.CertificateVerify[4] << 8) | verified.CertificateVerify[5]);
    }

    [TestMethod]
    public void Receive_ClientHelloInPieces_AnswersOnceTheMessageIsWhole()
    {
        using var handshake = EcdsaHandshake();
        var client = UnitTestTlsClient.WithX25519Share();
        var clientHello = X25519ClientHello(client);

        var first = handshake.Receive(TlsEncryptionLevel.Initial, clientHello.AsSpan(0, 2));
        var second = handshake.Receive(TlsEncryptionLevel.Initial, clientHello.AsSpan(2, 40));
        var third = handshake.Receive(TlsEncryptionLevel.Initial, clientHello.AsSpan(42));

        Assert.IsEmpty(first.Initial);
        Assert.IsEmpty(second.Initial);
        Assert.IsNotEmpty(third.Initial);
        Assert.IsNotEmpty(third.Handshake);
    }

    [TestMethod]
    public void Receive_ClientHelloWithoutH3_FailsWithNoApplicationProtocol()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder
        {
            ApplicationProtocols = ["h3-29"],
            KeyShares = [(0x001D, UnitTestTlsClient.WithX25519Share().PublicShare)],
        }.Write();

        AssertAlert(TlsAlert.NoApplicationProtocol, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    [DataRow("doq", "00100006000403646f71")]
    [DataRow("h3", "001000050003026833")]
    public void Receive_OptionsNameTheProtocolTheClientOffers_AgreesItAndSelectsItInEncryptedExtensions(string protocol, string alpnExtensionHex)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = UnitTestCertificates.QuicOptions(key);
        using var handshake = new TlsServerHandshake(
            new TlsServerHandshakeOptions
            {
                CertificateChain = options.CertificateChain,
                CertificateKey = key,
                ApplicationProtocols = [protocol],
                QuicTransportParameters = options.QuicTransportParameters,
            },
            RandomNumberGenerator.Create());
        var client = UnitTestTlsClient.WithX25519Share();
        var clientHello = new UnitTestClientHelloBuilder
        {
            ApplicationProtocols = ["hq-interop", protocol],
            KeyShares = [(0x001D, client.PublicShare)],
        }.Write();

        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);
        var verified = client.VerifyServerFlight([clientHello], output.Initial, output.Handshake);

        Assert.AreEqual(protocol, handshake.ApplicationProtocol);
        Assert.IsGreaterThanOrEqualTo(0, verified.EncryptedExtensions.AsSpan().IndexOf(Convert.FromHexString(alpnExtensionHex)));
    }

    [TestMethod]
    public void Receive_OptionsNameDoqAndTheClientOffersOnlyH3_FailsWithNoApplicationProtocol()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = UnitTestCertificates.QuicOptions(key);
        using var handshake = new TlsServerHandshake(
            new TlsServerHandshakeOptions { CertificateChain = options.CertificateChain, CertificateKey = key, ApplicationProtocols = ["doq"] },
            RandomNumberGenerator.Create());
        var clientHello = new UnitTestClientHelloBuilder { KeyShares = [(0x001D, UnitTestTlsClient.WithX25519Share().PublicShare)] }.Write();

        AssertAlert(TlsAlert.NoApplicationProtocol, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_ClientHelloWithoutAlpn_FailsWithNoApplicationProtocol()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { ApplicationProtocols = null }.Write();

        AssertAlert(TlsAlert.NoApplicationProtocol, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_NoSharedCipherSuite_FailsWithHandshakeFailure()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { CipherSuites = [0x1304, 0x00FF] }.Write();

        AssertAlert(TlsAlert.HandshakeFailure, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_NoSignatureSchemeForTheKey_FailsWithHandshakeFailure()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { SignatureAlgorithms = [0x0804, 0x0503] }.Write();

        AssertAlert(TlsAlert.HandshakeFailure, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_NoSupportedGroupTheServerServes_FailsWithHandshakeFailure()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { SupportedGroups = [0x001E], KeyShares = [(0x001E, new byte[56])] }.Write();

        AssertAlert(TlsAlert.HandshakeFailure, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_ClientFinishedThatDoesNotVerify_FailsWithDecryptError()
    {
        using var handshake = EcdsaHandshake();
        var client = UnitTestTlsClient.WithX25519Share();
        var clientHello = X25519ClientHello(client);
        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);
        var finished = client.VerifyServerFlight([clientHello], output.Initial, output.Handshake).ClientFinished;
        finished[^1] ^= 0x01;

        AssertAlert(TlsAlert.DecryptError, () => handshake.Receive(TlsEncryptionLevel.Handshake, finished));
        Assert.IsFalse(handshake.IsComplete);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(new[] { (ushort)0x0303 })]
    public void Receive_ClientHelloNotOfferingTls13_FailsWithProtocolVersion(ushort[]? versions)
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { SupportedVersions = versions }.Write();

        AssertAlert(TlsAlert.ProtocolVersion, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_NoSupportedGroups_FailsWithMissingExtension()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { SupportedGroups = null }.Write();

        AssertAlert(TlsAlert.MissingExtension, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_NoSignatureAlgorithms_FailsWithMissingExtension()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { SignatureAlgorithms = null }.Write();

        AssertAlert(TlsAlert.MissingExtension, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_NoQuicTransportParameters_FailsWithMissingExtension()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { QuicTransportParameters = null }.Write();

        AssertAlert(TlsAlert.MissingExtension, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_SecondClientHelloChangingTheCipherSuite_FailsWithIllegalParameter()
    {
        using var handshake = EcdsaHandshake();
        handshake.Receive(TlsEncryptionLevel.Initial, new UnitTestClientHelloBuilder().Write());
        var clientHello2 = new UnitTestClientHelloBuilder
        {
            CipherSuites = [0x1302],
            KeyShares = [(0x001D, UnitTestTlsClient.WithX25519Share().PublicShare)],
        }.Write();

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello2));
    }

    [TestMethod]
    [DataRow(false, 0x0017)]
    [DataRow(true, 0x001D)]
    public void Receive_SecondClientHelloWithoutOneShareForTheRetryGroup_FailsWithIllegalParameter(bool twoShares, int group)
    {
        using var handshake = EcdsaHandshake();
        handshake.Receive(TlsEncryptionLevel.Initial, new UnitTestClientHelloBuilder { SupportedGroups = [0x001D, 0x0017] }.Write());
        var shares = new List<(ushort, byte[])> { ((ushort)group, UnitTestTlsClient.WithX25519Share().PublicShare) };
        if (twoShares)
        {
            shares.Add((0x0017, UnitTestTlsClient.WithEllipticCurveShare(0x0017, ECCurve.NamedCurves.nistP256).PublicShare));
        }

        var clientHello2 = new UnitTestClientHelloBuilder { SupportedGroups = [0x001D, 0x0017], KeyShares = [.. shares] }.Write();

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello2));
    }

    [TestMethod]
    public void Receive_SecondClientHelloWithoutKeyShare_FailsWithIllegalParameter()
    {
        using var handshake = EcdsaHandshake();
        handshake.Receive(TlsEncryptionLevel.Initial, new UnitTestClientHelloBuilder().Write());

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, new UnitTestClientHelloBuilder().Write()));
    }

    [TestMethod]
    [DataRow(0x001D, 31)]
    [DataRow(0x0017, 64)]
    [DataRow(0x0018, 96)]
    public void Receive_KeyShareOfTheWrongLength_FailsWithIllegalParameter(int group, int length)
    {
        using var handshake = EcdsaHandshake();
        var share = new byte[length];
        share[0] = 0x04;
        var clientHello = new UnitTestClientHelloBuilder { SupportedGroups = [(ushort)group], KeyShares = [((ushort)group, share)] }.Write();

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_EllipticCurveShareNotUncompressed_FailsWithIllegalParameter()
    {
        using var handshake = EcdsaHandshake();
        var share = UnitTestTlsClient.WithEllipticCurveShare(0x0017, ECCurve.NamedCurves.nistP256).PublicShare;
        share[0] = 0x02;
        var clientHello = new UnitTestClientHelloBuilder { SupportedGroups = [0x0017], KeyShares = [(0x0017, share)] }.Write();

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    [DataRow((byte)0x01, (byte)0x01)]
    [DataRow((byte)0xFF, (byte)0x01)]
    [DataRow((byte)0x01, (byte)0xFF)]
    public void Receive_EllipticCurveShareNotOnTheCurve_FailsWithIllegalParameter(byte xFill, byte yFill)
    {
        using var handshake = EcdsaHandshake();
        byte[] share = [0x04, .. Enumerable.Repeat(xFill, 32), .. Enumerable.Repeat(yFill, 32)];
        var clientHello = new UnitTestClientHelloBuilder { SupportedGroups = [0x0017], KeyShares = [(0x0017, share)] }.Write();

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_X25519ShareOfALowOrderPoint_FailsWithIllegalParameter()
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { KeyShares = [(0x001D, new byte[32])] }.Write();

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }

    [TestMethod]
    public void Receive_MessageLongerThanTheLimit_FailsWithIllegalParameter()
    {
        using var handshake = EcdsaHandshake();
        var length = TlsServerHandshake.MaximumMessageLength + 1;

        AssertAlert(
            TlsAlert.IllegalParameter,
            () => handshake.Receive(TlsEncryptionLevel.Initial, [1, (byte)(length >> 16), (byte)(length >> 8), (byte)length]));
    }

    [TestMethod]
    public void Receive_BytesAtTheApplicationLevel_FailWithUnexpectedMessage()
    {
        using var handshake = EcdsaHandshake();

        AssertAlert(TlsAlert.UnexpectedMessage, () => handshake.Receive(TlsEncryptionLevel.Application, [20, 0, 0, 0]));
    }

    [TestMethod]
    public void Receive_HandshakeLevelMessageBeforeTheClientHello_FailsWithUnexpectedMessage()
    {
        using var handshake = EcdsaHandshake();

        AssertAlert(TlsAlert.UnexpectedMessage, () => handshake.Receive(TlsEncryptionLevel.Handshake, [20, 0, 0, 0]));
    }

    [TestMethod]
    public void Receive_MessageOtherThanFinishedAfterTheServerFlight_FailsWithUnexpectedMessage()
    {
        using var handshake = EcdsaHandshake();
        handshake.Receive(TlsEncryptionLevel.Initial, X25519ClientHello(UnitTestTlsClient.WithX25519Share()));

        AssertAlert(TlsAlert.UnexpectedMessage, () => handshake.Receive(TlsEncryptionLevel.Handshake, [5, 0, 0, 0]));
    }

    [TestMethod]
    public void Receive_MessageAfterTheHandshakeCompleted_FailsWithUnexpectedMessage()
    {
        using var handshake = EcdsaHandshake();
        var client = UnitTestTlsClient.WithX25519Share();
        var clientHello = X25519ClientHello(client);
        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);
        handshake.Receive(TlsEncryptionLevel.Handshake, client.VerifyServerFlight([clientHello], output.Initial, output.Handshake).ClientFinished);

        AssertAlert(TlsAlert.UnexpectedMessage, () => handshake.Receive(TlsEncryptionLevel.Handshake, [20, 0, 0, 0]));
    }

    [TestMethod]
    public void Receive_AfterAnAlert_ThrowsInvalidOperationException()
    {
        using var handshake = EcdsaHandshake();
        AssertAlert(TlsAlert.UnexpectedMessage, () => handshake.Receive(TlsEncryptionLevel.Application, [20, 0, 0, 0]));

        Assert.ThrowsExactly<InvalidOperationException>(() => handshake.Receive(TlsEncryptionLevel.Initial, []));
    }

    [TestMethod]
    public void Properties_BeforeAnyClientHello_AreUnset()
    {
        using var handshake = EcdsaHandshake();

        Assert.IsNull(handshake.CipherSuite);
        Assert.IsNull(handshake.ApplicationProtocol);
        Assert.IsNull(handshake.KeyExchangeGroup);
        Assert.IsNull(handshake.ClientHello);
        Assert.IsNull(handshake.ClientCertificateChain);
        Assert.IsFalse(handshake.IsComplete);
    }

    [TestMethod]
    public void Receive_ClientCertificateRequested_SendsCertificateRequestBetweenEncryptedExtensionsAndCertificate()
    {
        // RFC 8446 section 4.3.2: an empty certificate_request_context, then signature_algorithms
        // (0x000d) listing ecdsa_secp256r1_sha256, ecdsa_secp384r1_sha384, ecdsa_secp521r1_sha512,
        // rsa_pss_rsae_sha256, rsa_pss_rsae_sha384 and rsa_pss_rsae_sha512.
        var expected = Convert.FromHexString("0d000015" + "00" + "0012" + "000d000e000c" + "040305030603080408050806");

        var (handshake, flight) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;

        CollectionAssert.AreEqual(expected, flight.CertificateRequest);
        Assert.IsFalse(handshake.IsComplete);
    }

    [TestMethod]
    [DataRow(0x0403)]
    [DataRow(0x0503)]
    [DataRow(0x0603)]
    [DataRow(0x0804)]
    [DataRow(0x0805)]
    [DataRow(0x0806)]
    public void Receive_ClientCertificateCertificateVerifyAndFinished_CompleteTheHandshake(int scheme)
    {
        using var clientKey = ClientKeyFor(scheme);
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leaf = UnitTestCertificates.SelfSigned(clientKey);
        var issuer = UnitTestCertificates.SelfSigned(issuerKey);
        IReadOnlyList<ReadOnlyMemory<byte>>? trusted = null;
        var (handshake, flight) = ClientCertificateHandshake(chain => (trusted = chain) is not null);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([], leaf, issuer);
        var verify = UnitTestClientAuthentication.CertificateVerify(
            (ushort)scheme,
            UnitTestClientAuthentication.Sign(clientKey, (ushort)scheme, UnitTestClientAuthentication.SignedContent(flight, certificate)));

        handshake.Receive(TlsEncryptionLevel.Handshake, [.. certificate, .. verify, .. UnitTestClientAuthentication.Finished(flight, certificate, verify)]);

        Assert.IsTrue(handshake.IsComplete);
        Assert.IsNotNull(trusted);
        Assert.HasCount(2, handshake.ClientCertificateChain!);
        CollectionAssert.AreEqual(leaf, handshake.ClientCertificateChain![0].ToArray());
        CollectionAssert.AreEqual(issuer, handshake.ClientCertificateChain[1].ToArray());
    }

    [TestMethod]
    public void Receive_EmptyClientCertificateWhenOneIsRequired_FailsWithCertificateRequired()
    {
        var (handshake, _) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;

        AssertAlert(
            TlsAlert.CertificateRequired,
            () => handshake.Receive(TlsEncryptionLevel.Handshake, UnitTestClientAuthentication.CertificateMessage([])));
    }

    [TestMethod]
    public void Receive_ClientCertificateVerifyThatDoesNotVerify_FailsWithDecryptError()
    {
        using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handshake, flight) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([], UnitTestCertificates.SelfSigned(clientKey));
        var content = UnitTestClientAuthentication.SignedContent(flight, certificate);
        content[^1] ^= 0x01;
        var verify = UnitTestClientAuthentication.CertificateVerify(0x0403, UnitTestClientAuthentication.Sign(clientKey, 0x0403, content));

        AssertAlert(TlsAlert.DecryptError, () => handshake.Receive(TlsEncryptionLevel.Handshake, [.. certificate, .. verify]));
        Assert.IsNull(handshake.ClientCertificateChain);
    }

    [TestMethod]
    [DataRow(false, 256, 0x0804)]
    [DataRow(true, 0, 0x0403)]
    [DataRow(false, 384, 0x0403)]
    public void Receive_ClientCertificateVerifySchemeNotForTheCertificatesKey_FailsWithDecryptError(bool rsa, int curveBits, int scheme)
    {
        using AsymmetricAlgorithm clientKey = rsa
            ? RSA.Create(2048)
            : ECDsa.Create(curveBits == 256 ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384);
        var (handshake, flight) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([], UnitTestCertificates.SelfSigned(clientKey));
        var verify = UnitTestClientAuthentication.CertificateVerify(
            (ushort)scheme,
            UnitTestClientAuthentication.Sign(clientKey, (ushort)scheme, UnitTestClientAuthentication.SignedContent(flight, certificate)));

        AssertAlert(TlsAlert.DecryptError, () => handshake.Receive(TlsEncryptionLevel.Handshake, [.. certificate, .. verify]));
    }

    [TestMethod]
    [DataRow(0x0401)]
    [DataRow(0x0807)]
    public void Receive_ClientCertificateVerifySchemeTheCertificateRequestDidNotOffer_FailsWithIllegalParameter(int scheme)
    {
        using var clientKey = RSA.Create(2048);
        var (handshake, _) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([], UnitTestCertificates.SelfSigned(clientKey));
        var verify = UnitTestClientAuthentication.CertificateVerify((ushort)scheme, new byte[256]);

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Handshake, [.. certificate, .. verify]));
    }

    [TestMethod]
    public void Receive_ClientCertificateWithACertificateRequestContext_FailsWithIllegalParameter()
    {
        using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handshake, _) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([0x01], UnitTestCertificates.SelfSigned(clientKey));

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Handshake, certificate));
    }

    [TestMethod]
    public void Receive_ClientCertificateThatIsNotX509_FailsWithBadCertificate()
    {
        var (handshake, _) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;

        AssertAlert(
            TlsAlert.BadCertificate,
            () => handshake.Receive(TlsEncryptionLevel.Handshake, UnitTestClientAuthentication.CertificateMessage([], [0x30, 0x03, 0x01, 0x01, 0xFF])));
    }

    [TestMethod]
    public void Receive_ClientCertificateChainNotTrusted_FailsWithBadCertificate()
    {
        using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handshake, _) = ClientCertificateHandshake(_ => false);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([], UnitTestCertificates.SelfSigned(clientKey));

        AssertAlert(TlsAlert.BadCertificate, () => handshake.Receive(TlsEncryptionLevel.Handshake, certificate));
        Assert.IsNull(handshake.ClientCertificateChain);
    }

    [TestMethod]
    public void Receive_ClientCertificateWithTrailingBytes_FailsWithDecodeError()
    {
        using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handshake, _) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([], UnitTestCertificates.SelfSigned(clientKey));
        byte[] withTrailingByte = [.. certificate, 0x00];
        withTrailingByte[3]++;
        if (withTrailingByte[3] == 0)
        {
            withTrailingByte[2]++;
        }

        AssertAlert(TlsAlert.DecodeError, () => handshake.Receive(TlsEncryptionLevel.Handshake, withTrailingByte));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Receive_FinishedBeforeTheClientCertificateOrItsCertificateVerify_FailsWithUnexpectedMessage(bool afterCertificate)
    {
        using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handshake, flight) = ClientCertificateHandshake(_ => true);
        using var disposing = handshake;
        var certificate = UnitTestClientAuthentication.CertificateMessage([], UnitTestCertificates.SelfSigned(clientKey));
        byte[] bytes = afterCertificate ? [.. certificate, .. flight.ClientFinished] : flight.ClientFinished;

        AssertAlert(TlsAlert.UnexpectedMessage, () => handshake.Receive(TlsEncryptionLevel.Handshake, bytes));
    }

    private static (TlsServerHandshake Handshake, UnitTestVerifiedFlight Flight) ClientCertificateHandshake(
        Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool> trustsClientCertificateChain)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var quic = UnitTestCertificates.QuicOptions(key);
        var options = new TlsServerHandshakeOptions
        {
            CertificateChain = quic.CertificateChain,
            CertificateKey = key,
            ApplicationProtocols = quic.ApplicationProtocols,
            QuicTransportParameters = quic.QuicTransportParameters,
            TrustsClientCertificateChain = trustsClientCertificateChain,
        };
        var handshake = new TlsServerHandshake(options, RandomNumberGenerator.Create());
        var client = UnitTestTlsClient.WithX25519Share();
        var clientHello = X25519ClientHello(client);
        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);
        return (handshake, client.VerifyServerFlight([clientHello], output.Initial, output.Handshake));
    }

    private static AsymmetricAlgorithm ClientKeyFor(int scheme) => scheme switch
    {
        0x0403 => ECDsa.Create(ECCurve.NamedCurves.nistP256),
        0x0503 => ECDsa.Create(ECCurve.NamedCurves.nistP384),
        0x0603 => ECDsa.Create(ECCurve.NamedCurves.nistP521),
        _ => RSA.Create(2048),
    };

    [TestMethod]
    public void Constructor_NullOptionsOrRandom_ThrowsArgumentNullException()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = UnitTestCertificates.QuicOptions(key);

        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsServerHandshake(null!, RandomNumberGenerator.Create()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsServerHandshake(options, null!));
    }

    [TestMethod]
    public void Constructor_EmptyCertificateChain_ThrowsArgumentOutOfRangeException()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new TlsServerHandshakeOptions { CertificateChain = [], CertificateKey = key };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TlsServerHandshake(options, RandomNumberGenerator.Create()));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Constructor_KeyNeitherRsaNorEcdsaOnAServedCurve_ThrowsArgumentException(bool ecdsa)
    {
        using AsymmetricAlgorithm key = ecdsa ? new UnitTestEcdsaKeyOfUnsupportedSize() : new UnitTestUnsupportedKey();
        var options = new TlsServerHandshakeOptions { CertificateChain = [new byte[1]], CertificateKey = key };

        Assert.ThrowsExactly<ArgumentException>(() => new TlsServerHandshake(options, RandomNumberGenerator.Create()));
    }

    [TestMethod]
    public void Dispose_BeforeAnyClientHello_DoesNotThrow()
    {
        var handshake = EcdsaHandshake();

        handshake.Dispose();

        Assert.IsNull(handshake.CipherSuite);
    }

    private static TlsServerHandshake Rfc8448Handshake(RSA key) =>
        new(
            new TlsServerHandshakeOptions
            {
                CertificateChain = [Rfc8448.CertificateDer],
                CertificateKey = key,
                AdditionalEncryptedExtensions =
                [
                    new TlsExtension(TlsExtensionType.SupportedGroups, Convert.FromHexString("0012001d00170018001901000101010201030104")),
                    new TlsExtension(TlsExtensionType.RecordSizeLimit, Convert.FromHexString("4001")),
                    new TlsExtension(TlsExtensionType.ServerName, Array.Empty<byte>()),
                ],
            },
            new UnitTestScriptedRandom(Rfc8448.ServerRandom, Rfc8448.ServerEphemeralPrivateKey));

    private static TlsServerHandshake EcdsaHandshake()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new TlsServerHandshake(UnitTestCertificates.QuicOptions(key), RandomNumberGenerator.Create());
    }

    private static byte[] X25519ClientHello(UnitTestTlsClient client) =>
        new UnitTestClientHelloBuilder { KeyShares = [(0x001D, client.PublicShare)] }.Write();

    private static void AssertAlert(TlsAlert alert, Action receive)
    {
        var exception = Assert.ThrowsExactly<TlsAlertException>(receive);
        Assert.AreEqual(alert, exception.Alert, exception.Message);
    }
}
