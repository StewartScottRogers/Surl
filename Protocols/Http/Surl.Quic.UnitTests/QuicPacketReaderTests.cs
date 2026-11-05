namespace Surl.Quic;

/// <summary>
/// Reads and writes every QUIC packet header kind. The fixed bytes are RFC 9001 Appendix A's
/// unprotected packets (A.2 to A.5); none comes from the Curl port (ADR-0003).
/// </summary>
[TestClass]
public sealed class QuicPacketReaderTests
{
    /// <summary>RFC 9001 Appendix A's client Destination Connection ID.</summary>
    private static readonly byte[] ClientDestinationConnectionId = Convert.FromHexString("8394c8f03e515708");

    /// <summary>RFC 9001 Appendix A.3's server Source Connection ID.</summary>
    private static readonly byte[] ServerConnectionId = Convert.FromHexString("f067a5502a4262b5");

    /// <summary>RFC 9001 Appendix A.2: the unprotected client Initial's header, packet number 2.</summary>
    private const string ClientInitialHeader = "c300000001088394c8f03e5157080000449e00000002";

    /// <summary>RFC 9001 Appendix A.2: the client Initial's CRYPTO frame, which carries the 241-byte ClientHello.</summary>
    private const string ClientInitialCryptoFrame =
        "060040f1010000ed0303ebf8fa56f12939b9584a3896472ec40bb863cfd3e868" +
        "04fe3a47f06a2b69484c00000413011302010000c000000010000e00000b6578" +
        "616d706c652e636f6dff01000100000a00080006001d00170018001000070005" +
        "04616c706e000500050100000000003300260024001d00209370b2c9caa47fba" +
        "baf4559fedba753de171fa71f50f1ce15d43e994ec74d748002b000302030400" +
        "0d0010000e0403050306030203080408050806002d00020101001c0002400100" +
        "3900320408ffffffffffffffff05048000ffff07048000ffff08011001048000" +
        "75300901100f088394c8f03e51570806048000ffff";

    /// <summary>RFC 9001 Appendix A.3: the unprotected server Initial's header, packet number 1.</summary>
    private const string ServerInitialHeader = "c1000000010008f067a5502a4262b50040750001";

    /// <summary>RFC 9001 Appendix A.3: the server Initial's payload, an ACK frame and a CRYPTO frame carrying the ServerHello.</summary>
    private const string ServerInitialPayload =
        "02000000000600405a020000560303eefce7f7b37ba1d1632e96677825ddf73988cfc79825df566dc5430b9a045a1200130100002e00330024001d00209d3c940d89690b84d08a60993c144eca684d1081287c834d5311bcf32bb9da1a002b00020304";

    /// <summary>RFC 9001 Appendix A.4: the Retry packet, token "token".</summary>
    private const string RetryPacket = "ff000000010008f067a5502a4262b5746f6b656e04a265ba2eff4d829058fb3f0f2496ba";

    /// <summary>RFC 9001 Appendix A.5: the unprotected short header packet carrying one PING frame.</summary>
    private const string ShortHeaderPacket = "4200bff401";

    private const int AeadTagLength = 16;
    private const int ClientInitialPayloadLength = 1162;

    [TestMethod]
    public void Read_Rfc9001A2ClientInitial_ReadsItsHeaderFields()
    {
        var datagram = ClientInitialDatagram();

        var packet = (QuicLongHeaderPacket)QuicPacketReader.Read(datagram, 8, out var packetLength);

        Assert.AreEqual(datagram.Length, packetLength);
        Assert.AreEqual(QuicPacketType.Initial, packet.Type);
        CollectionAssert.AreEqual(ClientDestinationConnectionId, packet.DestinationConnectionId.ToArray());
        Assert.IsTrue(packet.SourceConnectionId.IsEmpty);
        Assert.IsTrue(packet.Token.IsEmpty);
        Assert.AreEqual(4, packet.PacketNumberLength);
        Assert.AreEqual(2U, packet.PacketNumber);
        Assert.AreEqual(ClientInitialPayloadLength + AeadTagLength, packet.Payload.Length);
    }

