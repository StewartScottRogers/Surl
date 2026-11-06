namespace Surl.Quic;

/// <summary>
/// Loss detection for a server connection's three packet number spaces, as RFC 9002 section 6
/// and Appendix A give it: acknowledged packets leave the record and give RTT samples, a packet is
/// lost once one sent three packet numbers later is acknowledged or once it is older than the loss
/// delay, and the probe timeout fires when acknowledgments stop. It feeds
/// <see cref="QuicCongestionController"/>, which decides persistent congestion with the RTT
/// estimates here. A server always holds the client's address as validating its own (section
/// 6.2.2.1), so no timer is armed with nothing ack-eliciting in flight.
/// </summary>
/// <param name="spaces">The Initial, Handshake and Application Data spaces, in that order.</param>
internal sealed class QuicLossDetection(IReadOnlyList<QuicPacketNumberSpace> spaces)
{
    /// <summary><c>kPacketThreshold</c>: a packet is lost once one three packet numbers later is acknowledged (RFC 9002 section 6.1.1).</summary>
    public const int PacketThreshold = 3;

    /// <summary>The <c>max_ack_delay</c> assumed until the client's transport parameters say otherwise (RFC 9000 section 18.2).</summary>
    public static readonly TimeSpan DefaultMaxAckDelay = TimeSpan.FromMilliseconds(25);

    /// <summary>The most doublings of the probe timeout; the idle timeout ends a connection long before.</summary>
    private const int MaxBackoffExponent = 16;

    private DateTimeOffset? firstRttSampleTime;

    /// <summary>The RTT estimates.</summary>
    public QuicRttEstimator Rtt { get; } = new();

    /// <summary>The congestion controller the losses and acknowledgments feed.</summary>
    public QuicCongestionController Congestion { get; } = new();

    /// <summary>How many probe timeouts have fired since the last acknowledgment, <c>pto_count</c>.</summary>
    public int ProbeCount { get; private set; }

    /// <summary>The client's <c>max_ack_delay</c>.</summary>
    public TimeSpan MaxAckDelay { get; set; } = DefaultMaxAckDelay;

    /// <summary>Whether the handshake is confirmed; until it is, the Application Data space arms no probe timeout.</summary>
    public bool HandshakeConfirmed { get; set; }

    /// <summary>Records a packet sent in <paramref name="space"/>.</summary>
    public void OnPacketSent(QuicPacketNumberSpace space, QuicSentPacket packet)
    {
        space.SentPackets.Add(packet.PacketNumber, packet);
        space.TimeOfLastAckElicitingPacket = packet.IsAckEliciting ? packet.TimeSent : space.TimeOfLastAckElicitingPacket;
        Congestion.OnPacketSent(packet);
    }

    /// <summary>
    /// Acts on an <c>ACK</c> frame whose largest packet number <paramref name="space"/> already holds
    /// as <see cref="QuicPacketNumberSpace.LargestAcknowledged"/> (RFC 9002 Appendix A.7).
    /// </summary>
    /// <param name="space">The space the frame acknowledges.</param>
    /// <param name="ack">The frame.</param>
    /// <param name="ackDelay">The acknowledgment delay it reports, decoded; zero outside Application Data.</param>
    /// <param name="now">When it arrived.</param>
    /// <returns>The packets it showed to be lost, in packet number order.</returns>
    public List<QuicSentPacket> OnAckReceived(QuicPacketNumberSpace space, QuicAckFrame ack, TimeSpan ackDelay, DateTimeOffset now)
    {
        var acknowledged = RemoveAcknowledged(space, ack);
        if (acknowledged.Count == 0)
        {
            return [];
        }

        if (acknowledged[^1].PacketNumber == ack.LargestAcknowledged && acknowledged.Exists(packet => packet.IsAckEliciting))
        {
            Rtt.Update(now - acknowledged[^1].TimeSent, ackDelay, MaxAckDelay, HandshakeConfirmed);
            firstRttSampleTime ??= now;
        }

        var lost = DetectLost(space, now);
        Congestion.OnPacketsAcknowledged(acknowledged);
        ProbeCount = 0;
        return lost;
    }

    /// <summary>When the loss detection timer fires: the earliest loss time, or the probe timeout; null when neither is armed or the server may send nothing (RFC 9002 Appendix A.8).</summary>
    /// <param name="atAmplificationLimit">Whether the anti-amplification limit stops the server sending.</param>
    /// <returns>The time, or null.</returns>
    public DateTimeOffset? Timer(bool atAmplificationLimit) =>
        EarliestLossTime()?.Time ?? (atAmplificationLimit ? null : ProbeTimeout()?.Time);

