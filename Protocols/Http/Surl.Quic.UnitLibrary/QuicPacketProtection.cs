using System.Buffers.Binary;
using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// QUIC packet protection for one direction at one encryption level (RFC 9001 section 5): the
/// AEAD key and IV and the header-protection key that HKDF-Expand-Label derives from a traffic
/// secret with <c>quic key</c>, <c>quic iv</c> and <c>quic hp</c>, and the sealing, opening and
/// header protection they key. AES-GCM is the base class library's, ChaCha20-Poly1305 the
/// hand-built one (ADR-0082 decision 3).
/// </summary>
public sealed class QuicPacketProtection : IDisposable
{
    /// <summary>The length of every AEAD tag QUIC version 1 uses, in bytes.</summary>
    public const int AeadTagLength = 16;

    /// <summary>The length of the ciphertext sample header protection takes, in bytes.</summary>
    public const int HeaderProtectionSampleLength = 16;

    private const int IvLength = 12;
    private const int SampleOffsetFromPacketNumber = 4;
    private const byte LongHeaderProtectedBits = 0x0f;
    private const byte ShortHeaderProtectedBits = 0x1f;

    private readonly byte[] key;
    private readonly byte[] iv;
    private readonly byte[] headerProtectionKey;
    private readonly QuicPacketCipher cipher;

    private QuicPacketProtection(TlsCipherSuite cipherSuite, byte[] key, byte[] iv, byte[] headerProtectionKey)
    {
        CipherSuite = cipherSuite;
        this.key = key;
        this.iv = iv;
        this.headerProtectionKey = headerProtectionKey;
        cipher = QuicPacketCipher.Create(cipherSuite, key, headerProtectionKey);
    }

    /// <summary>The cipher suite whose AEAD and header protection this uses.</summary>
    public TlsCipherSuite CipherSuite { get; }

    /// <summary>The AEAD key: 16 bytes for AES-128-GCM, 32 for AES-256-GCM and ChaCha20-Poly1305.</summary>
    public ReadOnlyMemory<byte> Key => key;

    /// <summary>The 12-byte IV the packet number is combined with to form each nonce.</summary>
    public ReadOnlyMemory<byte> Iv => iv;

    /// <summary>The header-protection key, as long as <see cref="Key"/>.</summary>
    public ReadOnlyMemory<byte> HeaderProtectionKey => headerProtectionKey;

    /// <summary>Derives the packet protection keys of a traffic secret (RFC 9001 section 5.1).</summary>
    /// <param name="cipherSuite">The negotiated cipher suite; Initial packets use <see cref="QuicInitialSecrets.CipherSuite"/>.</param>
    /// <param name="trafficSecret">The traffic secret TLS gave for this direction and encryption level.</param>
    /// <returns>The protection those keys give.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cipherSuite"/> is not a TLS 1.3 cipher suite.</exception>
    public static QuicPacketProtection FromTrafficSecret(TlsCipherSuite cipherSuite, ReadOnlySpan<byte> trafficSecret)
    {
        var hash = TlsKeySchedule.HashAlgorithmOf(cipherSuite);
        return FromTrafficSecret(cipherSuite, trafficSecret, TlsKeySchedule.ExpandLabel(hash, trafficSecret, "quic hp", [], KeyLength(cipherSuite)));
    }

    /// <summary>The traffic secret of the next key phase: HKDF-Expand-Label(secret, "quic ku", "", hash length) (RFC 9001 section 6.1).</summary>
    internal static byte[] NextTrafficSecret(TlsCipherSuite cipherSuite, ReadOnlySpan<byte> trafficSecret) =>
        TlsKeySchedule.ExpandLabel(TlsKeySchedule.HashAlgorithmOf(cipherSuite), trafficSecret, "quic ku", [], trafficSecret.Length);

    /// <summary>The protection of the next key phase: the AEAD key and IV of <paramref name="nextTrafficSecret"/>, and this protection's header-protection key, which a key update does not change (RFC 9001 section 6.1).</summary>
    internal QuicPacketProtection ForNextKeyPhase(ReadOnlySpan<byte> nextTrafficSecret) => FromTrafficSecret(CipherSuite, nextTrafficSecret, headerProtectionKey);