    [TestMethod]
    public void Read_Rfc9001A2ClientInitialPayload_CarriesTheCryptoAndPaddingFramesAtTheAppendixOffsets()
    {
        var packet = (QuicLongHeaderPacket)QuicPacketReader.Read(ClientInitialDatagram(), 8, out _);
        var payload = packet.Payload[..ClientInitialPayloadLength];

        var frames = QuicFrameReader.Read(payload, QuicPacketType.Initial);

        Assert.HasCount(2, frames);
        var crypto = (QuicCryptoFrame)frames[0];
        Assert.AreEqual(0UL, crypto.Offset);
        Assert.AreEqual(241, crypto.Data.Length);
        Assert.IsTrue(crypto.Data.Span.SequenceEqual(payload.Span[4..245]));
        Assert.AreEqual(0x01, crypto.Data.Span[0]);
        Assert.AreEqual(new QuicPaddingFrame(ClientInitialPayloadLength - 245), frames[1]);
        Assert.IsTrue(payload.Span[245..].IndexOfAnyExcept((byte)0) < 0);
    }

    [TestMethod]
    public void ToBytes_Rfc9001A2ClientInitial_WritesTheAppendixHeader()
    {
        var payload = new byte[ClientInitialPayloadLength + AeadTagLength];
        Convert.FromHexString(ClientInitialCryptoFrame).CopyTo(payload, 0);
        var packet = new QuicLongHeaderPacket(QuicPacketType.Initial, ClientDestinationConnectionId, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, 4, 2, payload);

        var bytes = packet.ToBytes();

        CollectionAssert.AreEqual(ClientInitialDatagram(), bytes);
    }

    [TestMethod]
    public void Read_Rfc9001A3ServerInitial_ReadsItsHeaderAndItsAckAndCryptoFrames()
    {
        var datagram = Convert.FromHexString(ServerInitialHeader + ServerInitialPayload + new string('0', 2 * AeadTagLength));

        var packet = (QuicLongHeaderPacket)QuicPacketReader.Read(datagram, 8, out var packetLength);
        var frames = QuicFrameReader.Read(packet.Payload[..^AeadTagLength], QuicPacketType.Initial);

        Assert.AreEqual(datagram.Length, packetLength);
        Assert.IsTrue(packet.DestinationConnectionId.IsEmpty);
        CollectionAssert.AreEqual(ServerConnectionId, packet.SourceConnectionId.ToArray());
        Assert.AreEqual(2, packet.PacketNumberLength);
        Assert.AreEqual(1U, packet.PacketNumber);
        Assert.HasCount(2, frames);
        var ack = (QuicAckFrame)frames[0];
        Assert.AreEqual(0UL, ack.LargestAcknowledged);
        Assert.HasCount(1, ack.Ranges);
        var crypto = (QuicCryptoFrame)frames[1];
        Assert.AreEqual(0UL, crypto.Offset);
        Assert.AreEqual(90, crypto.Data.Length);
        CollectionAssert.AreEqual(Convert.FromHexString(ServerInitialPayload), QuicFrameWriter.Write(frames));
        CollectionAssert.AreEqual(datagram, packet.ToBytes());
    }

    [TestMethod]
    public void Read_Rfc9001A4Retry_ReadsItsTokenAndIntegrityTagAndWritesItBack()
    {
        var datagram = Convert.FromHexString(RetryPacket);

        var packet = (QuicRetryPacket)QuicPacketReader.Read(datagram, 8, out var packetLength);

        Assert.AreEqual(datagram.Length, packetLength);
        Assert.IsTrue(packet.DestinationConnectionId.IsEmpty);
        CollectionAssert.AreEqual(ServerConnectionId, packet.SourceConnectionId.ToArray());
        Assert.AreEqual("token", System.Text.Encoding.ASCII.GetString(packet.RetryToken.Span));
        Assert.AreEqual("04a265ba2eff4d829058fb3f0f2496ba", Convert.ToHexStringLower(packet.RetryIntegrityTag.Span));
        Assert.AreEqual((byte)0x0f, packet.UnusedBits);
        CollectionAssert.AreEqual(datagram, packet.ToBytes());
    }

