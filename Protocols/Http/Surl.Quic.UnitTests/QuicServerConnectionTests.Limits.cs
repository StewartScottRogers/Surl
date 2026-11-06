using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Quic;

/// <summary>The first datagram, the anti-amplification limit, the timers and key discard.</summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void ReceiveDatagram_ClientInitialSmallerThan1200Bytes_IsDropped()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        var initial = exchange.Client.SealInitial(exchange.Client.OriginalDestinationConnectionId, 1199, new QuicCryptoFrame(0, exchange.Client.ClientHello[..100]));

        var output = exchange.Server.ReceiveDatagram(initial);

        Assert.HasCount(1199, initial);
        Assert.IsEmpty(output.Datagrams);
        Assert.IsFalse(exchange.Server.IsStarted);
        Assert.IsFalse(exchange.Server.HasKeys(TlsEncryptionLevel.Initial));
    }

    [TestMethod]
    public void ReceiveDatagram_ClientInitialOfExactly1200Bytes_StartsTheConnection()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        var initial = exchange.Client.SealInitial(exchange.Client.OriginalDestinationConnectionId, 1200, new QuicCryptoFrame(0, exchange.Client.ClientHello[..100]));

        exchange.Send(initial);

        Assert.HasCount(1200, initial);
        Assert.IsTrue(exchange.Server.IsStarted);
        Assert.IsTrue(exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicAckFrame>().Any());
    }

    /// <summary>
    /// curl's ClientHello fills four datagrams, so the server's first flight meets the congestion
    /// window (12000 bytes) before three times them (14400). The test client then acknowledges the
    /// Initial packets with a PING but never a Handshake packet, so its address stays unvalidated,
    /// and the server's probe timeouts carry the rest of a long flight as the limit lets them.
    /// </summary>
    [TestMethod]
    public void ReceiveDatagram_BeforeAddressValidation_SendsNoMoreThanThreeTimesTheBytesReceived()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key, copies: 24));
        var rounds = 0;

        foreach (var datagram in exchange.Client.InitialDatagrams)
        {
            exchange.Send(datagram);
            Assert.IsLessThanOrEqualTo(3 * exchange.BytesToServer, exchange.BytesFromServer);
        }

        while (exchange.Client.ClientFinished is null)
        {
            Assert.IsLessThan(100, rounds++, "The flight never arrived.");
            exchange.Send(exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 1200, exchange.Client.AckFrame(QuicPacketType.Initial), new QuicPingFrame()));
            Assert.IsLessThanOrEqualTo(3 * exchange.BytesToServer, exchange.BytesFromServer);
            exchange.AdvanceToNextDeadline();
            Assert.IsLessThanOrEqualTo(3 * exchange.BytesToServer, exchange.BytesFromServer);
        }

        exchange.SendFinished();

        Assert.IsGreaterThan(1, rounds);
        Assert.IsGreaterThan(18000, exchange.BytesFromServer);
        Assert.IsTrue(exchange.Server.IsHandshakeComplete);
    }

    [TestMethod]
    public void ReceiveDatagram_AfterAHandshakePacketOpens_SendsWithoutTheAmplificationLimit()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var frames = Enumerable.Range(0, 40).Select(index => new QuicStreamFrame(3, (ulong)index * 1000, new byte[1000], false)).ToArray();

        var datagrams = exchange.Server.SendApplicationFrames(frames).Datagrams.ToList();
        exchange.Client.Receive(datagrams);
        while (exchange.AcknowledgeOneRtt().Datagrams is { Count: > 0 } more)
        {
            datagrams.AddRange(more);
        }

        Assert.IsGreaterThan(3 * exchange.BytesToServer, exchange.BytesFromServer + datagrams.Sum(datagram => datagram.Length));
        Assert.IsTrue(datagrams.All(datagram => datagram.Length <= QuicServerConnection.MaxDatagramSize));
        Assert.AreEqual((ulong)40000, exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicStreamFrame>().Max(frame => frame.Offset + (ulong)frame.Data.Length));
    }

    [TestMethod]
    public void ReceiveDatagram_UnsupportedVersion_IsAnsweredWithVersionNegotiation()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        var datagram = LongHeaderOfVersion(0x1a2a3a4a, QuicServerConnection.MaxDatagramSize);

        var output = exchange.Server.ReceiveDatagram(datagram);

        var answer = (QuicVersionNegotiationPacket)QuicPacketReader.Read(output.Datagrams.Single(), 0, out _);
        CollectionAssert.AreEqual(new byte[] { 9, 9, 9, 9 }, answer.DestinationConnectionId.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, answer.SourceConnectionId.ToArray());
        CollectionAssert.AreEqual(new[] { QuicVersions.Version1 }, answer.SupportedVersions.ToArray());
        Assert.IsFalse(exchange.Server.IsStarted);
    }

    [TestMethod]
    public void ReceiveDatagram_UnsupportedVersionShorterThan1200Bytes_IsDropped()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        var output = exchange.Server.ReceiveDatagram(LongHeaderOfVersion(0x1a2a3a4a, 1199));

        Assert.IsEmpty(output.Datagrams);
    }

    [TestMethod]
    [DataRow("version negotiation with a version list", "c0000000000801020304050607080109")]
    [DataRow("version negotiation without whole versions", "c000000000080102030405060708040909090900")]
    [DataRow("short header", "40")]
    [DataRow("handshake packet", "e000000001080102030405060708040909090900")]
    [DataRow("initial with a 7-byte DCID", "c0000000010701020304050607040909090900")]
    [DataRow("initial with a 21-byte DCID", "c00000000115")]
    public void ReceiveDatagram_FirstDatagramThatIsNoClientInitial_StartsNothing(string description, string headerHex)
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        var datagram = new byte[QuicServerConnection.MaxDatagramSize];
        Convert.FromHexString(headerHex).CopyTo(datagram, 0);

        var output = exchange.Server.ReceiveDatagram(datagram);

        Assert.IsEmpty(output.Datagrams, description);
        Assert.IsFalse(exchange.Server.IsStarted, description);
    }

    [TestMethod]
    public void HandleTimeout_IdleTimeoutPassed_DiscardsTheConnectionSilently()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.AcknowledgeOneRtt();
        var deadline = exchange.Server.NextDeadline;

        exchange.Time.Advance(TimeSpan.FromSeconds(30) - TimeSpan.FromMilliseconds(1));
        var early = exchange.Server.HandleTimeout();
        exchange.Time.Advance(TimeSpan.FromMilliseconds(1));
        var due = exchange.Server.HandleTimeout();

        Assert.AreEqual(exchange.Time.GetUtcNow(), deadline);
        Assert.IsEmpty(early.Datagrams);
        Assert.IsEmpty(due.Datagrams);
        Assert.IsTrue(exchange.Server.IsClosed);
        Assert.IsNull(exchange.Server.NextDeadline);
        Assert.IsEmpty(exchange.Server.ReceiveDatagram(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams);
        Assert.IsEmpty(exchange.Server.HandleTimeout().Datagrams);
    }

    [TestMethod]
    [DataRow(30000, 100, 100)]
    [DataRow(50, 100, 50)]
    [DataRow(0, 100, 100)]
    public void HandleTimeout_ClientMaxIdleTimeout_TheSmallerOfTheTwoApplies(int serverMilliseconds, int clientMilliseconds, int expectedMilliseconds)
    {
        using var key = RSA.Create(2048);
        var options = UnitTestCertificates.Options(key);
        var maxIdleTimeout = "0104" + (0x80000000 | (uint)clientMilliseconds).ToString("x8");
        using var exchange = new UnitTestExchange(
            new QuicServerConnectionOptions { CertificateChain = options.CertificateChain, CertificateKey = key, IdleTimeout = TimeSpan.FromMilliseconds(serverMilliseconds) },
            editClientHello: UnitTestClientHelloEdits.Replace("090480040000", maxIdleTimeout));
        exchange.SendRecordedInitials();
        exchange.SendFinished();
        var start = exchange.Time.GetUtcNow();

        Assert.AreEqual(start + TimeSpan.FromMilliseconds(expectedMilliseconds), exchange.Server.NextDeadline);
        Assert.AreEqual((ulong)clientMilliseconds, exchange.Server.PeerTransportParameters!.MaxIdleTimeout);
    }

    [TestMethod]
    public void NextDeadline_NoIdleTimeoutOnEitherSide_IsNull()
    {
        using var key = RSA.Create(2048);
        var options = UnitTestCertificates.Options(key);
        using var exchange = new UnitTestExchange(new QuicServerConnectionOptions { CertificateChain = options.CertificateChain, CertificateKey = key, IdleTimeout = TimeSpan.Zero });

        exchange.SendRecordedInitials();
        exchange.SendFinished();
        exchange.AcknowledgeOneRtt();

        Assert.IsNull(exchange.Server.NextDeadline);
        Assert.IsEmpty(exchange.Server.HandleTimeout().Datagrams);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    [DataRow(0, 5000, 5000)]
    [DataRow(30000, 5000, 5000)]
    [DataRow(3000, 5000, 3000)]
    public void NextDeadline_DuringTheHandshake_IsTheEarlierOfTheIdleAndHandshakeTimeouts(int idleMilliseconds, int handshakeMilliseconds, int expectedMilliseconds)
    {
        using var key = RSA.Create(2048);
        var options = UnitTestCertificates.Options(key);
        using var exchange = new UnitTestExchange(new QuicServerConnectionOptions
        {
            CertificateChain = options.CertificateChain,
            CertificateKey = key,
            IdleTimeout = TimeSpan.FromMilliseconds(idleMilliseconds),
            HandshakeTimeout = TimeSpan.FromMilliseconds(handshakeMilliseconds),
        });

        exchange.Send(exchange.Client.InitialDatagrams[0]);

        Assert.AreEqual(exchange.Time.GetUtcNow() + TimeSpan.FromMilliseconds(expectedMilliseconds), exchange.Server.NextDeadline);
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(0)]
    public void HandleTimeout_HandshakeTimeoutPassed_ClosesWithNoErrorAtInitialAndHandshake(int idleSeconds)
    {
        using var key = RSA.Create(2048);
        var options = UnitTestCertificates.Options(key);
        using var exchange = new UnitTestExchange(new QuicServerConnectionOptions { CertificateChain = options.CertificateChain, CertificateKey = key, IdleTimeout = TimeSpan.FromSeconds(idleSeconds), HandshakeTimeout = TimeSpan.FromSeconds(10) });
        exchange.SendRecordedInitials();

        exchange.Time.Advance(TimeSpan.FromSeconds(10));
        exchange.Take(exchange.Server.HandleTimeout());

        Assert.IsTrue(exchange.Server.IsClosed);
        foreach (var type in new[] { QuicPacketType.Initial, QuicPacketType.Handshake })
        {
            var close = exchange.Client.FramesReceived[type].OfType<QuicConnectionCloseFrame>().Single();
            Assert.AreEqual((ulong)QuicTransportErrorCode.NoError, close.ErrorCode);
            Assert.IsFalse(close.IsApplicationError);
        }
    }

    [TestMethod]
    public void HandleTimeout_BeforeTheConnectionStarts_DoesNothing()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        Assert.IsEmpty(exchange.Server.HandleTimeout().Datagrams);
        Assert.IsNull(exchange.Server.NextDeadline);
    }

    [TestMethod]
    public void ReceiveDatagram_FirstHandshakePacket_DiscardsTheInitialKeys()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.SendRecordedInitials();
        Assert.IsTrue(exchange.Server.HasKeys(TlsEncryptionLevel.Initial));

        var output = exchange.Send(exchange.Client.SealHandshake(new QuicPingFrame()));
        var afterDiscard = exchange.Send(exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 1200, new QuicPingFrame()));

        Assert.IsFalse(exchange.Server.HasKeys(TlsEncryptionLevel.Initial));
        Assert.IsTrue(exchange.Server.HasKeys(TlsEncryptionLevel.Handshake));
        Assert.IsTrue(exchange.Client.FramesReceived[QuicPacketType.Handshake].OfType<QuicAckFrame>().Any());
        Assert.HasCount(1, output.Datagrams);
        Assert.IsEmpty(afterDiscard.Datagrams);
    }

    [TestMethod]
    public void ReceiveDatagram_HandshakeComplete_DiscardsTheHandshakeKeysAndKeepsOneRtt()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var lateHandshake = exchange.Send(exchange.Client.SealHandshake(new QuicPingFrame()));

        Assert.IsFalse(exchange.Server.HasKeys(TlsEncryptionLevel.Initial));
        Assert.IsFalse(exchange.Server.HasKeys(TlsEncryptionLevel.Handshake));
        Assert.IsTrue(exchange.Server.HasKeys(TlsEncryptionLevel.Application));
        Assert.IsEmpty(lateHandshake.Datagrams);
    }

    private static byte[] LongHeaderOfVersion(uint version, int length)
    {
        var datagram = new byte[length];
        byte[] header = [0xc0, (byte)(version >> 24), (byte)(version >> 16), (byte)(version >> 8), (byte)version, 8, 1, 2, 3, 4, 5, 6, 7, 8, 4, 9, 9, 9, 9];
        header.CopyTo(datagram, 0);
        return datagram;
    }
}