    private static QuicPacketProtection FromTrafficSecret(TlsCipherSuite cipherSuite, ReadOnlySpan<byte> trafficSecret, byte[] headerProtectionKey)
    {
        var hash = TlsKeySchedule.HashAlgorithmOf(cipherSuite);
        return new QuicPacketProtection(
            cipherSuite,
            TlsKeySchedule.ExpandLabel(hash, trafficSecret, "quic key", [], KeyLength(cipherSuite)),
            TlsKeySchedule.ExpandLabel(hash, trafficSecret, "quic iv", [], IvLength),
            headerProtectionKey);
    }

    private static int KeyLength(TlsCipherSuite cipherSuite) => cipherSuite == TlsCipherSuite.Aes128GcmSha256 ? 16 : 32;

    /// <summary>
    /// Protects an Initial, 0-RTT or Handshake packet: seals its payload, makes its Length field
    /// cover the AEAD tag too, and applies header protection.
    /// </summary>
    /// <param name="packet">The packet in the clear; its <c>PacketNumber</c> is <paramref name="packetNumber"/>'s low bytes.</param>
    /// <param name="packetNumber">The full packet number, which forms the nonce.</param>
    /// <returns>The protected packet's bytes.</returns>
    /// <exception cref="ArgumentException">
    /// A field holds a value RFC 9000 does not allow, the packet number does not match
    /// <paramref name="packetNumber"/>, or the packet number and payload together are shorter
    /// than 4 bytes, too short to sample (RFC 9001 section 5.4.2: pad the payload).
    /// </exception>
    public byte[] Protect(QuicLongHeaderPacket packet, ulong packetNumber)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var bytes = (packet with { Payload = new byte[packet.Payload.Length + AeadTagLength] }).ToBytes();
        return Protect(bytes, packet.Payload.Span, packet.PacketNumberLength, packet.PacketNumber, packetNumber, LongHeaderProtectedBits);
    }

    /// <summary>Protects a 1-RTT packet: seals its payload and applies header protection.</summary>
    /// <param name="packet">The packet in the clear; its <c>PacketNumber</c> is <paramref name="packetNumber"/>'s low bytes.</param>
    /// <param name="packetNumber">The full packet number, which forms the nonce.</param>
    /// <returns>The protected packet's bytes.</returns>
    /// <exception cref="ArgumentException">
    /// A field holds a value RFC 9000 does not allow, the packet number does not match
    /// <paramref name="packetNumber"/>, or the packet number and payload together are shorter
    /// than 4 bytes, too short to sample (RFC 9001 section 5.4.2: pad the payload).
    /// </exception>
    public byte[] Protect(QuicShortHeaderPacket packet, ulong packetNumber)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var bytes = (packet with { Payload = new byte[packet.Payload.Length + AeadTagLength] }).ToBytes();
        return Protect(bytes, packet.Payload.Span, packet.PacketNumberLength, packet.PacketNumber, packetNumber, ShortHeaderProtectedBits);
    }

    /// <summary>
    /// Removes header protection from the packet at the start of <paramref name="datagram"/>,
    /// recovers its full packet number and opens its payload. A packet that does not open is
    /// reported as <see cref="QuicUnprotectStatus.Undecryptable"/>, never thrown: the caller drops
    /// it (RFC 9001 section 5.5) and reads the next coalesced packet after
    /// <see cref="QuicUnprotectResult.PacketLength"/> bytes.
    /// </summary>
    /// <param name="datagram">The datagram, or what is left of it after earlier coalesced packets.</param>
    /// <param name="shortHeaderDestinationConnectionIdLength">The length of the connection IDs this endpoint issued, 0 to 20; a short header does not carry it.</param>
    /// <param name="expectedPacketNumber">One more than the largest packet number unprotected in this packet number space, or 0 before any.</param>
    /// <returns>The packet in the clear, or why there is none.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shortHeaderDestinationConnectionIdLength"/> is not 0 to 20, or <paramref name="expectedPacketNumber"/> is above 2^62.</exception>
    /// <exception cref="QuicTransportException">
    /// The header cannot be located - it ends early, its fixed bit is 0, a connection ID is over
    /// 20 bytes, or it is not a version 1 Initial, 0-RTT, Handshake or 1-RTT packet - with
    /// <c>PROTOCOL_VIOLATION</c> and no frame type: the caller drops the packet.
    /// </exception>
    public QuicUnprotectResult Unprotect(ReadOnlyMemory<byte> datagram, int shortHeaderDestinationConnectionIdLength, ulong expectedPacketNumber) =>
        Unprotect(datagram, shortHeaderDestinationConnectionIdLength, expectedPacketNumber, (_, _) => this);

    /// <summary>
    /// Removes header protection with this protection's header-protection key, then opens the
    /// payload with the protection <paramref name="payloadProtectionFor"/> picks from the key phase
    /// bit (false for a long header) and the full packet number (RFC 9001 section 6.3).
    /// </summary>
    internal QuicUnprotectResult Unprotect(ReadOnlyMemory<byte> datagram, int shortHeaderDestinationConnectionIdLength, ulong expectedPacketNumber, Func<bool, ulong, QuicPacketProtection> payloadProtectionFor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shortHeaderDestinationConnectionIdLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shortHeaderDestinationConnectionIdLength, QuicConnectionIds.MaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expectedPacketNumber, QuicPacketNumber.MaxValue + 1);
        var (packetNumberOffset, packetLength) = LocatePacketNumber(datagram, shortHeaderDestinationConnectionIdLength);
        if (packetLength - packetNumberOffset < SampleOffsetFromPacketNumber + HeaderProtectionSampleLength)
        {
            return new QuicUnprotectResult(QuicUnprotectStatus.Undecryptable, null, 0, packetLength);
        }

        var packet = datagram[..packetLength].ToArray();
        var isLongHeader = (packet[0] & QuicPacketHeaderBits.HeaderForm) != 0;
        var mask = cipher.ComputeHeaderProtectionMask(packet.AsSpan(packetNumberOffset + SampleOffsetFromPacketNumber, HeaderProtectionSampleLength));
        packet[0] ^= (byte)(mask[0] & (isLongHeader ? LongHeaderProtectedBits : ShortHeaderProtectedBits));
        var packetNumberLength = QuicPacketHeaderBits.PacketNumberLength(packet[0]);
        var packetNumber = QuicPacketNumber.Decode(expectedPacketNumber, RemovePacketNumberMask(packet, packetNumberOffset, packetNumberLength, mask), packetNumberLength);
        var payloadOffset = packetNumberOffset + packetNumberLength;
        var plaintext = new byte[packetLength - payloadOffset - AeadTagLength];
        var payloadProtection = payloadProtectionFor(!isLongHeader && (packet[0] & QuicPacketHeaderBits.ShortKeyPhaseBit) != 0, packetNumber);
        if (!payloadProtection.cipher.TryOpen(payloadProtection.Nonce(packetNumber), packet.AsSpan(payloadOffset, plaintext.Length), packet.AsSpan(packetLength - AeadTagLength), packet.AsSpan(0, payloadOffset), plaintext))
        {
            return new QuicUnprotectResult(QuicUnprotectStatus.Undecryptable, null, packetNumber, packetLength);
        }

        if ((packet[0] & (isLongHeader ? QuicPacketHeaderBits.LongReservedBits : QuicPacketHeaderBits.ShortReservedBits)) != 0)
        {
            return new QuicUnprotectResult(QuicUnprotectStatus.ReservedBitsNotZero, null, packetNumber, packetLength);
        }

        plaintext.CopyTo(packet.AsSpan(payloadOffset));
        return new QuicUnprotectResult(QuicUnprotectStatus.Unprotected, ReadInTheClear(packet, shortHeaderDestinationConnectionIdLength), packetNumber, packetLength);
    }

    /// <inheritdoc/>
    public void Dispose() => cipher.Dispose();

    /// <summary>Finds where a protected packet's packet number starts and where the packet ends, from the fields header protection leaves alone.</summary>
    private static (int PacketNumberOffset, int PacketLength) LocatePacketNumber(ReadOnlyMemory<byte> datagram, int shortHeaderDestinationConnectionIdLength)
    {
        var reader = new QuicReader(datagram, QuicTransportErrorCode.ProtocolViolation);
        var firstByte = reader.ReadByte();
        if ((firstByte & QuicPacketHeaderBits.HeaderForm) == 0)
        {
            QuicPacketHeaderBits.RequireFixedBit(reader, firstByte);
            reader.ReadBytes(shortHeaderDestinationConnectionIdLength);
            return (reader.Position, datagram.Length);
        }

        if (reader.ReadUInt32() != QuicVersions.Version1)
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, "Only a QUIC version 1 packet carries packet protection.");
        }

        QuicPacketHeaderBits.RequireFixedBit(reader, firstByte);
        var typeBits = (firstByte & QuicPacketHeaderBits.LongPacketTypeMask) >> QuicPacketHeaderBits.LongPacketTypeShift;
        if (typeBits == QuicPacketHeaderBits.RetryTypeBits)
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, "A Retry packet carries no packet protection.");
        }

        QuicPacketReader.ReadConnectionId(reader, QuicVersions.Version1);
        QuicPacketReader.ReadConnectionId(reader, QuicVersions.Version1);
        if (typeBits == (int)QuicPacketType.Initial)
        {
            reader.ReadLengthPrefixedBytes();
        }

        var length = reader.ReadVariableLengthInteger();
        var packetNumberOffset = reader.Position;
        reader.ReadBytes(length);
        return (packetNumberOffset, reader.Position);
    }

    /// <summary>Removes the mask from the packet number's bytes in place and returns them as a number.</summary>
    private static uint RemovePacketNumberMask(byte[] packet, int packetNumberOffset, int packetNumberLength, byte[] mask)
    {
        uint truncated = 0;
        for (var index = 0; index < packetNumberLength; index++)
        {
            packet[packetNumberOffset + index] ^= mask[1 + index];
            truncated = (truncated << 8) | packet[packetNumberOffset + index];
        }

        return truncated;
    }

    /// <summary>Reads an unprotected packet, whose payload holds the plaintext then the tag, and drops the tag from its payload.</summary>
    private static QuicPacket ReadInTheClear(byte[] packet, int shortHeaderDestinationConnectionIdLength) =>
        QuicPacketReader.Read(packet, shortHeaderDestinationConnectionIdLength, out _) switch
        {
            QuicLongHeaderPacket longHeader => longHeader with { Payload = longHeader.Payload[..^AeadTagLength] },
            var shortHeader => (QuicShortHeaderPacket)shortHeader with { Payload = ((QuicShortHeaderPacket)shortHeader).Payload[..^AeadTagLength] },
        };

    private byte[] Protect(byte[] bytes, ReadOnlySpan<byte> plaintext, int packetNumberLength, uint truncatedPacketNumber, ulong packetNumber, byte protectedBits)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(packetNumber, QuicPacketNumber.MaxValue);
        if (QuicPacketNumber.Truncate(packetNumber, packetNumberLength) != truncatedPacketNumber)
        {
            throw new ArgumentException($"The packet carries packet number {truncatedPacketNumber}, which is not the low {packetNumberLength} bytes of {packetNumber}.", nameof(packetNumber));
        }

        if (packetNumberLength + plaintext.Length < SampleOffsetFromPacketNumber)
        {
            throw new ArgumentException("The packet number and payload together are shorter than 4 bytes, too short to sample for header protection; pad the payload (RFC 9001 section 5.4.2).", "packet");
        }

        var payloadOffset = bytes.Length - AeadTagLength - plaintext.Length;
        var packetNumberOffset = payloadOffset - packetNumberLength;
        cipher.Seal(Nonce(packetNumber), plaintext, bytes.AsSpan(0, payloadOffset), bytes.AsSpan(payloadOffset, plaintext.Length), bytes.AsSpan(bytes.Length - AeadTagLength));
        var mask = cipher.ComputeHeaderProtectionMask(bytes.AsSpan(packetNumberOffset + SampleOffsetFromPacketNumber, HeaderProtectionSampleLength));
        bytes[0] ^= (byte)(mask[0] & protectedBits);
        for (var index = 0; index < packetNumberLength; index++)
        {
            bytes[packetNumberOffset + index] ^= mask[1 + index];
        }

        return bytes;
    }

    /// <summary>The nonce: the IV exclusive-ored with the packet number, left-padded to 12 bytes (RFC 9001 section 5.3).</summary>
    private byte[] Nonce(ulong packetNumber)
    {
        var nonce = iv.ToArray();
        Span<byte> packetNumberBytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(packetNumberBytes, packetNumber);
        for (var index = 0; index < packetNumberBytes.Length; index++)
        {
            nonce[IvLength - sizeof(ulong) + index] ^= packetNumberBytes[index];
        }

        return nonce;
    }
}