    [TestMethod]
    public void Read_Rfc9001A5ShortHeader_ReadsItsPacketNumberAndPingAndWritesItBack()
    {
        var datagram = Convert.FromHexString(ShortHeaderPacket);

        var packet = (QuicShortHeaderPacket)QuicPacketReader.Read(datagram, 0, out var packetLength);

        Assert.AreEqual(datagram.Length, packetLength);
        Assert.IsTrue(packet.DestinationConnectionId.IsEmpty);
        Assert.IsFalse(packet.SpinBit);
        Assert.IsFalse(packet.KeyPhase);
        Assert.AreEqual(3, packet.PacketNumberLength);
        Assert.AreEqual(0x00bff4U, packet.PacketNumber);
        Assert.IsInstanceOfType<QuicPingFrame>(QuicFrameReader.Read(packet.Payload, QuicPacketType.OneRtt)[0]);
        CollectionAssert.AreEqual(datagram, packet.ToBytes());
    }

    [TestMethod]
    [DataRow(QuicPacketType.Initial, "c80001020304", 1, 0x07U)]
    [DataRow(QuicPacketType.ZeroRtt, "", 2, 0x0102U)]
    [DataRow(QuicPacketType.Handshake, "", 4, 0xfffffffeU)]
    public void ToBytes_EachLongHeaderType_ReadsBackItsFields(QuicPacketType type, string tokenHex, int packetNumberLength, uint packetNumber)
    {
        var packet = new QuicLongHeaderPacket(type, ClientDestinationConnectionId, ServerConnectionId, Convert.FromHexString(tokenHex), packetNumberLength, packetNumber, new byte[] { 0x01 });

        var bytes = packet.ToBytes();
        var read = (QuicLongHeaderPacket)QuicPacketReader.Read(bytes, 8, out var packetLength);

        Assert.AreEqual(bytes.Length, packetLength);
        Assert.AreEqual(type, read.Type);
        CollectionAssert.AreEqual(ClientDestinationConnectionId, read.DestinationConnectionId.ToArray());
        CollectionAssert.AreEqual(ServerConnectionId, read.SourceConnectionId.ToArray());
        Assert.AreEqual(tokenHex, Convert.ToHexStringLower(read.Token.Span));
        Assert.AreEqual(packetNumberLength, read.PacketNumberLength);
        Assert.AreEqual(packetNumber, read.PacketNumber);
        CollectionAssert.AreEqual(new byte[] { 0x01 }, read.Payload.ToArray());
        CollectionAssert.AreEqual(bytes, read.ToBytes());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void ToBytes_ShortHeader_ReadsBackItsBits(bool spinBit, bool keyPhase)
    {
        var packet = new QuicShortHeaderPacket(ServerConnectionId, spinBit, keyPhase, 2, 0x0304, new byte[] { 0x01, 0x00 });

        var bytes = packet.ToBytes();
        var read = (QuicShortHeaderPacket)QuicPacketReader.Read(bytes, 8, out _);

        Assert.AreEqual(spinBit, read.SpinBit);
        Assert.AreEqual(keyPhase, read.KeyPhase);
        CollectionAssert.AreEqual(ServerConnectionId, read.DestinationConnectionId.ToArray());
        Assert.AreEqual(0x0304U, read.PacketNumber);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00 }, read.Payload.ToArray());
        CollectionAssert.AreEqual(bytes, read.ToBytes());
    }

    [TestMethod]
    public void ToBytes_VersionNegotiation_ReadsBackItsVersionsAndConnectionIds()
    {
        var longConnectionId = new byte[255];
        var packet = new QuicVersionNegotiationPacket(longConnectionId, ClientDestinationConnectionId, [QuicVersions.Version1, 0x6b3343cf], 0x2a);

        var bytes = packet.ToBytes();
        var read = (QuicVersionNegotiationPacket)QuicPacketReader.Read(bytes, 8, out var packetLength);

        Assert.AreEqual("aa00000000ff", Convert.ToHexStringLower(bytes.AsSpan(0, 6)));
        Assert.AreEqual(bytes.Length, packetLength);
        Assert.AreEqual(255, read.DestinationConnectionId.Length);
        CollectionAssert.AreEqual(ClientDestinationConnectionId, read.SourceConnectionId.ToArray());
        CollectionAssert.AreEqual(new[] { QuicVersions.Version1, 0x6b3343cfU }, read.SupportedVersions.ToArray());
        Assert.AreEqual((byte)0x2a, read.UnusedBits);
    }

    [TestMethod]
    public void Read_UnsupportedVersionWithLongConnectionIds_ReadsItsVersionAndConnectionIds()
    {
        var datagram = Convert.FromHexString("c0" + "6b3343cf" + "15" + new string('1', 42) + "00" + "abcdef");

        var packet = (QuicUnsupportedVersionPacket)QuicPacketReader.Read(datagram, 8, out var packetLength);

        Assert.AreEqual(datagram.Length, packetLength);
        Assert.AreEqual(0x6b3343cfU, packet.Version);
        Assert.AreEqual(21, packet.DestinationConnectionId.Length);
        Assert.IsTrue(packet.SourceConnectionId.IsEmpty);
    }

    [TestMethod]
    public void Read_CoalescedPackets_ReadsEachFromWhereTheLastEnded()
    {
        var first = new QuicLongHeaderPacket(QuicPacketType.Initial, ServerConnectionId, ServerConnectionId, ReadOnlyMemory<byte>.Empty, 1, 0, new byte[] { 0x01 }).ToBytes();
        var second = new QuicLongHeaderPacket(QuicPacketType.Handshake, ServerConnectionId, ServerConnectionId, ReadOnlyMemory<byte>.Empty, 1, 0, new byte[] { 0x01 }).ToBytes();
        ReadOnlyMemory<byte> datagram = (byte[])[.. first, .. second];

        var initial = (QuicLongHeaderPacket)QuicPacketReader.Read(datagram, 8, out var initialLength);
        var handshake = (QuicLongHeaderPacket)QuicPacketReader.Read(datagram[initialLength..], 8, out var handshakeLength);

        Assert.AreEqual(QuicPacketType.Initial, initial.Type);
        Assert.AreEqual(first.Length, initialLength);
        Assert.AreEqual(QuicPacketType.Handshake, handshake.Type);
        Assert.AreEqual(second.Length, handshakeLength);
    }

    [TestMethod]
    [DataRow("c00000000115" + "000000000000000000000000000000000000000000" + "00")]
    [DataRow("c0000000010015" + "000000000000000000000000000000000000000000")]
    public void Read_ConnectionIdLongerThan20Bytes_ThrowsProtocolViolation(string hex)
    {
        AssertProtocolViolation(hex, 8);
    }

    [TestMethod]
    [DataRow("80000000010000000101")]
    [DataRow("0001")]
    public void Read_FixedBitZero_ThrowsProtocolViolation(string hex)
    {
        AssertProtocolViolation(hex, 0);
    }

    [TestMethod]
    [DataRow("c4000000010000000101")]
    [DataRow("4801")]
    public void Read_ReservedBitsNotZero_ThrowsProtocolViolation(string hex)
    {
        AssertProtocolViolation(hex, 0);
    }

    [TestMethod]
    public void Read_LengthFieldShorterThanThePacketNumber_ThrowsProtocolViolation()
    {
        AssertProtocolViolation("c100000001000000010000", 0);
    }

    [TestMethod]
    [DataRow("c0000000010000000501")]
    [DataRow("c00000")]
    [DataRow("")]
    [DataRow("40")]
    public void Read_HeaderEndingEarly_ThrowsProtocolViolation(string hex)
    {
        AssertProtocolViolation(hex, 0);
    }

    [TestMethod]
    public void Read_ShortHeaderEndingInsideItsConnectionId_ThrowsProtocolViolation()
    {
        AssertProtocolViolation("40010203", 8);
    }

    [TestMethod]
    public void Read_RetryWithoutItsIntegrityTag_ThrowsProtocolViolation()
    {
        AssertProtocolViolation("f00000000100000102030405060708090a0b0c0d0e0f", 0);
    }

    [TestMethod]
    public void Read_VersionNegotiationListNotWholeVersions_ThrowsProtocolViolation()
    {
        AssertProtocolViolation("80000000000000000000", 0);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(21)]
    public void Read_ShortHeaderConnectionIdLengthOutOfRange_Throws(int length)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketReader.Read(new byte[] { 0x40 }, length, out _));
    }

    [TestMethod]
    public void ToBytes_LongHeaderOfRetryType_Throws()
    {
        var packet = LongHeader(QuicPacketType.Retry, ReadOnlyMemory<byte>.Empty, 1, 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => packet.ToBytes());
    }

    [TestMethod]
    [DataRow(QuicPacketType.ZeroRtt)]
    [DataRow(QuicPacketType.Handshake)]
    public void ToBytes_TokenOnANonInitialPacket_Throws(QuicPacketType type)
    {
        var packet = LongHeader(type, new byte[] { 1 }, 1, 0);

        Assert.ThrowsExactly<ArgumentException>(() => packet.ToBytes());
    }

    [TestMethod]
    [DataRow(0, 0U)]
    [DataRow(5, 0U)]
    [DataRow(1, 0x100U)]
    [DataRow(3, 0x1000000U)]
    public void ToBytes_PacketNumberThatDoesNotFitItsLength_Throws(int packetNumberLength, uint packetNumber)
    {
        var packet = LongHeader(QuicPacketType.Handshake, ReadOnlyMemory<byte>.Empty, packetNumberLength, packetNumber);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_ConnectionIdLongerThan20Bytes_Throws()
    {
        var tooLong = new byte[21];
        var tag = new byte[16];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicLongHeaderPacket(QuicPacketType.Initial, tooLong, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, 1, 0, ReadOnlyMemory<byte>.Empty).ToBytes());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicLongHeaderPacket(QuicPacketType.Initial, ReadOnlyMemory<byte>.Empty, tooLong, ReadOnlyMemory<byte>.Empty, 1, 0, ReadOnlyMemory<byte>.Empty).ToBytes());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicRetryPacket(tooLong, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, tag, 0).ToBytes());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicRetryPacket(ReadOnlyMemory<byte>.Empty, tooLong, ReadOnlyMemory<byte>.Empty, tag, 0).ToBytes());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicShortHeaderPacket(tooLong, false, false, 1, 0, ReadOnlyMemory<byte>.Empty).ToBytes());
    }

    [TestMethod]
    public void ToBytes_RetryWithAShortTagOrTooManyUnusedBits_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicRetryPacket(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, new byte[15], 0).ToBytes());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicRetryPacket(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, new byte[16], 0x10).ToBytes());
    }

    [TestMethod]
    public void ToBytes_VersionNegotiationOutOfRange_Throws()
    {
        var tooLong = new byte[256];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicVersionNegotiationPacket(tooLong, ReadOnlyMemory<byte>.Empty, [], 0).ToBytes());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicVersionNegotiationPacket(ReadOnlyMemory<byte>.Empty, tooLong, [], 0).ToBytes());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicVersionNegotiationPacket(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, [], 0x80).ToBytes());
    }

    private static QuicLongHeaderPacket LongHeader(QuicPacketType type, ReadOnlyMemory<byte> token, int packetNumberLength, uint packetNumber) =>
        new(type, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, token, packetNumberLength, packetNumber, new byte[] { 0x01 });

    private static void AssertProtocolViolation(string hex, int shortHeaderDestinationConnectionIdLength)
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => QuicPacketReader.Read(Convert.FromHexString(hex), shortHeaderDestinationConnectionIdLength, out _));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.IsNull(exception.FrameType);
    }

    private static byte[] ClientInitialDatagram()
    {
        var datagram = new byte[22 + ClientInitialPayloadLength + AeadTagLength];
        Convert.FromHexString(ClientInitialHeader).CopyTo(datagram, 0);
        Convert.FromHexString(ClientInitialCryptoFrame).CopyTo(datagram, 22);
        return datagram;
    }
}
