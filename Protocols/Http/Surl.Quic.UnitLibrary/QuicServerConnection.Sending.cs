namespace Surl.Quic;

/// <summary>How the connection fills, pads and seals the datagrams it sends.</summary>
public sealed partial class QuicServerConnection
{
    private const int AmplificationFactor = 3;
    private const int MinPacketNumberAndPayloadLength = 4;

    /// <summary>
    /// The most a CRYPTO frame and the packet's Length field can grow by once data is added to an
    /// empty CRYPTO frame: two bytes of data length and one of the Length field.
    /// </summary>
    private const int CryptoFrameGrowth = 3;

    /// <summary>
    /// Puts what the streams have waiting into 1-RTT frames once the handshake is complete, then
    /// builds datagrams until nothing is left to send, or the anti-amplification limit stops it.
    /// </summary>
    private List<byte[]> Flush()
    {
        if (IsHandshakeComplete && !IsClosed)
        {
            streams!.QueueFrames(application.FramesToSend);
        }

        var datagrams = new List<byte[]>();
        while (BuildDatagram() is { } datagram)
        {
            datagrams.Add(datagram);
        }

        return datagrams;
    }

    /// <summary>
    /// The most the next datagram may hold: 1200 bytes, and before the client's address is
    /// validated no more than three times what was received less what was sent (RFC 9000 section 8.1).
    /// </summary>
    private int NextDatagramBudget() =>
        addressValidated ? MaxDatagramSize : (int)Math.Min(MaxDatagramSize, (AmplificationFactor * bytesReceived) - bytesSent);

    private byte[]? BuildDatagram()
    {
        var budget = NextDatagramBudget();
        var packets = new List<(QuicPacketNumberSpace Space, List<QuicFrame> Frames)>();
        var used = 0;
        foreach (var space in (QuicPacketNumberSpace[])[initial, handshake, application])
        {
            var frames = space.HasSomethingToSend ? FillPacket(space, budget - used, budget == MaxDatagramSize) : [];
            if (frames.Count > 0)
            {
                packets.Add((space, frames));
                used += PacketLength(space, frames);
            }
        }

        if (packets.Count == 0)
        {
            return null;
        }

        PadInitialDatagram(packets, used);
        return Seal(packets);
    }

    /// <summary>
    /// Takes what fits in <paramref name="available"/> bytes from the space's waiting ACK, CRYPTO
    /// data and frames. Initial CRYPTO data goes only in a datagram that may be padded to 1200 bytes.
    /// </summary>
    private List<QuicFrame> FillPacket(QuicPacketNumberSpace space, int available, bool mayBePaddedToFullSize)
    {
        var frames = new List<QuicFrame>();
        if (space.AckPending)
        {
            space.AckPending = !TryAddFrame(space, frames, new QuicAckFrame(space.Received.Ranges.ToArray(), 0, null), available);
        }

        if (space.CryptoToSend.Count > 0 && (space != initial || mayBePaddedToFullSize))
        {
            AddCrypto(space, frames, available);
        }

        while (space.FramesToSend.Count > 0 && TryAddFrame(space, frames, space.FramesToSend[0], available))
        {
            space.FramesToSend.RemoveAt(0);
        }

        return frames;
    }

    /// <summary>Adds <paramref name="frame"/> when the packet still fits in <paramref name="available"/> bytes with it.</summary>
    private bool TryAddFrame(QuicPacketNumberSpace space, List<QuicFrame> frames, QuicFrame frame, int available)
    {
        frames.Add(frame);
        if (PacketLength(space, frames) <= available)
        {
            return true;
        }

        frames.RemoveAt(frames.Count - 1);
        return false;
    }

    private static bool ContainsAckEliciting(List<QuicFrame> frames)
    {
        foreach (var frame in frames)
        {
            if (IsAckEliciting(frame))
            {
                return true;
            }
        }

        return false;
    }

    private void AddCrypto(QuicPacketNumberSpace space, List<QuicFrame> frames, int available)
    {
        var overhead = PacketLength(space, [.. frames, new QuicCryptoFrame(space.CryptoSendOffset, ReadOnlyMemory<byte>.Empty)]) + CryptoFrameGrowth;
        var count = Math.Min(available - overhead, space.CryptoToSend.Count);
        if (count > 0)
        {
            frames.Add(new QuicCryptoFrame(space.CryptoSendOffset, space.CryptoToSend.GetRange(0, count).ToArray()));
            space.CryptoToSend.RemoveRange(0, count);
            space.CryptoSendOffset += (ulong)count;
        }
    }

