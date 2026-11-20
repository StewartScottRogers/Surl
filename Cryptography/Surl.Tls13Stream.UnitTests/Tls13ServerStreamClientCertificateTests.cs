using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Tls13Stream;

[TestClass]
public sealed class Tls13ServerStreamClientCertificateTests
{
    [TestMethod]
    public async Task AcceptAsync_ClientAnswersTheCertificateRequest_ExposesTheVerifiedChainAndCarriesData()
    {
        var credentials = UnitTestClientCredentials.SelfSigned();
        IReadOnlyList<ReadOnlyMemory<byte>>? trusted = null;
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var options = UnitTestEstablished.OptionsRequiringClientCertificate(chain => (trusted = chain) is not null);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, options, RandomNumberGenerator.Create());

        await client.HandshakeAsync(UnitTestRfc8448.ClientHello, credentials: credentials);
        await using var stream = await accepting;
        await client.SendAsync(TlsContentType.ApplicationData, "ping"u8.ToArray());
        var buffer = new byte[16];
        var count = await stream.ReadAsync(buffer);

        Assert.HasCount(1, stream.ClientCertificateChain!);
        CollectionAssert.AreEqual(credentials.Chain[0], stream.ClientCertificateChain![0].ToArray());
        Assert.HasCount(1, trusted!);
        Assert.AreEqual("ping", System.Text.Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task AcceptAsync_ClientSendsNoCertificate_WritesTheCertificateRequiredAlertAndThrowsIt()
    {
        await AssertRefusedAsync(UnitTestClientCredentials.None(), _ => true, TlsAlert.CertificateRequired);
    }

    [TestMethod]
    public async Task AcceptAsync_ClientChainIsNotTrusted_WritesTheBadCertificateAlertAndThrowsIt()
    {
        await AssertRefusedAsync(UnitTestClientCredentials.SelfSigned(), _ => false, TlsAlert.BadCertificate);
    }

    [TestMethod]
    public async Task AcceptAsync_ClientSignatureIsWrong_WritesTheDecryptErrorAlertAndThrowsIt()
    {
        await AssertRefusedAsync(UnitTestClientCredentials.SelfSigned(corruptSignature: true), _ => true, TlsAlert.DecryptError);
    }

    private static async Task AssertRefusedAsync(
        UnitTestClientCredentials credentials, Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool> trusts, TlsAlert expected)
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(
            serverEnd, UnitTestEstablished.OptionsRequiringClientCertificate(trusts), RandomNumberGenerator.Create());

        var handshaking = client.HandshakeAsync(UnitTestRfc8448.ClientHello, corruptFinished: true, credentials: credentials);
        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => accepting);
        var alert = await client.ReadProtectedAsync();
        await handshaking;

        Assert.AreEqual(expected, failure.Alert);
        Assert.AreEqual(TlsContentType.Alert, alert!.ContentType);
        CollectionAssert.AreEqual(new byte[] { 2, (byte)expected }, alert.Content);
    }
}
