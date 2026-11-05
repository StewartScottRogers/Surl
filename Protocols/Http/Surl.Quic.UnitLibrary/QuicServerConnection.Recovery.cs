namespace Surl.Quic;

/// <summary>How the connection detects lost packets, sends their frames again and probes (RFC 9002, RFC 9000 section 13.3).</summary>
public sealed partial class QuicServerConnection
{
    /// <summary>The <c>ack_delay_exponent</c> assumed when the client sends none (RFC 9000 section 18.2).</summary>
    private const int DefaultAckDelayExponent = 3;

    /// <summary>When the loss detection timer fires, or null when it is not armed.</summary>
    private DateTimeOffset? LossDetectionDeadline => tls is null ? null : recovery.Timer(IsAtAmplificationLimit);

    /// <summary>Whether the anti-amplification limit lets the server send nothing more (RFC 9000 section 8.1).</summary>
    private bool IsAtAmplificationLimit => !addressValidated && AmplificationFactor * bytesReceived <= bytesSent;

    /// <summary>How many bytes the congestion window lets the next datagram put in flight; a probe is not held back by it (RFC 9002 section 7).</summary>
    private long CongestionBudget => probePending ? MaxDatagramSize : recovery.Congestion.AvailableWindow;

    /// <summary>Records a sealed packet for loss detection; an ack-eliciting one uses up a pending probe.</summary>
    private void RecordSent(QuicPacketNumberSpace space, ulong packetNumber, List<QuicFrame> frames, int sentBytes)
    {
        var ackEliciting = ContainsAckEliciting(frames);
        var inFlight = ackEliciting || frames.Exists(frame => frame is QuicPaddingFrame);
        recovery.OnPacketSent(space, new QuicSentPacket(packetNumber, timeProvider.GetUtcNow(), ackEliciting, inFlight, sentBytes, frames));
        probePending &= !ackEliciting;
    }

    /// <summary>Acts on an <c>ACK</c>: acknowledged packets leave flight, and the frames of those it shows lost are sent again.</summary>
    private void DetectLossOnAck(QuicPacketNumberSpace space, QuicAckFrame ack)
    {
        var ackDelay = space == application ? DecodeAckDelay(ack.AckDelay) : TimeSpan.Zero;
        var lost = recovery.OnAckReceived(space, ack, ackDelay, timeProvider.GetUtcNow());
        QueueRetransmission(space, lost);
    }

    /// <summary>
    /// The acknowledgment delay an <c>ACK</c> reports, scaled by the client's
    /// <c>ack_delay_exponent</c> (RFC 9000 section 19.3); a delay beyond the client's
    /// <c>max_ack_delay</c> is taken as that, which RFC 9002 section 5.3 caps it to anyway.
    /// </summary>
    private TimeSpan DecodeAckDelay(ulong encoded)
    {
        var exponent = (int)(PeerTransportParameters!.AckDelayExponent ?? DefaultAckDelayExponent);
        var limit = (ulong)recovery.MaxAckDelay.TotalMicroseconds;
        return TimeSpan.FromMicroseconds((long)(encoded > limit >> exponent ? limit : encoded << exponent));
    }

    /// <summary>Acts on the loss detection timer: declares the packets a loss time condemns lost, or on a probe timeout sends a probe.</summary>
    private void HandleLossDetectionTimeout()
    {
        var (space, lost) = recovery.OnTimeout(timeProvider.GetUtcNow());
        if (lost.Count > 0)
        {
            QueueRetransmission(space, lost);
            return;
        }

        QueueProbe(space);
    }

    /// <summary>
    /// Queues one ack-eliciting probe packet in <paramref name="space"/> (RFC 9002 section 6.2.4):
    /// new data when the space has some waiting, otherwise the frames of its oldest packet in
    /// flight sent again, or a <c>PING</c> when none of them is to be sent again. The probe goes
    /// whatever the congestion window.
    /// </summary>
    private void QueueProbe(QuicPacketNumberSpace space)
    {
        probePending = true;
        if (space == application)
        {
            streams!.QueueFrames(space.FramesToSend);
        }

        if (space.CryptoToSend.Count > 0 || space.FramesToSend.Count > 0)
        {
            return;
        }

        var oldest = space.SentPackets.Values.First(packet => packet.IsAckEliciting);
        var frames = Retransmittable(oldest.Frames);
        space.FramesToSend.InsertRange(0, frames.Count > 0 ? frames : [new QuicPingFrame()]);
    }

    /// <summary>Puts what the lost packets carried that RFC 9000 section 13.3 sends again ahead of everything waiting in the space.</summary>
    private void QueueRetransmission(QuicPacketNumberSpace space, List<QuicSentPacket> lost) =>
        space.FramesToSend.InsertRange(0, Retransmittable(lost.SelectMany(packet => packet.Frames)));

    /// <summary>
    /// What is sent again of lost frames (RFC 9000 section 13.3): <c>CRYPTO</c> and
    /// <c>HANDSHAKE_DONE</c> as they were, stream and flow-control frames as <see cref="QuicStreams.Refresh"/>
    /// says; never <c>ACK</c>, <c>PADDING</c>, <c>PING</c>, <c>PATH_RESPONSE</c> or <c>CONNECTION_CLOSE</c>.
    /// </summary>
    private List<QuicFrame> Retransmittable(IEnumerable<QuicFrame> frames)
    {
        var again = new List<QuicFrame>();
        foreach (var frame in frames)
        {
            var resent = frame switch
            {
                QuicAckFrame or QuicPaddingFrame or QuicPingFrame or QuicPathResponseFrame or QuicConnectionCloseFrame => null,
                QuicCryptoFrame or QuicHandshakeDoneFrame => frame,
                _ => streams!.Refresh(frame),
            };
            if (resent is not null)
            {
                again.Add(resent);
            }
        }

        return again;
    }

    /// <summary>Discards a space's keys and forgets its packets in flight (RFC 9001 section 4.9, RFC 9002 section 6.4).</summary>
    private void DiscardSpace(QuicPacketNumberSpace space)
    {
        recovery.Discard(space);
        space.DiscardKeys();
    }
}
