using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// Protects and unprotects QUIC packets (RFC 9001 section 5). The fixed bytes are RFC 9001
/// Appendix A's (A.2, A.3 and A.5); none comes from the Curl port (ADR-0003).
/// </summary>
[TestClass]
public sealed class QuicPacketProtectionTests
{
    private const int AeadTagLength = QuicPacketProtection.AeadTagLength;

    [TestMethod]
    public void Protect_Rfc9001A2ClientInitial_GivesAppendixA2ProtectedPacket()
    {
        using var client = ClientInitialProtection();

        var protectedPacket = client.Protect(Rfc9001AppendixA.ClientInitial(), Rfc9001AppendixA.ClientInitialPacketNumber);

        Assert.AreEqual(Rfc9001AppendixA.ClientInitialProtected, Convert.ToHexStringLower(protectedPacket));
    }

    [TestMethod]
    public void Unprotect_Rfc9001A2ClientInitialCoalescedWithMore_GivesThePlaintextAndItsLength()
    {
        using var client = ClientInitialProtection();
        var protectedPacket = Convert.FromHexString(Rfc9001AppendixA.ClientInitialProtected);
        var datagram = protectedPacket.Concat(new byte[] { 0x40, 0x01, 0x02 }).ToArray();

        var result = client.Unprotect(datagram, 8, 0);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(Rfc9001AppendixA.ClientInitialPacketNumber, result.PacketNumber);
        Assert.AreEqual(protectedPacket.Length, result.PacketLength);
        AssertSamePacket(Rfc9001AppendixA.ClientInitial(), (QuicLongHeaderPacket)result.Packet!);
    }

    [TestMethod]
    public void Protect_Rfc9001A3ServerInitial_GivesAppendixA3ProtectedPacket()
    {
        using var server = ServerInitialProtection();

        var protectedPacket = server.Protect(Rfc9001AppendixA.ServerInitial(), Rfc9001AppendixA.ServerInitialPacketNumber);

        Assert.AreEqual(Rfc9001AppendixA.ServerInitialProtected, Convert.ToHexStringLower(protectedPacket));
    }

    [TestMethod]
    public void Unprotect_Rfc9001A3ServerInitial_GivesThePlaintext()
    {
        using var server = ServerInitialProtection();
        var datagram = Convert.FromHexString(Rfc9001AppendixA.ServerInitialProtected);

        var result = server.Unprotect(datagram, 0, 0);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(Rfc9001AppendixA.ServerInitialPacketNumber, result.PacketNumber);
        Assert.AreEqual(datagram.Length, result.PacketLength);
        AssertSamePacket(Rfc9001AppendixA.ServerInitial(), (QuicLongHeaderPacket)result.Packet!);
    }

    [TestMethod]
    public void FromTrafficSecret_Rfc9001A5ChaCha20Poly1305Secret_GivesAppendixA5KeyIvAndHeaderProtectionKey()
    {
        using var protection = ChaChaProtection();

        Assert.AreEqual(TlsCipherSuite.ChaCha20Poly1305Sha256, protection.CipherSuite);
        Assert.AreEqual(Rfc9001AppendixA.ChaChaKey, Convert.ToHexStringLower(protection.Key.Span));
        Assert.AreEqual(Rfc9001AppendixA.ChaChaIv, Convert.ToHexStringLower(protection.Iv.Span));
        Assert.AreEqual(Rfc9001AppendixA.ChaChaHeaderProtectionKey, Convert.ToHexStringLower(protection.HeaderProtectionKey.Span));
    }

    [TestMethod]
    public void Protect_Rfc9001A5ShortHeaderPacket_GivesAppendixA5ProtectedPacket()
    {
        using var protection = ChaChaProtection();

        var protectedPacket = protection.Protect(Rfc9001AppendixA.ChaChaShortHeaderPacket(), Rfc9001AppendixA.ChaChaPacketNumber);

        Assert.AreEqual(Rfc9001AppendixA.ChaChaProtected, Convert.ToHexStringLower(protectedPacket));
    }

