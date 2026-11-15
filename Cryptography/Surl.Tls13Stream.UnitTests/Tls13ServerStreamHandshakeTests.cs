using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Tls13Stream;

[TestClass]
public sealed class Tls13ServerStreamHandshakeTests
{
    [TestMethod]
    public async Task AcceptAsync_Rfc8448ClientHello_SendsTheTracesServerHelloAndExchangesApplicationDataBothWays()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), UnitTestEstablished.Rfc8448Random());

        await client.HandshakeAsync(UnitTestRfc8448.ClientHello);
        await using var stream = await accepting;
        await client.SendAsync(TlsContentType.ApplicationData, "GET / HTTP/1.1"u8.ToArray());
        var buffer = new byte[64];
        var count = await stream.ReadAsync(buffer);
        await stream.WriteAsync("HTTP/1.1 200 OK"u8.ToArray());
        var reply = await client.ReadProtectedAsync();

        CollectionAssert.AreEqual(UnitTestRfc8448.ServerHello, client.ServerHello);
        Assert.IsFalse(client.ReceivedChangeCipherSpec);
        Assert.AreEqual(TlsCipherSuite.Aes128GcmSha256, stream.CipherSuite);
        Assert.IsNull(stream.ApplicationProtocol);
        Assert.IsNull(stream.ClientCertificateChain);
        Assert.IsNotNull(stream.ClientHello);
        Assert.AreEqual("GET / HTTP/1.1", System.Text.Encoding.ASCII.GetString(buffer, 0, count));
        Assert.AreEqual(TlsContentType.ApplicationData, reply!.ContentType);
        Assert.AreEqual("HTTP/1.1 200 OK", System.Text.Encoding.ASCII.GetString(reply.Content));
    }

    [TestMethod]
    public async Task AcceptAsync_ClientOffersAlpn_AgreesTheFirstServerProtocolTheClientOffers()
    {
        var clientHello = UnitTestClientHellos.WithApplicationProtocols(UnitTestRfc8448.ClientHello, "http/1.1", "h2");

        await using var established = await UnitTestEstablished.CreateAsync(clientHello, "h2", "http/1.1");

        Assert.AreEqual("h2", established.Stream.ApplicationProtocol);
    }

    [TestMethod]
    public async Task AcceptAsync_NoSharedAlpnProtocol_WritesTheNoApplicationProtocolAlertAndThrowsIt()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var clientHello = UnitTestClientHellos.WithApplicationProtocols(UnitTestRfc8448.ClientHello, "spdy");
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options("h2"), RandomNumberGenerator.Create());

        await client.SendClientHelloAsync(clientHello);
        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => accepting);
        var alert = await client.ReadRecordAsync();

        Assert.AreEqual(TlsAlert.NoApplicationProtocol, failure.Alert);
        CollectionAssert.AreEqual(new byte[] { 21, 3, 3, 0, 2, 2, 120 }, alert);
    }

    [TestMethod]
    public async Task AcceptAsync_ClientHelloWithSessionIdAndHelloRetryRequest_SendsOneChangeCipherSpecAndCompletes()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var first = UnitTestClientHellos.WithEmptyKeyShare(UnitTestClientHellos.WithSessionId(UnitTestRfc8448.ClientHello));
        var second = UnitTestClientHellos.WithSessionId(UnitTestRfc8448.ClientHello);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        var handshaking = client.HandshakeAsync(first, second);
        await Task.WhenAny(handshaking, accepting);
        if (accepting.IsCompleted)
        {
            await accepting;
        }

        await handshaking;
        await using var stream = await accepting;

        Assert.IsTrue(client.ReceivedChangeCipherSpec);
        Assert.AreEqual(TlsCipherSuite.Aes128GcmSha256, stream.CipherSuite);
    }

    [TestMethod]
    public async Task AcceptAsync_ClientHelloWithSessionId_SendsAChangeCipherSpecAfterTheServerHello()
    {
        var clientHello = UnitTestClientHellos.WithSessionId(UnitTestRfc8448.ClientHello);

        await using var established = await UnitTestEstablished.CreateAsync(clientHello);

        Assert.IsTrue(established.Client.ReceivedChangeCipherSpec);
    }

    [TestMethod]
    public async Task AcceptAsync_ClientHelloReplayedInPartThenTheRestFromTheTransport_Completes()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var record = UnitTestClientHellos.Record(22, UnitTestRfc8448.ClientHello);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create(), record.AsMemory(0, 7));

        await client.SendRecordAsync(record[7..]);
        await client.HandshakeAsync(UnitTestRfc8448.ClientHello, alreadySent: true);
        await using var stream = await accepting;

        Assert.AreEqual(TlsCipherSuite.Aes128GcmSha256, stream.CipherSuite);
    }

    [TestMethod]
    public async Task AcceptAsync_ChangeCipherSpecBeforeTheClientHello_IsIgnored()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await client.SendRecordAsync(UnitTestClientHellos.Record(20, [1]));
        await client.HandshakeAsync(UnitTestRfc8448.ClientHello);
        await using var stream = await accepting;

        Assert.AreEqual(TlsCipherSuite.Aes128GcmSha256, stream.CipherSuite);
    }

    [TestMethod]
    public async Task AcceptAsync_BadClientFinished_WritesTheProtectedDecryptErrorAlertAndThrowsIt()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await client.HandshakeAsync(UnitTestRfc8448.ClientHello, corruptFinished: true);
        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => accepting);
        var alert = await client.ReadProtectedAsync();

        Assert.AreEqual(TlsAlert.DecryptError, failure.Alert);
        Assert.AreEqual(TlsContentType.Alert, alert!.ContentType);
        CollectionAssert.AreEqual(new byte[] { 2, 51 }, alert.Content);
    }

    [TestMethod]
    public async Task AcceptAsync_TruncatedClientHelloRecord_ThrowsEndOfStream()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await clientEnd.WriteAsync(UnitTestClientHellos.Record(22, UnitTestRfc8448.ClientHello)[..40]);
        clientEnd.Dispose();

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => accepting);
    }

    [TestMethod]
    public async Task AcceptAsync_TruncatedRecordHeader_ThrowsEndOfStream()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await clientEnd.WriteAsync(new byte[] { 22, 3, 3 });
        clientEnd.Dispose();

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => accepting);
    }

    [TestMethod]
    public async Task AcceptAsync_ClientClosesBeforeSendingAnything_ThrowsEndOfStream()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        clientEnd.Dispose();

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => accepting);
    }

    [TestMethod]
    public async Task AcceptAsync_RecordLongerThanTheLimit_WritesRecordOverflowAndThrowsIt()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await client.SendRecordAsync([22, 3, 3, 0x41, 0x01]);
        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => accepting);
        var alert = await client.ReadRecordAsync();

        Assert.AreEqual(TlsAlert.RecordOverflow, failure.Alert);
        CollectionAssert.AreEqual(new byte[] { 21, 3, 3, 0, 2, 2, 22 }, alert);
    }

    [TestMethod]
    [DataRow(40, new byte[] { 2, 40 })]
    [DataRow(50, new byte[] { 2 })]
    public async Task AcceptAsync_ClientSendsAnAlert_ThrowsThePeerAlert(int expected, byte[] alertBytes)
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await client.SendRecordAsync(UnitTestClientHellos.Record(21, alertBytes));
        var failure = await Assert.ThrowsExactlyAsync<Tls13PeerAlertException>(() => accepting);

        Assert.AreEqual((TlsAlert)expected, failure.Alert);
        StringAssert.Contains(failure.Message, expected.ToString());
    }

    [TestMethod]
    public async Task AcceptAsync_ApplicationDataInsteadOfAHandshake_WritesUnexpectedMessageAndThrowsIt()
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await client.SendRecordAsync(UnitTestClientHellos.Record(23, [1, 2, 3]));
        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => accepting);

        Assert.AreEqual(TlsAlert.UnexpectedMessage, failure.Alert);
    }

    [TestMethod]
    [DataRow(new byte[] { 2 })]
    [DataRow(new byte[] { 1, 1 })]
    public async Task AcceptAsync_ChangeCipherSpecThatIsNotTheSingleByteOne_WritesUnexpectedMessageAndThrowsIt(byte[] content)
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, UnitTestEstablished.Options(), RandomNumberGenerator.Create());

        await client.SendRecordAsync(UnitTestClientHellos.Record(20, content));
        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => accepting);

        Assert.AreEqual(TlsAlert.UnexpectedMessage, failure.Alert);
    }

    [TestMethod]
    public async Task AcceptAsync_NullArgument_Throws()
    {
        var (serverEnd, _) = UnitTestDuplex.CreatePair();
        var options = UnitTestEstablished.Options();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ServerStream.AcceptAsync(null!, options, RandomNumberGenerator.Create()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ServerStream.AcceptAsync(serverEnd, null!, RandomNumberGenerator.Create()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ServerStream.AcceptAsync(serverEnd, options, null!));
    }
}
