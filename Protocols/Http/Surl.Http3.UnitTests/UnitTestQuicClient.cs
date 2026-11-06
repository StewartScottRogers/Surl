using System.Runtime.InteropServices;
using Surl.Quic;
using Surl.Tls13;

namespace Surl.Http3;

/// <summary>
/// The client side of a QUIC handshake, for tests. It starts from a pinned upstream curl's
/// recorded Initial datagrams: it opens them with the client's Initial keys, puts its own x25519
/// public key where curl's was in the ClientHello (and applies any other same-length edit a test
/// asks for) inside the CRYPTO frames, and seals them again with the same headers and packet
/// numbers - every other byte stays curl's. It then opens the server's datagrams at every level,
/// completes the TLS handshake with <see cref="UnitTestTls13Client"/>, and seals packets of its own.
/// Packet protection is Surl.Quic's own <see cref="QuicPacketProtection"/>, checked against RFC
/// 9001 Appendix A in BL-374; the handshake logic is the test's.
/// </summary>
internal sealed class UnitTestQuicClient : IDisposable
{
    private readonly QuicPacketProtection initialSend;
    private readonly QuicPacketProtection initialReceive;
    private readonly Dictionary<QuicPacketType, List<(ulong Offset, byte[] Data)>> crypto = new()
    {
        [QuicPacketType.Initial] = [],
        [QuicPacketType.Handshake] = [],
    };

    private readonly Dictionary<QuicPacketType, ulong> largestReceived = [];
    private readonly List<byte[]> undecrypted = [];
    private QuicPacketProtection? handshakeSend;
    private QuicPacketProtection? handshakeReceive;
    private QuicPacketProtection? oneRttSend;
    private QuicPacketProtection? oneRttReceive;
    private ulong nextInitialPacketNumber;
    private ulong nextHandshakePacketNumber;
    private ulong nextOneRttPacketNumber;

    private UnitTestQuicClient(byte[] destinationConnectionId, byte[] sourceConnectionId)
    {
        OriginalDestinationConnectionId = destinationConnectionId;
        SourceConnectionId = sourceConnectionId;
        (initialSend, initialReceive) = QuicInitialSecrets.DerivePacketProtection(destinationConnectionId);
    }

    public UnitTestTls13Client Tls { get; } = new();

    public byte[] OriginalDestinationConnectionId { get; }

    public byte[] SourceConnectionId { get; }

    /// <summary>The recorded Initial datagrams with this client's key share (and the test's edit) in place.</summary>
    public List<byte[]> InitialDatagrams { get; } = [];

    /// <summary>The ClientHello as these datagrams carry it.</summary>
    public byte[] ClientHello { get; private set; } = [];

    /// <summary>The server's connection ID, from the Source Connection ID of its first long header packet.</summary>
    public byte[]? ServerConnectionId { get; private set; }

    /// <summary>Every frame the server sent, by the packet type that carried it.</summary>
    public Dictionary<QuicPacketType, List<QuicFrame>> FramesReceived { get; } = new()
    {
        [QuicPacketType.Initial] = [],
        [QuicPacketType.Handshake] = [],
        [QuicPacketType.OneRtt] = [],
    };

    /// <summary>The packet numbers of the server's packets the client opened, by packet type.</summary>
    public Dictionary<QuicPacketType, SortedSet<ulong>> PacketNumbersReceived { get; } = new()
    {
        [QuicPacketType.Initial] = [],
        [QuicPacketType.Handshake] = [],
        [QuicPacketType.OneRtt] = [],
    };

    /// <summary>An ACK frame for every packet of <paramref name="type"/> the client opened, ranges largest first, ACK Delay 0.</summary>
    public QuicAckFrame AckFrame(QuicPacketType type)
    {
        var ranges = new List<QuicAckRange>();
        foreach (var packetNumber in PacketNumbersReceived[type].Reverse())
        {
            if (ranges.Count > 0 && ranges[^1].Smallest == packetNumber + 1)
            {
                ranges[^1] = ranges[^1] with { Smallest = packetNumber };
            }
            else
            {
                ranges.Add(new QuicAckRange(packetNumber, packetNumber));
            }
        }

        return new QuicAckFrame(ranges, 0, null);
    }

    /// <summary>The client's Finished, once the server's flight has verified.</summary>
    public byte[]? ClientFinished { get; private set; }

