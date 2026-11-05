namespace Surl.Quic;

/// <summary>How the connection fills, pads and seals the datagrams it sends.</summary>
public sealed partial class QuicServerConnection
{
    private const int AmplificationFactor = 3;
    private const int MinPacketNumberAndPayloadLength = 4;

    /// <summary>The least data a CRYPTO or STREAM frame split to fit a packet gives the packet; less waits for the next packet.</summary>
    private const int MinSplitDataLength = 64;

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
        var inFlightBudget = (int)Math.Min(budget, CongestionBudget);
        var packets = new List<(QuicPacketNumberSpace Space, List<QuicFrame> Frames)>();
        var used = 0;
        foreach (var space in (QuicPacketNumberSpace[])[initial, handshake, application])
        {
            var frames = space.HasSomethingToSend ? FillPacket(space, budget - used, inFlightBudget - used, inFlightBudget == MaxDatagramSize) : [];
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
    /// Takes what fits from the space's waiting ACK, in <paramref name="ackRoom"/> bytes, and its
    /// CRYPTO data and frames, which count in flight, in <paramref name="room"/> bytes. Initial
    /// CRYPTO data and other ack-eliciting Initial frames go only in a datagram that may be padded
    /// to 1200 bytes.
    /// </summary>
    private List<QuicFrame> FillPacket(QuicPacketNumberSpace space, int ackRoom, int room, bool mayBePaddedToFullSize)
    {
        var frames = new List<QuicFrame>();
        if (space.AckPending)
        {
            space.AckPending = !TryAddFrame(space, frames, new QuicAckFrame(space.Received.Ranges.ToArray(), 0, null), ackRoom);
        }

        var mayElicit = space != initial || mayBePaddedToFullSize;
        if (space.CryptoToSend.Count > 0 && mayElicit)
        {
            AddCrypto(space, frames, room);
        }

        while (space.FramesToSend.Count > 0 && (mayElicit || !IsAckEliciting(space.FramesToSend[0])) && TryAddWaitingFrame(space, frames, room))
        {
            // TryAddWaitingFrame has moved the first waiting frame, or its first part, into the packet.
        }

        return frames;
    }

    /// <summary>
    /// Moves the space's first waiting frame into the packet when it fits in <paramref name="available"/>
    /// bytes; a CRYPTO or STREAM frame too long for the room left, as one sent again after loss can
    /// be, is split and its first part moved, when that part carries at least <see cref="MinSplitDataLength"/> bytes.
    /// </summary>
    private bool TryAddWaitingFrame(QuicPacketNumberSpace space, List<QuicFrame> frames, int available)
    {
        var waiting = space.FramesToSend[0];
        if (TryAddFrame(space, frames, waiting, available))
        {
            space.FramesToSend.RemoveAt(0);
            return true;
        }

        frames.Add(waiting);
        var overshoot = PacketLength(space, frames) - available;
        frames.RemoveAt(frames.Count - 1);
        if (Split(waiting, overshoot) is not { } parts || !TryAddFrame(space, frames, parts.First, available))
        {
            return false;
        }

        space.FramesToSend[0] = parts.Remainder;
        return true;
    }

    /// <summary>Splits a CRYPTO or STREAM frame into a first part <paramref name="overshoot"/> bytes of data shorter and the remainder; null for another frame or a first part under <see cref="MinSplitDataLength"/> bytes.</summary>
    private static (QuicFrame First, QuicFrame Remainder)? Split(QuicFrame frame, int overshoot) => frame switch
    {
        QuicCryptoFrame crypto when crypto.Data.Length - overshoot >= MinSplitDataLength => SplitAt(crypto, crypto.Data.Length - overshoot),
        QuicStreamFrame stream when stream.Data.Length - overshoot >= MinSplitDataLength => SplitAt(stream, stream.Data.Length - overshoot),
        _ => null,
    };

    private static (QuicFrame First, QuicFrame Remainder) SplitAt(QuicCryptoFrame crypto, int keep) =>
        (new QuicCryptoFrame(crypto.Offset, crypto.Data[..keep]), new QuicCryptoFrame(crypto.Offset + (ulong)keep, crypto.Data[keep..]));

    private static (QuicFrame First, QuicFrame Remainder) SplitAt(QuicStreamFrame stream, int keep) =>
        (new QuicStreamFrame(stream.StreamId, stream.Offset, stream.Data[..keep], false), new QuicStreamFrame(stream.StreamId, stream.Offset + (ulong)keep, stream.Data[keep..], stream.Fin));

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
            var sealedPacket = SealPacket(space, frames, packetNumber);
            datagram.AddRange(sealedPacket);
            NoteAckElicitingSent(frames);
            NoteOneRttAckSent(space, frames);
            RecordSent(space, packetNumber, frames, sealedPacket.Length);
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

    /// <summary>A 1-RTT packet carrying an ACK lets the client update its keys again (RFC 9001 section 6.1).</summary>
    private void NoteOneRttAckSent(QuicPacketNumberSpace space, List<QuicFrame> frames)
    {
        if (space == application && frames.Exists(frame => frame is QuicAckFrame))
        {
            oneRttKeyPhase!.NoteAckSent();
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
            ? new QuicShortHeaderPacket(clientConnectionId, false, oneRttKeyPhase!.KeyPhase, packetNumberLength, truncated, payload)
            : new QuicLongHeaderPacket(space.PacketType, clientConnectionId, serverConnectionId, ReadOnlyMemory<byte>.Empty, packetNumberLength, truncated, payload);
    }

    /// <summary>The fewest bytes that encode twice the packets not yet acknowledged (RFC 9000 section 17.1, Appendix A.2).</summary>
    private static int PacketNumberLength(ulong packetNumber, ulong? largestAcknowledged)
    {
        var unacknowledged = largestAcknowledged is { } largest ? packetNumber - largest : packetNumber + 1;
        return Math.Min(4, (System.Numerics.BitOperations.Log2(unacknowledged) + 9) / 8);
    }
}
