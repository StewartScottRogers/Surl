using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Surl.Quic;

/// <summary>
/// Client certificates (BL-741, ADR-0010 section 5): with <see cref="QuicServerConnectionOptions.TrustsClientCertificateChain"/>
/// set the handshake asks for one and completes only with a chain the check trusts.
/// </summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void ReceiveDatagram_TrustCheckAndNoClientCertificate_EndsWithCryptoErrorCertificateRequired()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(TrustingOptions(key, _ => true));

        exchange.SendRecordedInitials();
        exchange.SendFinished();

        Assert.IsTrue(exchange.Client.Tls.ReceivedCertificateRequest);
        Assert.IsFalse(exchange.Server.IsHandshakeComplete);
        Assert.IsNull(exchange.Server.ClientCertificateChain);
        var close = exchange.Client.FramesReceived[QuicPacketType.Handshake].OfType<QuicConnectionCloseFrame>().First();
        Assert.AreEqual(0x0174UL, close.ErrorCode);
        Assert.IsTrue(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_TrustCheckRefusingTheChain_EndsWithCryptoErrorBadCertificate()
    {
        using var key = RSA.Create(2048);
        using var clientCertificate = ClientCertificate();
        using var exchange = new UnitTestExchange(TrustingOptions(key, _ => false));
        exchange.Client.Tls.ClientCertificate = clientCertificate;

        exchange.SendRecordedInitials();
        exchange.SendFinished();

        Assert.IsFalse(exchange.Server.IsHandshakeComplete);
        var close = exchange.Client.FramesReceived[QuicPacketType.Handshake].OfType<QuicConnectionCloseFrame>().First();
        Assert.AreEqual(0x012AUL, close.ErrorCode);
    }

    [TestMethod]
    public void ReceiveDatagram_TrustCheckAndAChainingClientCertificate_CompletesTheHandshake()
    {
        using var key = RSA.Create(2048);
        using var clientCertificate = ClientCertificate();
        IReadOnlyList<ReadOnlyMemory<byte>>? checkedChain = null;
        using var exchange = new UnitTestExchange(TrustingOptions(key, chain =>
        {
            checkedChain = chain;
            return chain[0].Span.SequenceEqual(clientCertificate.RawData);
        }));
        exchange.Client.Tls.ClientCertificate = clientCertificate;

        exchange.SendRecordedInitials();
        exchange.SendFinished();

        Assert.IsTrue(exchange.Server.IsHandshakeComplete);
        Assert.IsNotNull(checkedChain);
        CollectionAssert.AreEqual(clientCertificate.RawData, exchange.Server.ClientCertificateChain!.Single().ToArray());
    }

    [TestMethod]
    public void ReceiveDatagram_NoTrustCheck_AsksForNoClientCertificate()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        Assert.IsFalse(exchange.Client.Tls.ReceivedCertificateRequest);
        Assert.IsNull(exchange.Server.ClientCertificateChain);
    }

    [TestMethod]
    public void ClientCertificateChain_BeforeTheConnectionStarts_IsNull()
    {
        using var key = RSA.Create(2048);
        using var random = RandomNumberGenerator.Create();

        using var connection = new QuicServerConnection(TrustingOptions(key, _ => true), TimeProvider.System, random);

        Assert.IsNull(connection.ClientCertificateChain);
    }

    private static QuicServerConnectionOptions TrustingOptions(RSA key, Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool> trusts) => new()
    {
        CertificateChain = [UnitTestCertificates.SelfSigned(key)],
        CertificateKey = key,
        TrustsClientCertificateChain = trusts,
    };

    // A 1024-bit key keeps the client's Certificate, CertificateVerify and Finished inside one datagram.
    private static X509Certificate2 ClientCertificate()
    {
        using var clientKey = RSA.Create(1024);
        var request = new X509CertificateRequest("CN=client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }
}
