using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Dtls;

/// <summary>
/// What a completed <see cref="DtlsServerSession"/> reports about its client for the <c>coaps</c>
/// server's access rules and block sizes (ADR-0097 decisions 4, 6 and 7, ADR-0087 decisions 3 and 7):
/// the PSK identity, the client certificate and whether it verified, and the largest application
/// payload one record carries.
/// </summary>
[TestClass]
public sealed class DtlsServerSessionPeerReportTests
{
    private const int RecordHeaderLength = 13;
    private const int ExplicitNonceLength = 8;

    [TestMethod]
    public async Task ClientPreSharedKeyIdentity_AfterAPskHandshake_IsTheIdentityTheClientSentAsUtf8Text()
    {
        var identity = "zoë@coap"u8.ToArray();
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], PskIdentity = identity };
        var (session, flow) = StartPskOnly(client, presented => presented.SequenceEqual(identity) ? "secret"u8.ToArray() : null);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual("zoë@coap", session.ClientPreSharedKeyIdentity);
        Assert.IsNull(session.ClientCertificate);
        Assert.IsFalse(session.ClientCertificateVerified);
    }

    [TestMethod]
    public async Task ClientPreSharedKeyIdentity_AfterAPskHandshakeThatFailed_IsNull()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], PskIdentity = "mallory"u8.ToArray() };
        var (session, flow) = StartPskOnly(client, _ => null);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.AlertSent, outcome.Result);
        Assert.IsNull(session.ClientPreSharedKeyIdentity);
    }

    [TestMethod]
    public void Reports_BeforeTheHandshake_AreNoneAndTheRecordSizeIsRefused()
    {
        var (session, _) = DtlsServerSessionTests.Start(new UnitTestDtlsClient());
        using var _ = session;

        Assert.IsNull(session.ClientPreSharedKeyIdentity);
        Assert.IsNull(session.ClientCertificate);
        Assert.IsFalse(session.ClientCertificateVerified);
        Assert.ThrowsExactly<InvalidOperationException>(() => session.MaximumApplicationDataLength);
    }

    [TestMethod]
    public async Task ClientCertificate_AfterACertificateHandshakeUnderCaCert_IsTheClientsCertificateAndVerified()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var certificate = UnitTestCertificates.IssueClientCertificate(key);
        var client = new UnitTestDtlsClient
        {
            ClientCertificateChain = [certificate, UnitTestCertificates.ClientAuthority.RawData],
            ClientCertificateKey = key,
        };
        var (session, flow) = DtlsServerSessionTests.Start(client, configure: options => options with
        {
            ClientCertificateAuthorities = new X509Certificate2Collection(UnitTestCertificates.ClientAuthority),
        });
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        CollectionAssert.AreEqual(certificate, session.ClientCertificate!.RawData);
        Assert.IsTrue(session.ClientCertificateVerified);
        Assert.IsNull(session.ClientPreSharedKeyIdentity);
    }

    [TestMethod]
    public async Task ClientCertificate_AfterACertificateHandshakeWithoutCaCert_IsNoneAndNotVerified()
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsNull(session.ClientCertificate);
        Assert.IsFalse(session.ClientCertificateVerified);
        Assert.IsNull(session.ClientPreSharedKeyIdentity);
    }

    [TestMethod]
    public async Task MaximumApplicationDataLength_UnderAesGcm_IsWhatFitsA1200ByteDatagramAndSendAsyncKeepsToIt()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0xC02B] };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;
        await session.HandshakeAsync(flow.Exhausted.Token);
        const int expected = 1200 - RecordHeaderLength - ExplicitNonceLength - 16;

        await session.SendAsync(new byte[expected], flow.Exhausted.Token);

        Assert.AreEqual(1163, session.MaximumApplicationDataLength);
        Assert.AreEqual(expected, session.MaximumApplicationDataLength);
        Assert.HasCount(1200, flow.Sent[^1]);
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await session.SendAsync(new byte[expected + 1], flow.Exhausted.Token));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task MaximumApplicationDataLength_UnderAesCcm8_GainsTheEightBytesItsShorterTagSaves()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0xC0A8] };
        var (session, flow) = StartPskOnly(client, _ => "secret"u8.ToArray());
        using var _ = session;

        await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(1200 - RecordHeaderLength - ExplicitNonceLength - 8, session.MaximumApplicationDataLength);
    }

    private static (DtlsServerSession Session, UnitTestDatagramFlow Flow) StartPskOnly(UnitTestDtlsClient client, DtlsPreSharedKeyLookup lookup) =>
        DtlsServerSessionTests.Start(client, configure: options => options with { Certificate = null, PreSharedKeys = lookup });
}