    /// <summary>
    /// Acts on the loss detection timer (RFC 9002 Appendix A.9): with a loss time due, declares
    /// what it shows to be lost; otherwise the probe timeout fired, and the caller sends a probe in
    /// the space returned.
    /// </summary>
    /// <param name="now">The time.</param>
    /// <returns>The space acted on and the packets declared lost, none for a probe timeout.</returns>
    public (QuicPacketNumberSpace Space, List<QuicSentPacket> Lost) OnTimeout(DateTimeOffset now)
    {
        if (EarliestLossTime() is { } loss)
        {
            return (loss.Space, DetectLost(loss.Space, now));
        }

        var probe = ProbeTimeout()!.Value;
        ProbeCount++;
        return (probe.Space, []);
    }

    /// <summary>Forgets a space whose keys are discarded: its packets leave flight without being lost (RFC 9002 section 6.4).</summary>
    public void Discard(QuicPacketNumberSpace space)
    {
        Congestion.Discard(space.SentPackets.Values);
        space.SentPackets.Clear();
        space.LossTime = null;
        space.TimeOfLastAckElicitingPacket = null;
        ProbeCount = 0;
    }

    /// <summary>
    /// Whether the lost packets establish persistent congestion (RFC 9002 section 7.6): within a run
    /// of consecutive packet numbers all lost and sent after the first RTT sample, two ack-eliciting
    /// packets sent further apart than <see cref="QuicRttEstimator.PersistentCongestionDuration"/>.
    /// Runs are looked for in one space, the one the losses came from.
    /// </summary>
    public bool IsPersistentCongestion(IReadOnlyList<QuicSentPacket> lost)
    {
        if (firstRttSampleTime is not { } firstSample)
        {
            return false;
        }

        var duration = Rtt.PersistentCongestionDuration(MaxAckDelay);
        DateTimeOffset? runStart = null;
        ulong? previous = null;
        foreach (var packet in lost.Where(packet => packet.TimeSent > firstSample))
        {
            runStart = previous + 1 == packet.PacketNumber ? runStart : null;
            previous = packet.PacketNumber;
            if (!packet.IsAckEliciting)
            {
                continue;
            }

            runStart ??= packet.TimeSent;
            if (packet.TimeSent - runStart.Value > duration)
            {
                return true;
            }
        }

        return false;
    }

    private static List<QuicSentPacket> RemoveAcknowledged(QuicPacketNumberSpace space, QuicAckFrame ack)
    {
        var acknowledged = space.SentPackets.Values
            .Where(packet => ack.Ranges.Any(range => range.Smallest <= packet.PacketNumber && packet.PacketNumber <= range.Largest))
            .ToList();
        acknowledged.ForEach(packet => space.SentPackets.Remove(packet.PacketNumber));
        return acknowledged;
    }

    /// <summary>Declares lost the packets at or below the largest acknowledged that the packet or time threshold condemns, and sets the space's loss time for the rest (RFC 9002 Appendix A.10).</summary>
    private List<QuicSentPacket> DetectLost(QuicPacketNumberSpace space, DateTimeOffset now)
    {
        space.LossTime = null;
        var delay = Rtt.LossDelay;
        var largest = space.LargestAcknowledged!.Value;
        var lost = new List<QuicSentPacket>();
        foreach (var packet in space.SentPackets.Values.TakeWhile(packet => packet.PacketNumber <= largest))
        {
            if (packet.TimeSent <= now - delay || largest >= packet.PacketNumber + PacketThreshold)
            {
                lost.Add(packet);
            }
            else
            {
                space.LossTime ??= packet.TimeSent + delay;
            }
        }

        lost.ForEach(packet => space.SentPackets.Remove(packet.PacketNumber));
        Congestion.OnPacketsLost(lost, now, IsPersistentCongestion(lost));
        return lost;
    }

    private (DateTimeOffset Time, QuicPacketNumberSpace Space)? EarliestLossTime()
    {
        (DateTimeOffset Time, QuicPacketNumberSpace Space)? earliest = null;
        foreach (var space in spaces)
        {
            if (space.LossTime is { } time && (earliest is null || time < earliest.Value.Time))
            {
                earliest = (time, space);
            }
        }

        return earliest;
    }

    /// <summary>The probe timeout and its space (RFC 9002 Appendix A.8, <c>GetPtoTimeAndSpace</c>), or null with nothing ack-eliciting in flight.</summary>
    private (DateTimeOffset Time, QuicPacketNumberSpace Space)? ProbeTimeout()
    {
        var backoff = 1L << Math.Min(ProbeCount, MaxBackoffExponent);
        (DateTimeOffset Time, QuicPacketNumberSpace Space)? earliest = null;
        foreach (var space in spaces.Where(space => space.HasAckElicitingInFlight))
        {
            var isApplication = space.PacketType == QuicPacketType.OneRtt;
            if (isApplication && !HandshakeConfirmed)
            {
                break;
            }

            var duration = (Rtt.ProbeTimeout + (isApplication ? MaxAckDelay : TimeSpan.Zero)) * backoff;
            var time = space.TimeOfLastAckElicitingPacket!.Value + duration;
            earliest = earliest is null || time < earliest.Value.Time ? (time, space) : earliest;
        }

        return earliest;
    }
}