    public static UnitTestQuicClient FromRecording(List<byte[]> recorded, Func<byte[], byte[]>? editClientHello = null)
    {
        var first = recorded[0];
        var destinationLength = first[5];
        var destination = first[6..(6 + destinationLength)];
        var source = first[(7 + destinationLength)..(7 + destinationLength + first[6 + destinationLength])];
        var client = new UnitTestQuicClient(destination, source);
        client.ReplaceKeyShare(recorded, editClientHello ?? (hello => hello));
        return client;
    }

    /// <summary>Opens each of the server's datagrams and handles the CRYPTO data that becomes contiguous.</summary>
    public void Receive(IEnumerable<byte[]> datagrams)
    {
        foreach (var datagram in datagrams)
        {
            Assert.IsLessThanOrEqualTo(QuicServerConnection.MaxDatagramSize, datagram.Length);
            OpenDatagram(datagram);
        }

        AdvanceHandshake();
        foreach (var datagram in undecrypted.ToArray())
        {
            undecrypted.Remove(datagram);
            OpenDatagram(datagram);
        }

        AdvanceHandshake();
    }

    /// <summary>A Handshake packet carrying the client's Finished.</summary>
    public byte[] FinishedDatagram()
    {
        Assert.IsNotNull(ClientFinished, "The server's flight has not verified yet.");
        return SealHandshake(new QuicCryptoFrame(0, ClientFinished));
    }

    /// <summary>An Initial packet padded to exactly <paramref name="datagramLength"/> bytes.</summary>
    public byte[] SealInitial(byte[] destinationConnectionId, int datagramLength, params QuicFrame[] frames)
    {
        var packetNumber = nextInitialPacketNumber++;
        return initialSend.Protect(PaddedInitialPacket(destinationConnectionId, datagramLength, frames, packetNumber), packetNumber);
    }

    /// <summary>
    /// A 1200-byte Initial packet whose reserved bits are set before it is sealed, so it opens but
    /// breaks RFC 9000 section 17.2. Sealed here with the BCL's AES-128-GCM and AES-ECB (RFC 9001
    /// sections 5.3 and 5.4.3), since Surl.Quic never writes reserved bits.
    /// </summary>
    public byte[] SealInitialWithReservedBitsSet(byte[] destinationConnectionId, params QuicFrame[] frames)
    {
        var packetNumber = nextInitialPacketNumber++;
        var packet = PaddedInitialPacket(destinationConnectionId, QuicServerConnection.MaxDatagramSize, frames, packetNumber);
        var bytes = (packet with { Payload = new byte[packet.Payload.Length + QuicPacketProtection.AeadTagLength] }).ToBytes();
        bytes[0] |= 0x0c;
        var payloadOffset = bytes.Length - QuicPacketProtection.AeadTagLength - packet.Payload.Length;
        var packetNumberOffset = payloadOffset - packet.PacketNumberLength;
        var nonce = initialSend.Iv.ToArray();
        for (var index = 0; index < 8; index++)
        {
            nonce[4 + index] ^= (byte)(packetNumber >> (56 - (8 * index)));
        }

        using (var aead = new System.Security.Cryptography.AesGcm(initialSend.Key.Span, QuicPacketProtection.AeadTagLength))
        {
            aead.Encrypt(nonce, packet.Payload.Span, bytes.AsSpan(payloadOffset, packet.Payload.Length), bytes.AsSpan(bytes.Length - QuicPacketProtection.AeadTagLength), bytes.AsSpan(0, payloadOffset));
        }

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = initialSend.HeaderProtectionKey.ToArray();
        var mask = aes.EncryptEcb(bytes.AsSpan(packetNumberOffset + 4, 16), System.Security.Cryptography.PaddingMode.None);
        bytes[0] ^= (byte)(mask[0] & 0x0f);
        for (var index = 0; index < packet.PacketNumberLength; index++)
        {
            bytes[packetNumberOffset + index] ^= mask[1 + index];
        }

        return bytes;
    }

    private QuicLongHeaderPacket PaddedInitialPacket(byte[] destinationConnectionId, int datagramLength, QuicFrame[] frames, ulong packetNumber)
    {
        var padding = datagramLength - InitialPacket(destinationConnectionId, frames, packetNumber).ToBytes().Length - QuicPacketProtection.AeadTagLength;
        for (var attempt = 0; attempt < 2 && padding > 0; attempt++)
        {
            var length = InitialPacket(destinationConnectionId, [.. frames, new QuicPaddingFrame(padding)], packetNumber).ToBytes().Length + QuicPacketProtection.AeadTagLength;
            padding -= length - datagramLength;
        }

        return InitialPacket(destinationConnectionId, padding > 0 ? [.. frames, new QuicPaddingFrame(padding)] : frames, packetNumber);
    }

