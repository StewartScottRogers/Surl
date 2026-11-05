using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>Errors: each ends the connection with CONNECTION_CLOSE and the code RFC 9000 or RFC 9001 names.</summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    [DataRow(0x1e, "1e")]
    [DataRow(0x19, "1900")]
    [DataRow(0x07, "070101")]
    public void ReceiveDatagram_FrameAClientMayNotSend_EndsWithConnectionCloseProtocolViolation(int frameType, string frameHex)
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var frame = QuicFrameReader.Read(Convert.FromHexString(frameHex), QuicPacketType.OneRtt).Single();

        exchange.Send(exchange.Client.SealOneRtt(frame));

        var close = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual((ulong)frameType, close.FrameType);
        Assert.IsFalse(close.IsApplicationError);
        Assert.IsTrue(close.ReasonPhrase.IsEmpty);
        Assert.IsTrue(exchange.Server.IsClosed);
        Assert.IsEmpty(exchange.Server.ReceiveDatagram(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams);
    }

    [TestMethod]
    [DataRow(false, 0x02)]
    [DataRow(true, 0x03)]
    public void ReceiveDatagram_AckOfAPacketNeverSent_EndsWithConnectionCloseProtocolViolation(bool withEcn, int frameType)
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.Send(exchange.Client.InitialDatagrams[0]);
        var ack = new QuicAckFrame([new QuicAckRange(100, 100)], 0, withEcn ? new QuicEcnCounts(0, 0, 0) : null);

        exchange.Send(exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 1200, ack));

        var close = exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual((ulong)frameType, close.FrameType);
    }

    [TestMethod]
    public void ReceiveDatagram_MalformedFrame_EndsWithConnectionCloseFrameEncodingError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        exchange.Send(exchange.Client.SealOneRttPayload([0x40, 0x50, 0x00, 0x00]));

        var close = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.FrameEncodingError, close.ErrorCode);
        Assert.AreEqual(0x50UL, close.FrameType);
    }

    [TestMethod]
    public void ReceiveDatagram_ReservedBitsSet_EndsWithConnectionCloseProtocolViolation()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.Send(exchange.Client.InitialDatagrams[0]);

        exchange.Send(exchange.Client.SealInitialWithReservedBitsSet(exchange.Client.ServerConnectionId!, new QuicPingFrame()));

        var close = exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual(0UL, close.FrameType);
    }

    [TestMethod]
    public void ReceiveDatagram_ClientHelloWithoutH3_EndsWithCryptoErrorNoApplicationProtocol()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key), editClientHello: UnitTestClientHelloEdits.Replace("026833056833", "027833056833"));

        exchange.SendRecordedInitials();

        var close = exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual(0x0178UL, close.ErrorCode);
        Assert.AreEqual(0x06UL, close.FrameType);
        Assert.IsFalse(exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicCryptoFrame>().Any());
        Assert.IsTrue(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_CryptoFrameInAOneRttPacket_EndsWithCryptoErrorUnexpectedMessage()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        exchange.Send(exchange.Client.SealOneRtt(new QuicCryptoFrame(0, new byte[] { 4, 0, 0, 0 })));

        var close = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual(0x010aUL, close.ErrorCode);
    }

    [TestMethod]
    [DataRow("server-only original_destination_connection_id", "0004", 4, 0x08)]
    [DataRow("server-only stateless_reset_token", "0210", 16, 0x08)]
    [DataRow("server-only preferred_address", "0d29", 41, 0x08)]
    [DataRow("server-only retry_source_connection_id", "1004", 4, 0x08)]
    [DataRow("version_information choosing version 2", "110800000002", 4, 0x11)]
    [DataRow("a malformed max_idle_timeout", "0102c0", 1, 0x08)]
    public void ReceiveDatagram_ClientTransportParameterFault_EndsWithItsError(string description, string parameterPrefixHex, int zeroBytes, int expectedError)
    {
        using var key = RSA.Create(2048);
        var edit = UnitTestClientHelloEdits.ReplaceParameters(parameterPrefixHex + new string('0', zeroBytes * 2));
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key), editClientHello: edit);

        exchange.SendRecordedInitials();

        var close = exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)expectedError, close.ErrorCode, description);
        Assert.IsTrue(exchange.Server.IsClosed, description);
    }

    [TestMethod]
    public void ReceiveDatagram_ClientParametersWithoutVersionInformation_AreAccepted()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key), editClientHello: UnitTestClientHelloEdits.ReplaceParameters("0404be800000"));

        exchange.SendRecordedInitials();
        exchange.SendFinished();

        Assert.IsTrue(exchange.Server.IsHandshakeComplete);
        Assert.IsNull(exchange.Server.PeerTransportParameters!.VersionInformation);
    }

    [TestMethod]
    [DataRow("a different initial_source_connection_id", "0f14", 0xff)]
    [DataRow("no initial_source_connection_id", "1b14", 0x00)]
    public void ReceiveDatagram_ClientSourceConnectionIdParameterWrong_EndsWithTransportParameterError(string description, string replacementPrefix, int firstByteMask)
    {
        using var key = RSA.Create(2048);
        using var probe = UnitTestQuicClient.FromRecording(UnitTestExchange.DefaultFixture);
        var changed = probe.SourceConnectionId.ToArray();
        changed[0] ^= (byte)firstByteMask;
        var edit = UnitTestClientHelloEdits.Replace("0f14" + Convert.ToHexString(probe.SourceConnectionId), replacementPrefix + Convert.ToHexString(changed));
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key), editClientHello: edit);

        exchange.SendRecordedInitials();

        var close = exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.TransportParameterError, close.ErrorCode, description);
    }

    [TestMethod]
    public void ReceiveDatagram_ClientConnectionClose_DrainsWithNothingSent()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = exchange.Send(exchange.Client.SealOneRtt(new QuicConnectionCloseFrame(true, 0x100, 0, ReadOnlyMemory<byte>.Empty), new QuicPingFrame()));

        Assert.IsEmpty(output.Datagrams);
        Assert.IsTrue(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void Close_AfterTheHandshake_SendsAnApplicationConnectionCloseInOneRtt()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        exchange.Take(exchange.Server.Close(0x100));

        var close = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicConnectionCloseFrame>().Single();
        Assert.IsTrue(close.IsApplicationError);
        Assert.AreEqual(0x100UL, close.ErrorCode);
        Assert.IsTrue(exchange.Server.IsClosed);
        Assert.IsEmpty(exchange.Server.Close(0x100).Datagrams);
    }

    [TestMethod]
    public void Close_DuringTheHandshake_SendsApplicationErrorAtInitialAndHandshake()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.SendRecordedInitials();

        exchange.Take(exchange.Server.Close(0x100));

        foreach (var type in new[] { QuicPacketType.Initial, QuicPacketType.Handshake })
        {
            var close = exchange.Client.FramesReceived[type].OfType<QuicConnectionCloseFrame>().Single();
            Assert.IsFalse(close.IsApplicationError);
            Assert.AreEqual((ulong)QuicTransportErrorCode.ApplicationError, close.ErrorCode);
        }
    }

    [TestMethod]
    public void Close_BeforeTheConnectionStarts_SendsNothing()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        var output = exchange.Server.Close(0x100);

        Assert.IsEmpty(output.Datagrams);
        Assert.IsTrue(exchange.Server.IsClosed);
        Assert.IsEmpty(exchange.Server.ReceiveDatagram(exchange.Client.InitialDatagrams[0]).Datagrams);
    }
}