    [TestMethod]
    public void Unprotect_Rfc9001A5ShortHeaderPacket_GivesThePingFrameAndFullPacketNumber()
    {
        using var protection = ChaChaProtection();

        var result = protection.Unprotect(Convert.FromHexString(Rfc9001AppendixA.ChaChaProtected), 0, Rfc9001AppendixA.ChaChaPacketNumber - 4);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(Rfc9001AppendixA.ChaChaPacketNumber, result.PacketNumber);
        AssertSamePacket(Rfc9001AppendixA.ChaChaShortHeaderPacket(), (QuicShortHeaderPacket)result.Packet!);
    }

    [TestMethod]
    public void Unprotect_AesGcmTagThatFailsToOpen_IsUndecryptableNotThrown()
    {
        using var client = ClientInitialProtection();
        var datagram = Convert.FromHexString(Rfc9001AppendixA.ClientInitialProtected);
        datagram[^1] ^= 0x01;

        var result = client.Unprotect(datagram, 8, 0);

        Assert.AreEqual(QuicUnprotectStatus.Undecryptable, result.Status);
        Assert.IsNull(result.Packet);
        Assert.AreEqual(Rfc9001AppendixA.ClientInitialPacketNumber, result.PacketNumber);
        Assert.AreEqual(datagram.Length, result.PacketLength);
    }

    [TestMethod]
    public void Unprotect_ChaCha20Poly1305TagThatFailsToOpen_IsUndecryptableNotThrown()
    {
        using var protection = ChaChaProtection();
        var datagram = Convert.FromHexString(Rfc9001AppendixA.ChaChaProtected);
        datagram[^1] ^= 0x01;

        var result = protection.Unprotect(datagram, 0, Rfc9001AppendixA.ChaChaPacketNumber);

        Assert.AreEqual(QuicUnprotectStatus.Undecryptable, result.Status);
        Assert.IsNull(result.Packet);
    }

    [TestMethod]
    public void Unprotect_InitialOpenedWithTheOtherDirectionsKeys_IsUndecryptable()
    {
        using var server = ServerInitialProtection();

        var result = server.Unprotect(Convert.FromHexString(Rfc9001AppendixA.ClientInitialProtected), 8, 0);

        Assert.AreEqual(QuicUnprotectStatus.Undecryptable, result.Status);
    }

    [TestMethod]
    public void Unprotect_PacketTooShortToSample_IsUndecryptableWithPacketNumberZero()
    {
        using var protection = ChaChaProtection();
        var datagram = Convert.FromHexString(Rfc9001AppendixA.ChaChaProtected)[..^1];

        var result = protection.Unprotect(datagram, 0, 0);

        Assert.AreEqual(QuicUnprotectStatus.Undecryptable, result.Status);
        Assert.AreEqual(0UL, result.PacketNumber);
        Assert.AreEqual(datagram.Length, result.PacketLength);
    }

    [TestMethod]
    public void Unprotect_OpenedPacketWithReservedBitsSet_ReportsReservedBitsNotZero()
    {
        using var client = ClientInitialProtection();
        var datagram = ProtectClientInitialWithReservedBitSet(client);

        var result = client.Unprotect(datagram, 8, 0);

        Assert.AreEqual(QuicUnprotectStatus.ReservedBitsNotZero, result.Status);
        Assert.IsNull(result.Packet);
        Assert.AreEqual(Rfc9001AppendixA.ClientInitialPacketNumber, result.PacketNumber);
    }

    [TestMethod]
    public void ProtectThenUnprotect_HandshakePacket_RoundTrips()
    {
        using var server = ServerInitialProtection();
        var packet = new QuicLongHeaderPacket(QuicPacketType.Handshake, Convert.FromHexString("0102"), Convert.FromHexString(Rfc9001AppendixA.ServerConnectionId), ReadOnlyMemory<byte>.Empty, 1, 7, new byte[20]);

        var result = server.Unprotect(server.Protect(packet, 7), 0, 5);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(7UL, result.PacketNumber);
        AssertSamePacket(packet, (QuicLongHeaderPacket)result.Packet!);
    }

