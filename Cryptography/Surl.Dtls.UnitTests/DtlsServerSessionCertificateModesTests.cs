using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/>'s RawPublicKey mode (RFC 7250) and client certificates under
/// <c>--cacert</c> (ADR-0087 decision 3), with the alerts of decision 10.
/// </summary>
[TestClass]
public sealed class DtlsServerSessionCertificateModesTests
{
    private static readonly byte[] X509 = [0];
    private static readonly byte[] RawPublicKey = [2];

    [TestMethod]
    public async Task HandshakeAsync_ClientListsRawPublicKeyFirst_SendsTheKeyAloneAndCompletes()
    {
        var client = new UnitTestDtlsClient { ServerCertificateTypes = [2, 0] };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsTrue(client.IsEstablished);
        CollectionAssert.AreEqual(RawPublicKey, Extension(client, DtlsExtensionType.ServerCertificateType));
        CollectionAssert.AreEqual(
            UnitTestCertificates.EcdsaP256.SubjectPublicKeyInfo,
            DtlsRawPublicKeyCertificate.Read(client.ServerCertificateBody).SubjectPublicKeyInfo);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientListsX509First_SendsTheChain()
    {
        var client = new UnitTestDtlsClient { ServerCertificateTypes = [0, 2] };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        CollectionAssert.AreEqual(X509, Extension(client, DtlsExtensionType.ServerCertificateType));
        CollectionAssert.AreEqual(
            UnitTestCertificates.EcdsaP256.Chain[0], DtlsCertificate.Read(client.ServerCertificateBody).Certificates.Single());
    }

    [TestMethod]
    [DataRow(new byte[] { 1 })]
    [DataRow(new byte[] { 1, 3 })]
    public async Task HandshakeAsync_ServerCertificateTypesNamingNeitherType_SendsUnsupportedCertificate(byte[] types)
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient { ServerCertificateTypes = types }, DtlsAlertDescription.UnsupportedCertificate);
    }

    [TestMethod]
    [DataRow(DtlsCertificateKeyAlgorithm.Ecdsa, (ushort)0x0403)]
    [DataRow(DtlsCertificateKeyAlgorithm.Rsa, (ushort)0x0804)]
    [DataRow(DtlsCertificateKeyAlgorithm.Rsa, (ushort)0x0401)]
    public async Task HandshakeAsync_ClientCertificateThatChains_Completes(DtlsCertificateKeyAlgorithm keyAlgorithm, ushort scheme)
    {
        using AsymmetricAlgorithm key = keyAlgorithm == DtlsCertificateKeyAlgorithm.Rsa ? RSA.Create(2048) : ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var client = new UnitTestDtlsClient
        {
            ClientCertificateChain = [UnitTestCertificates.IssueClientCertificate(key), UnitTestCertificates.ClientAuthority.RawData],
            ClientCertificateKey = key,
            CertificateVerifyScheme = scheme,
            ClientCertificateTypes = [2, 0],
            ServerCertificateTypes = [2],
        };
        var (session, flow) = StartWithAuthority(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [1]));
        var received = await session.ReceiveAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsTrue(client.IsEstablished);
        CollectionAssert.AreEqual(new byte[] { 1 }, received);
        CollectionAssert.AreEqual(new byte[] { 64, 1 }, client.CertificateRequest!.CertificateTypes);
        CollectionAssert.AreEqual(
            new ushort[] { 0x0403, 0x0503, 0x0804, 0x0805, 0x0401, 0x0501 }, client.CertificateRequest.SignatureSchemes.ToArray());
        Assert.IsEmpty(client.CertificateRequest.CertificateAuthorities);
        CollectionAssert.AreEqual(X509, Extension(client, DtlsExtensionType.ClientCertificateType));
        CollectionAssert.AreEqual(RawPublicKey, Extension(client, DtlsExtensionType.ServerCertificateType));
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientCertificateFromAnotherAuthority_SendsUnknownCa()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var certificate = UnitTestCertificates.IssueClientCertificate(key, UnitTestCertificates.OtherAuthority);

        await AssertAlertSentAsync(ClientWithCertificate(certificate, key), DtlsAlertDescription.UnknownCa, withAuthority: true);
    }

    [TestMethod]
    [DataRow(-800, 365)]
    [DataRow(10, 365)]
    public async Task HandshakeAsync_ClientCertificateOutsideItsValidityByTheTimeProvider_SendsCertificateExpired(int startDays, int validDays)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var certificate = UnitTestCertificates.IssueClientCertificate(
            key, notBefore: UnitTestCertificates.ClockStart.AddDays(startDays), validFor: TimeSpan.FromDays(validDays));

        await AssertAlertSentAsync(ClientWithCertificate(certificate, key), DtlsAlertDescription.CertificateExpired, withAuthority: true);
    }

    [TestMethod]
    public async Task HandshakeAsync_BadCertificateVerify_SendsDecryptError()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var client = new UnitTestDtlsClient
        {
            ClientCertificateChain = [UnitTestCertificates.IssueClientCertificate(key)],
            ClientCertificateKey = key,
            CorruptsCertificateVerify = true,
        };

        await AssertAlertSentAsync(client, DtlsAlertDescription.DecryptError, withAuthority: true);
    }

    [TestMethod]
    public async Task HandshakeAsync_HandshakeMessageInPlaceOfChangeCipherSpec_SendsUnexpectedMessage()
    {
        var client = new UnitTestDtlsClient
        {
            SplitsFinalFlight = true,
            ReplacesFinalFlight = c =>
            {
                var epoch0 = c.BuildFinalFlight()[0];
                var (keyExchangeHeader, _) = UnitTestRecords.Split(epoch0)[0];
                var nextSeq = (ushort)(c.NextMessageSeq - 1);
                return
                [
                    epoch0[..(DtlsRecordHeader.Size + keyExchangeHeader.Length)],
                    c.PlaintextRecord(DtlsContentType.Handshake, new DtlsHandshakeMessage(DtlsHandshakeType.Certificate, nextSeq, [0, 0, 0]).ToUnfragmentedBytes()),
                ];
            },
        };

        await AssertAlertSentAsync(client, DtlsAlertDescription.UnexpectedMessage);
    }

    [TestMethod]
    public async Task HandshakeAsync_NoClientCertificateUnderCaCert_SendsHandshakeFailure()
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient(), DtlsAlertDescription.HandshakeFailure, withAuthority: true);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientCertificateThatDoesNotRead_SendsBadCertificate()
    {
        var client = new UnitTestDtlsClient { ClientCertificateChain = [[1, 2, 3]] };

        await AssertAlertSentAsync(client, DtlsAlertDescription.BadCertificate, withAuthority: true);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientCertificateKeyNeitherRsaNorEcdsa_SendsUnsupportedCertificate()
    {
        var client = new UnitTestDtlsClient { ClientCertificateChain = [UnitTestCertificates.IssueEd25519ClientCertificate()] };

        await AssertAlertSentAsync(client, DtlsAlertDescription.UnsupportedCertificate, withAuthority: true);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientCertificateTypesWithoutX509UnderCaCert_SendsUnsupportedCertificate()
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient { ClientCertificateTypes = RawPublicKey }, DtlsAlertDescription.UnsupportedCertificate, withAuthority: true);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientCertificateTypesWithoutCaCert_IsIgnored()
    {
        var client = new UnitTestDtlsClient { ClientCertificateTypes = RawPublicKey };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsNull(client.CertificateRequest);
        Assert.IsNull(Extension(client, DtlsExtensionType.ClientCertificateType));
    }

    [TestMethod]
    public async Task HandshakeAsync_PreSharedKeySuiteUnderCaCert_AsksForNoCertificate()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], ClientCertificateTypes = X509, ServerCertificateTypes = [1] };
        var (session, flow) = DtlsServerSessionTests.Start(client, configure: options => options with
        {
            PreSharedKeys = _ => "secret"u8.ToArray(),
            ClientCertificateAuthorities = [UnitTestCertificates.ClientAuthority],
        });
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsNull(client.CertificateRequest);
        Assert.IsNull(Extension(client, DtlsExtensionType.ClientCertificateType));
        Assert.IsNull(Extension(client, DtlsExtensionType.ServerCertificateType));
    }

    private static UnitTestDtlsClient ClientWithCertificate(byte[] certificate, AsymmetricAlgorithm key) =>
        new() { ClientCertificateChain = [certificate], ClientCertificateKey = key };

    private static byte[]? Extension(UnitTestDtlsClient client, DtlsExtensionType type) =>
        client.ServerHello!.Extensions.SingleOrDefault(extension => extension.Type == type)?.Data;

    private static (DtlsServerSession Session, UnitTestDatagramFlow Flow) StartWithAuthority(UnitTestDtlsClient client) =>
        DtlsServerSessionTests.Start(client, configure: options => options with
        {
            ClientCertificateAuthorities = new X509Certificate2Collection(UnitTestCertificates.ClientAuthority),
        });

    private static async Task AssertAlertSentAsync(UnitTestDtlsClient client, DtlsAlertDescription expected, bool withAuthority = false)
    {
        var (session, flow) = withAuthority ? StartWithAuthority(client) : DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertSent, expected), outcome);
        Assert.AreEqual(new DtlsAlert(true, expected), UnitTestRecords.PlaintextAlert(flow.Sent[^1]));
        Assert.AreEqual(new DtlsAlert(true, expected), client.ReceivedAlerts.Single());
    }
}
