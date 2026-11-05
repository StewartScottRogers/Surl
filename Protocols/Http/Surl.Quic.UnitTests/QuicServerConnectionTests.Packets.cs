using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Quic;

/// <summary>Packets the server drops or skips, frames it answers or ignores, and the guards on its API.</summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        using var key = RSA.Create(2048);
        var options = UnitTestCertificates.Options(key);
        using var random = RandomNumberGenerator.Create();

        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicServerConnection(null!, TimeProvider.System, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicServerConnection(options, null!, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicServerConnection(options, TimeProvider.System, null!));
    }

    [TestMethod]
    public void ReceiveDatagram_SameInitialTwice_IsAcknowledgedOnce()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.Send(exchange.Client.InitialDatagrams[0]);

        var repeated = exchange.Send(exchange.Client.InitialDatagrams[0]);

        Assert.IsEmpty(repeated.Datagrams);
    }

    [TestMethod]
    public void ReceiveDatagram_InitialToAnotherConnectionId_IsIgnored()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.Send(exchange.Client.InitialDatagrams[0]);

        var output = exchange.Send(exchange.Client.SealInitial(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 1200, new QuicPingFrame()));

        Assert.IsEmpty(output.Datagrams);
    }

    [TestMethod]
    public void ReceiveDatagram_OneRttToAnotherConnectionId_IsIgnored()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = exchange.Send(exchange.Client.SealOneRttPayload(QuicFrameWriter.Write([new QuicMaxDataFrame(9)]), new byte[8]));

        Assert.IsEmpty(output.Datagrams);
        Assert.IsEmpty(output.Streams);
    }

    [TestMethod]
    public void ReceiveDatagram_InitialThatDoesNotOpen_IsDroppedAndTheNextCoalescedPacketRead()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.Send(exchange.Client.InitialDatagrams[0]);
        var broken = exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 600, new QuicPingFrame());
        broken[^1] ^= 1;
        var good = exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 600, new QuicPingFrame());

        var output = exchange.Send([.. broken, .. good]);

        Assert.HasCount(1, output.Datagrams);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    [DataRow("a header cut short", "c0000000")]
    [DataRow("another version", "c01a2a3a4a00")]
    [DataRow("a 0-RTT packet", "d00000000100")]
    [DataRow("an Initial whose header ends early", "c00000000108")]
    public void ReceiveDatagram_CoalescedPacketTheServerCannotRead_DropsTheRestOfTheDatagram(string description, string trailingHex)
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.Send(exchange.Client.InitialDatagrams[0]);
        var good = exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 1200, new QuicPingFrame());

        var output = exchange.Send([.. good, .. Convert.FromHexString(trailingHex)]);

        Assert.HasCount(1, output.Datagrams, description);
        Assert.IsFalse(exchange.Server.IsClosed, description);
    }

    [TestMethod]
    public void ReceiveDatagram_OneRttBeforeTheHandshakeCompletes_IsDropped()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.SendRecordedInitials();

        var output = exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));

        Assert.IsEmpty(output.Datagrams);
        Assert.IsFalse(exchange.Server.IsHandshakeComplete);
    }

    [TestMethod]
    public void ReceiveDatagram_InitialAndHandshakeAfterTheirKeysAreDiscarded_AreSkippedToTheOneRttPacket()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var initial = exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 100, new QuicPingFrame());
        var handshake = exchange.Client.SealHandshake(new QuicPingFrame());
        var oneRtt = exchange.Client.SealOneRtt(new QuicStreamFrame(4, 0, new byte[] { 5 }, false));

        var output = exchange.Send([.. initial, .. handshake, .. oneRtt]);

        Assert.AreEqual((ulong)4, output.Streams.Single().Id);
    }

    [TestMethod]
    public void ReceiveDatagram_InitialCutShortAfterItsKeysAreDiscarded_DropsTheRest()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = exchange.Send(Convert.FromHexString("c0000000010800000000000000000000"));

        Assert.IsEmpty(output.Datagrams);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_PathChallenge_IsAnsweredWithPathResponse()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        exchange.Send(exchange.Client.SealOneRtt(new QuicPathChallengeFrame(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));

        var response = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicPathResponseFrame>().Single();
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, response.Data.ToArray());
    }

    [TestMethod]
    public void ReceiveDatagram_FramesTheTransportConsumes_AreNotHandedToTheCaller()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var newConnectionId = new QuicNewConnectionIdFrame(1, 0, new byte[] { 1, 2, 3, 4 }, new byte[16]);
        var pathResponse = new QuicPathResponseFrame(new byte[8]);

        var output = exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame(), newConnectionId, pathResponse, new QuicPaddingFrame(3)));

        Assert.IsEmpty(output.Streams);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_AckOfTheServersPackets_ShortensItsPacketNumbers()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Take(exchange.Server.SendApplicationFrames([new QuicMaxDataFrame(1)]));

        exchange.Send(exchange.Client.SealOneRtt(new QuicAckFrame([new QuicAckRange(0, 1)], 0, null)));
        exchange.Take(exchange.Server.SendApplicationFrames([new QuicMaxDataFrame(2)]));

        Assert.IsFalse(exchange.Server.IsClosed);
        Assert.AreEqual(new QuicMaxDataFrame(2), exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicMaxDataFrame>().Last());
    }

    [TestMethod]
    public void SendApplicationFrames_BeforeTheHandshakeCompletes_Throws()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        Assert.ThrowsExactly<InvalidOperationException>(() => exchange.Server.SendApplicationFrames([new QuicPingFrame()]));
        Assert.ThrowsExactly<ArgumentNullException>(() => exchange.Server.SendApplicationFrames(null!));
    }

    [TestMethod]
    public void SendApplicationFrames_AfterTheConnectionEnds_Throws()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Server.Close(0);

        Assert.ThrowsExactly<InvalidOperationException>(() => exchange.Server.SendApplicationFrames([new QuicPingFrame()]));
    }

    [TestMethod]
    public void SendApplicationFrames_FrameLongerThanAPacketHolds_Throws()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var tooLong = new QuicStreamFrame(3, 0, new byte[QuicServerConnection.MaxApplicationFrameLength], false);
        var longest = new QuicStreamFrame(3, 0, new byte[QuicServerConnection.MaxApplicationFrameLength - tooLong.ToBytes().Length + QuicServerConnection.MaxApplicationFrameLength], false);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => exchange.Server.SendApplicationFrames([tooLong]));
        var output = exchange.Server.SendApplicationFrames([longest]);
        Assert.HasCount(QuicServerConnection.MaxApplicationFrameLength, longest.ToBytes());
        Assert.HasCount(1, output.Datagrams);
    }

    [TestMethod]
    public void HasKeys_BeforeTheConnectionStarts_IsFalseAtEveryLevel()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        Assert.IsFalse(exchange.Server.HasKeys(TlsEncryptionLevel.Initial));
        Assert.IsFalse(exchange.Server.HasKeys(TlsEncryptionLevel.Handshake));
        Assert.IsFalse(exchange.Server.HasKeys(TlsEncryptionLevel.Application));
        Assert.IsNull(exchange.Server.ApplicationProtocol);
        Assert.IsNull(exchange.Server.CipherSuite);
        Assert.IsTrue(exchange.Server.ServerConnectionId.IsEmpty);
    }
}