    [TestMethod]
    public void ProtectThenUnprotect_Aes256GcmShortHeaderWithSpinAndKeyPhase_RoundTrips()
    {
        using var protection = QuicPacketProtection.FromTrafficSecret(TlsCipherSuite.Aes256GcmSha384, new byte[48]);
        var packet = new QuicShortHeaderPacket(Convert.FromHexString(Rfc9001AppendixA.ServerConnectionId), true, true, 2, 0x1234, Convert.FromHexString("0100000000"));

        var result = protection.Unprotect(protection.Protect(packet, 0x51234), 8, 0x51200);

        Assert.AreEqual(32, protection.Key.Length);
        Assert.AreEqual(32, protection.HeaderProtectionKey.Length);
        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(0x51234UL, result.PacketNumber);
        AssertSamePacket(packet, (QuicShortHeaderPacket)result.Packet!);
    }

    [TestMethod]
    [DataRow("00", 0, DisplayName = "Short header with fixed bit 0")]
    [DataRow("c00000000200", 0, DisplayName = "Not version 1")]
    [DataRow("800000000100", 0, DisplayName = "Long header with fixed bit 0")]
    [DataRow(Rfc9001AppendixA.RetryPacket, 0, DisplayName = "Retry")]
    [DataRow("c000000001000040640000", 0, DisplayName = "Length announces more than remains")]
    [DataRow("40", 8, DisplayName = "Short header connection ID cut short")]
    public void Unprotect_HeaderThatCannotBeLocated_ThrowsProtocolViolation(string datagram, int shortHeaderDestinationConnectionIdLength)
    {
        using var client = ClientInitialProtection();

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => client.Unprotect(Convert.FromHexString(datagram), shortHeaderDestinationConnectionIdLength, 0));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.IsNull(exception.FrameType);
    }

    [TestMethod]
    [DataRow(-1, 0UL)]
    [DataRow(21, 0UL)]
    [DataRow(0, (1UL << 62) + 1)]
    public void Unprotect_ConnectionIdLengthOrExpectedPacketNumberOutOfRange_Throws(int shortHeaderDestinationConnectionIdLength, ulong expectedPacketNumber)
    {
        using var client = ClientInitialProtection();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => client.Unprotect(Convert.FromHexString(Rfc9001AppendixA.ChaChaProtected), shortHeaderDestinationConnectionIdLength, expectedPacketNumber));
    }

    [TestMethod]
    public void Protect_PacketNumberAbove2To62Minus1_Throws()
    {
        using var client = ClientInitialProtection();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => client.Protect(Rfc9001AppendixA.ClientInitial(), QuicPacketNumber.MaxValue + 1));
    }

    [TestMethod]
    public void Protect_PacketNumberWhoseLowBytesAreNotThePacketsOwn_Throws()
    {
        using var client = ClientInitialProtection();

        var exception = Assert.ThrowsExactly<ArgumentException>(() => client.Protect(Rfc9001AppendixA.ClientInitial(), 3));

        Assert.AreEqual("packetNumber", exception.ParamName);
    }

    [TestMethod]
    public void Protect_PacketNumberAndPayloadShorterThanFourBytes_Throws()
    {
        using var protection = ChaChaProtection();
        var packet = new QuicShortHeaderPacket(ReadOnlyMemory<byte>.Empty, false, false, 1, 1, new byte[] { 0x01, 0x00 });

        var exception = Assert.ThrowsExactly<ArgumentException>(() => protection.Protect(packet, 1));

        Assert.AreEqual("packet", exception.ParamName);
    }

    [TestMethod]
    public void Protect_NullLongHeaderPacket_Throws()
    {
        using var client = ClientInitialProtection();

        Assert.ThrowsExactly<ArgumentNullException>(() => client.Protect((QuicLongHeaderPacket)null!, 0));
    }

    [TestMethod]
    public void Protect_NullShortHeaderPacket_Throws()
    {
        using var client = ClientInitialProtection();

        Assert.ThrowsExactly<ArgumentNullException>(() => client.Protect((QuicShortHeaderPacket)null!, 0));
    }

    [TestMethod]
    public void FromTrafficSecret_NotATls13CipherSuite_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketProtection.FromTrafficSecret((TlsCipherSuite)0x1304, new byte[32]));
    }

    private static QuicPacketProtection ClientInitialProtection() =>
        QuicPacketProtection.FromTrafficSecret(QuicInitialSecrets.CipherSuite, Convert.FromHexString(Rfc9001AppendixA.ClientInitialSecret));

    private static QuicPacketProtection ServerInitialProtection() =>
        QuicPacketProtection.FromTrafficSecret(QuicInitialSecrets.CipherSuite, Convert.FromHexString(Rfc9001AppendixA.ServerInitialSecret));

    private static QuicPacketProtection ChaChaProtection() =>
        QuicPacketProtection.FromTrafficSecret(TlsCipherSuite.ChaCha20Poly1305Sha256, Convert.FromHexString(Rfc9001AppendixA.ChaChaSecret));

    /// <summary>
    /// Protects A.2's client Initial by RFC 9001 section 5 with a reserved bit set in the first
    /// byte, which <see cref="QuicPacketProtection.Protect(QuicLongHeaderPacket, ulong)"/> never sends.
    /// </summary>
    private static byte[] ProtectClientInitialWithReservedBitSet(QuicPacketProtection protection)
    {
        const int packetNumberOffset = 18;
        const int payloadOffset = 22;
        var plaintext = Rfc9001AppendixA.ClientInitial().Payload.ToArray();
        var bytes = (Rfc9001AppendixA.ClientInitial() with { Payload = new byte[plaintext.Length + AeadTagLength] }).ToBytes();
        bytes[0] |= 0x04;
        var nonce = protection.Iv.ToArray();
        nonce[^1] ^= (byte)Rfc9001AppendixA.ClientInitialPacketNumber;
        using var cipher = QuicPacketCipher.Create(protection.CipherSuite, protection.Key.ToArray(), protection.HeaderProtectionKey.ToArray());
        cipher.Seal(nonce, plaintext, bytes.AsSpan(0, payloadOffset), bytes.AsSpan(payloadOffset, plaintext.Length), bytes.AsSpan(bytes.Length - AeadTagLength));
        var mask = cipher.ComputeHeaderProtectionMask(bytes.AsSpan(payloadOffset, QuicPacketProtection.HeaderProtectionSampleLength));
        bytes[0] ^= (byte)(mask[0] & 0x0f);
        for (var index = 0; index < 4; index++)
        {
            bytes[packetNumberOffset + index] ^= mask[1 + index];
        }

        return bytes;
    }

    private static void AssertSamePacket(QuicLongHeaderPacket expected, QuicLongHeaderPacket actual)
    {
        Assert.AreEqual(expected.Type, actual.Type);
        CollectionAssert.AreEqual(expected.DestinationConnectionId.ToArray(), actual.DestinationConnectionId.ToArray());
        CollectionAssert.AreEqual(expected.SourceConnectionId.ToArray(), actual.SourceConnectionId.ToArray());
        CollectionAssert.AreEqual(expected.Token.ToArray(), actual.Token.ToArray());
        Assert.AreEqual(expected.PacketNumberLength, actual.PacketNumberLength);
        Assert.AreEqual(expected.PacketNumber, actual.PacketNumber);
        CollectionAssert.AreEqual(expected.Payload.ToArray(), actual.Payload.ToArray());
    }

    private static void AssertSamePacket(QuicShortHeaderPacket expected, QuicShortHeaderPacket actual)
    {
        CollectionAssert.AreEqual(expected.DestinationConnectionId.ToArray(), actual.DestinationConnectionId.ToArray());
        Assert.AreEqual(expected.SpinBit, actual.SpinBit);
        Assert.AreEqual(expected.KeyPhase, actual.KeyPhase);
        Assert.AreEqual(expected.PacketNumberLength, actual.PacketNumberLength);
        Assert.AreEqual(expected.PacketNumber, actual.PacketNumber);
        CollectionAssert.AreEqual(expected.Payload.ToArray(), actual.Payload.ToArray());
    }
}
