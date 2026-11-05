namespace Surl.Quic;

/// <summary>
/// NewReno congestion control as RFC 9002 section 7 and Appendix B give it (ADR-0082 decision 4):
/// slow start from an initial window of ten 1200-byte datagrams, congestion avoidance past the
/// slow start threshold, a halved window on loss with one reduction per recovery period, and the
/// minimum window on persistent congestion. Sending is limited by the window; there is no pacing.
/// </summary>
internal sealed class QuicCongestionController
{
    /// <summary>The largest datagram the server sends, <c>max_datagram_size</c>.</summary>
    public const int MaxDatagramSize = QuicServerConnection.MaxDatagramSize;

    /// <summary><c>kInitialWindow</c>: <c>min(10 * max_datagram_size, max(14720, 2 * max_datagram_size))</c>, 12000 bytes (RFC 9002 section 7.2).</summary>
    public const long InitialWindow = 10 * MaxDatagramSize;

    /// <summary><c>kMinimumWindow</c>: two datagrams (RFC 9002 section 7.2).</summary>
    public const long MinimumWindow = 2 * MaxDatagramSize;

    private DateTimeOffset? recoveryStartTime;

    /// <summary>The congestion window in bytes.</summary>
    public long CongestionWindow { get; private set; } = InitialWindow;

    /// <summary>The slow start threshold in bytes; unbounded until the first loss.</summary>
    public long SlowStartThreshold { get; private set; } = long.MaxValue;

    /// <summary>The bytes of the packets in flight: sent, counted in flight, and neither acknowledged, lost nor discarded.</summary>
    public long BytesInFlight { get; private set; }

    /// <summary>How many more bytes the window lets the server put in flight now.</summary>
    public long AvailableWindow => Math.Max(0, CongestionWindow - BytesInFlight);

    /// <summary>Whether a packet sent at <paramref name="timeSent"/> was sent before the current recovery period began.</summary>
    public bool InRecovery(DateTimeOffset timeSent) => recoveryStartTime is { } start && timeSent <= start;

    /// <summary>Counts a packet in flight once it is sent.</summary>
    public void OnPacketSent(QuicSentPacket packet)
    {
        if (packet.IsInFlight)
        {
            BytesInFlight += packet.SentBytes;
        }
    }

    /// <summary>Takes packets out of flight and grows the window for each sent outside recovery: by its size in slow start, by a datagram per window in congestion avoidance (RFC 9002 section 7.3).</summary>
    public void OnPacketsAcknowledged(IEnumerable<QuicSentPacket> packets)
    {
        foreach (var packet in packets.Where(packet => packet.IsInFlight))
        {
            BytesInFlight -= packet.SentBytes;
            if (!InRecovery(packet.TimeSent))
            {
                CongestionWindow += CongestionWindow < SlowStartThreshold
                    ? packet.SentBytes
                    : MaxDatagramSize * packet.SentBytes / CongestionWindow;
            }
        }
    }

    /// <summary>
    /// Takes lost packets out of flight; a loss of a packet sent after the current recovery period
    /// began starts a new one, halving the window (RFC 9002 section 7.3.2), and persistent
    /// congestion collapses it to <see cref="MinimumWindow"/> (section 7.6.2).
    /// </summary>
    /// <param name="packets">The packets declared lost.</param>
    /// <param name="now">The time of the loss.</param>
    /// <param name="persistentCongestion">Whether the losses establish persistent congestion.</param>
    public void OnPacketsLost(IReadOnlyCollection<QuicSentPacket> packets, DateTimeOffset now, bool persistentCongestion)
    {
        var inFlight = packets.Where(packet => packet.IsInFlight).ToList();
        if (inFlight.Count > 0)
        {
            BytesInFlight -= inFlight.Sum(packet => (long)packet.SentBytes);
            OnCongestionEvent(inFlight.Max(packet => packet.TimeSent), now);
        }

        if (persistentCongestion)
        {
            CongestionWindow = MinimumWindow;
            recoveryStartTime = null;
        }
    }

    /// <summary>Takes the packets of a discarded packet number space out of flight without counting them lost (RFC 9002 section 6.4).</summary>
    public void Discard(IEnumerable<QuicSentPacket> packets) =>
        BytesInFlight -= packets.Where(packet => packet.IsInFlight).Sum(packet => (long)packet.SentBytes);

    private void OnCongestionEvent(DateTimeOffset lastLostSent, DateTimeOffset now)
    {
        if (InRecovery(lastLostSent))
        {
            return;
        }

        recoveryStartTime = now;
        SlowStartThreshold = CongestionWindow / 2;
        CongestionWindow = Math.Max(SlowStartThreshold, MinimumWindow);
    }
}