    /// <summary>
    /// Pads a datagram carrying an ack-eliciting Initial packet to 1200 bytes (RFC 9000 section
    /// 14.1), with PADDING at the end of its last packet.
    /// </summary>
    private void PadInitialDatagram(List<(QuicPacketNumberSpace Space, List<QuicFrame> Frames)> packets, int used)
    {
        var carriesAckElicitingInitial = packets[0].Space == initial && ContainsAckEliciting(packets[0].Frames);
        if (!carriesAckElicitingInitial || used >= MaxDatagramSize)
        {
            return;
        }

        var (space, frames) = packets[^1];
        var unpadded = PacketLength(space, frames);
        frames.Add(new QuicPaddingFrame(MaxDatagramSize - used));
        var overshoot = used - unpadded + PacketLength(space, frames) - MaxDatagramSize;
        frames[^1] = new QuicPaddingFrame(Math.Max(1, MaxDatagramSize - used - overshoot));
    }

    private byte[] Seal(List<(QuicPacketNumberSpace Space, List<QuicFrame> Frames)> packets)
    {
        var datagram = new List<byte>();
        foreach (var (space, frames) in packets)
        {
            var packetNumber = space.NextPacketNumber++;
            datagram.AddRange(SealPacket(space, frames, packetNumber));
            NoteAckElicitingSent(frames);
        }

        bytesSent += datagram.Count;
        return [.. datagram];
    }

    /// <summary>The idle timer restarts when an ack-eliciting packet is sent, if none was since the last packet received (RFC 9000 section 10.1).</summary>
    private void NoteAckElicitingSent(List<QuicFrame> frames)
    {
        if (!ackElicitingSentSinceLastReceive && ContainsAckEliciting(frames))
        {
            ackElicitingSentSinceLastReceive = true;
            lastActivity = timeProvider.GetUtcNow();
        }
    }

    private int PacketLength(QuicPacketNumberSpace space, IReadOnlyList<QuicFrame> frames) =>
        PacketInTheClear(space, frames, space.NextPacketNumber) switch
        {
            QuicLongHeaderPacket longHeader => longHeader.ToBytes().Length,
            var shortHeader => ((QuicShortHeaderPacket)shortHeader).ToBytes().Length,
        } + QuicPacketProtection.AeadTagLength;

    private byte[] SealPacket(QuicPacketNumberSpace space, IReadOnlyList<QuicFrame> frames, ulong packetNumber) =>
        PacketInTheClear(space, frames, packetNumber) switch
        {
            QuicLongHeaderPacket longHeader => space.SendProtection!.Protect(longHeader, packetNumber),
            var shortHeader => space.SendProtection!.Protect((QuicShortHeaderPacket)shortHeader, packetNumber),
        };

    /// <summary>
    /// The packet in the clear, its packet number as short as RFC 9000 section 17.1 allows given
    /// what the client has acknowledged, and its payload padded so header protection can sample it.
    /// </summary>
    private QuicPacket PacketInTheClear(QuicPacketNumberSpace space, IReadOnlyList<QuicFrame> frames, ulong packetNumber)
    {
        var packetNumberLength = PacketNumberLength(packetNumber, space.LargestAcknowledged);
        var payload = QuicFrameWriter.Write(frames);
        var shortfall = MinPacketNumberAndPayloadLength - packetNumberLength - payload.Length;
        if (shortfall > 0)
        {
            payload = [.. payload, .. new byte[shortfall]];
        }

        var truncated = QuicPacketNumber.Truncate(packetNumber, packetNumberLength);
        return space.PacketType == QuicPacketType.OneRtt
            ? new QuicShortHeaderPacket(clientConnectionId, false, false, packetNumberLength, truncated, payload)
            : new QuicLongHeaderPacket(space.PacketType, clientConnectionId, serverConnectionId, ReadOnlyMemory<byte>.Empty, packetNumberLength, truncated, payload);
    }

    /// <summary>The fewest bytes that encode twice the packets not yet acknowledged (RFC 9000 section 17.1, Appendix A.2).</summary>
    private static int PacketNumberLength(ulong packetNumber, ulong? largestAcknowledged)
    {
        var unacknowledged = largestAcknowledged is { } largest ? packetNumber - largest : packetNumber + 1;
        return Math.Min(4, (System.Numerics.BitOperations.Log2(unacknowledged) + 9) / 8);
    }
}