    public byte[] SealHandshake(params QuicFrame[] frames)
    {
        var packetNumber = nextHandshakePacketNumber++;
        var packet = new QuicLongHeaderPacket(QuicPacketType.Handshake, ServerConnectionId!, SourceConnectionId, ReadOnlyMemory<byte>.Empty, 4, (uint)packetNumber, QuicFrameWriter.Write(frames));
        return handshakeSend!.Protect(packet, packetNumber);
    }

    /// <summary>The packet number the next 1-RTT packet this client seals takes.</summary>
    public ulong NextOneRttPacketNumber => nextOneRttPacketNumber;

    public byte[] SealOneRtt(params QuicFrame[] frames) => SealOneRttPayload(QuicFrameWriter.Write(frames));

    /// <summary>A 1-RTT packet whose payload is exactly <paramref name="payload"/>, well-formed frames or not.</summary>
    public byte[] SealOneRttPayload(byte[] payload, byte[]? destinationConnectionId = null)
    {
        var packetNumber = nextOneRttPacketNumber++;
        var packet = new QuicShortHeaderPacket(destinationConnectionId ?? ServerConnectionId!, false, false, 4, (uint)packetNumber, payload);
        return oneRttSend!.Protect(packet, packetNumber);
    }

    public void Dispose()
    {
        initialSend.Dispose();
        initialReceive.Dispose();
        handshakeSend?.Dispose();
        handshakeReceive?.Dispose();
        oneRttSend?.Dispose();
        oneRttReceive?.Dispose();
    }

    private QuicLongHeaderPacket InitialPacket(byte[] destinationConnectionId, QuicFrame[] frames, ulong packetNumber) =>
        new(QuicPacketType.Initial, destinationConnectionId, SourceConnectionId, ReadOnlyMemory<byte>.Empty, 4, (uint)packetNumber, QuicFrameWriter.Write(frames));

    private void ReplaceKeyShare(List<byte[]> recorded, Func<byte[], byte[]> editClientHello)
    {
        var opened = recorded.Select(datagram => initialSend.Unprotect(datagram, 0, 0)).ToList();
        var original = Reassemble(opened.SelectMany(result => QuicFrameReader.Read(((QuicLongHeaderPacket)result.Packet!).Payload, QuicPacketType.Initial)).OfType<QuicCryptoFrame>());
        ClientHello = editClientHello(WithOwnKeyShares(original));
        Assert.HasCount(original.Length, ClientHello, "An edit must keep the ClientHello's length.");
        foreach (var result in opened)
        {
            var packet = (QuicLongHeaderPacket)result.Packet!;
            foreach (var frame in QuicFrameReader.Read(packet.Payload, QuicPacketType.Initial).OfType<QuicCryptoFrame>())
            {
                Assert.IsTrue(MemoryMarshal.TryGetArray(frame.Data, out var segment));
                ClientHello.AsSpan((int)frame.Offset, frame.Data.Length).CopyTo(segment.AsSpan());
            }

            // Curl writes the Length field in four bytes and Surl.Quic writes the shortest, two, so the
            // final PADDING run grows by what the header shrank: the datagram keeps curl's size.
            var shortfall = result.PacketLength - initialSend.Protect(packet, result.PacketNumber).Length;
            var resealed = initialSend.Protect(packet with { Payload = (byte[])[.. packet.Payload.Span, .. new byte[shortfall]] }, result.PacketNumber);
            Assert.HasCount(result.PacketLength, resealed);
            InitialDatagrams.Add(resealed);
        }

        nextInitialPacketNumber = (ulong)recorded.Count;
    }

    /// <summary>
    /// The recorded ClientHello with this client's own key_exchange values in place of curl's: its
    /// x25519 key, and its X25519MLKEM768 share where curl sent one. Each share is found by its
    /// whole entry (group, length, key), because curl may reuse one x25519 key in both shares.
    /// </summary>
    private byte[] WithOwnKeyShares(byte[] original)
    {
        var withOwnShares = original.ToArray();
        foreach (var entry in Surl.Tls13.ClientHello.Read(original).KeyShares!)
        {
            var own = entry.Group switch
            {
                TlsNamedGroup.X25519 => Tls.PublicKey,
                TlsNamedGroup.X25519MlKem768 => Tls.X25519MlKem768Share,
                _ => null,
            };
            if (own is not null)
            {
                byte[] header = [(byte)((ushort)entry.Group >> 8), (byte)entry.Group, (byte)(entry.KeyExchange.Length >> 8), (byte)entry.KeyExchange.Length];
                own.CopyTo(withOwnShares, original.AsSpan().IndexOf([.. header, .. entry.KeyExchange.Span]) + header.Length);
            }
        }

        return withOwnShares;
    }
    private void OpenDatagram(byte[] datagram)
    {
        var remaining = (ReadOnlyMemory<byte>)datagram;
        while (!remaining.IsEmpty)
        {
            var (type, protection) = ProtectionFor(remaining.Span[0]);
            if (protection is null)
            {
                undecrypted.Add(remaining.ToArray());
                return;
            }

            var result = protection.Unprotect(remaining, SourceConnectionId.Length, largestReceived.TryGetValue(type, out var largest) ? largest + 1 : 0);
            Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status, $"A {type} packet from the server does not open.");
            largestReceived[type] = Math.Max(result.PacketNumber, largestReceived.GetValueOrDefault(type));
            PacketNumbersReceived[type].Add(result.PacketNumber);
            TakeFrames(type, result.Packet!);
            remaining = remaining[result.PacketLength..];
        }
    }

    private (QuicPacketType Type, QuicPacketProtection? Protection) ProtectionFor(byte firstByte) =>
        (firstByte & 0x80) == 0 ? (QuicPacketType.OneRtt, oneRttReceive)
        : ((firstByte & 0x30) >> 4) == 0 ? (QuicPacketType.Initial, initialReceive)
        : (QuicPacketType.Handshake, handshakeReceive);

    private void TakeFrames(QuicPacketType type, QuicPacket packet)
    {
        if (packet is QuicLongHeaderPacket longHeader)
        {
            CollectionAssert.AreEqual(SourceConnectionId, longHeader.DestinationConnectionId.ToArray());
            ServerConnectionId ??= longHeader.SourceConnectionId.ToArray();
            CollectionAssert.AreEqual(ServerConnectionId, longHeader.SourceConnectionId.ToArray());
        }

        var payload = packet is QuicLongHeaderPacket withLength ? withLength.Payload : ((QuicShortHeaderPacket)packet).Payload;
        foreach (var frame in QuicFrameReader.Read(payload, type))
        {
            FramesReceived[type].Add(frame);
            if (frame is QuicCryptoFrame cryptoFrame)
            {
                crypto[type].Add((cryptoFrame.Offset, cryptoFrame.Data.ToArray()));
            }
        }
    }

    private void AdvanceHandshake()
    {
        if (handshakeReceive is null && TryReassemble(QuicPacketType.Initial) is { } serverHello && HoldsWholeMessages(serverHello, 1))
        {
            Tls.ReceiveServerHello(ClientHello, serverHello);
            var suite = (TlsCipherSuite)Tls.CipherSuite;
            handshakeSend = QuicPacketProtection.FromTrafficSecret(suite, Tls.ClientHandshakeSecret);
            handshakeReceive = QuicPacketProtection.FromTrafficSecret(suite, Tls.ServerHandshakeSecret);
        }

        if (ClientFinished is null && handshakeReceive is not null && TryReassemble(QuicPacketType.Handshake) is { } flight && HoldsWholeMessages(flight, 4))
        {
            ClientFinished = Tls.ReceiveServerFlight(flight);
            var suite = (TlsCipherSuite)Tls.CipherSuite;
            oneRttSend = QuicPacketProtection.FromTrafficSecret(suite, Tls.ClientApplicationSecret);
            oneRttReceive = QuicPacketProtection.FromTrafficSecret(suite, Tls.ServerApplicationSecret);
        }
    }

    /// <summary>Whether the bytes are exactly <paramref name="messages"/> whole handshake messages.</summary>
    private static bool HoldsWholeMessages(byte[] bytes, int messages)
    {
        var offset = 0;
        var count = 0;
        while (offset + 4 <= bytes.Length)
        {
            offset += 4 + ((bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
            count++;
        }

        return offset == bytes.Length && count == messages;
    }

    private byte[]? TryReassemble(QuicPacketType type)
    {
        var fragments = crypto[type];
        return fragments.Count == 0 ? null : Reassemble(fragments.Select(fragment => new QuicCryptoFrame(fragment.Offset, fragment.Data)));
    }

    private static byte[] Reassemble(IEnumerable<QuicCryptoFrame> frames)
    {
        var bytes = new List<byte>();
        foreach (var frame in frames.OrderBy(frame => frame.Offset))
        {
            var start = (int)frame.Offset;
            if (start <= bytes.Count && start + frame.Data.Length > bytes.Count)
            {
                bytes.AddRange(frame.Data.Span[(bytes.Count - start)..]);
            }
        }

        return [.. bytes];
    }
}
