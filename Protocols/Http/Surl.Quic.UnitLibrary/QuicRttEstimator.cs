namespace Surl.Quic;

/// <summary>
/// The round-trip time estimates of one connection (RFC 9002 section 5): the latest sample, the
/// minimum, the smoothed RTT and its variation, and the durations loss detection derives from
/// them. Arithmetic is in <see cref="TimeSpan"/> ticks, so a scripted series of samples gives
/// exactly the values RFC 9002 section 5.3's formulas give.
/// </summary>
internal sealed class QuicRttEstimator
{
    /// <summary>The RTT assumed before the first sample (RFC 9002 section 6.2.2).</summary>
    public static readonly TimeSpan InitialRtt = TimeSpan.FromMilliseconds(333);

    /// <summary>The timer granularity, <c>kGranularity</c> (RFC 9002 section 6.1.2).</summary>
    public static readonly TimeSpan Granularity = TimeSpan.FromMilliseconds(1);

    /// <summary>How many probe timeouts long a run of losses must be to be persistent congestion (RFC 9002 section 7.6.1).</summary>
    public const int PersistentCongestionThreshold = 3;

    /// <summary>The most recent RTT sample; <see cref="InitialRtt"/> before the first.</summary>
    public TimeSpan LatestRtt { get; private set; } = InitialRtt;

    /// <summary>The smallest RTT sample seen; zero before the first.</summary>
    public TimeSpan MinRtt { get; private set; }

    /// <summary>The smoothed RTT, <c>smoothed_rtt</c>.</summary>
    public TimeSpan SmoothedRtt { get; private set; } = InitialRtt;

    /// <summary>The RTT variation, <c>rttvar</c>.</summary>
    public TimeSpan RttVariation { get; private set; } = InitialRtt / 2;

    /// <summary>Whether a sample has been taken.</summary>
    public bool HasSample { get; private set; }

    /// <summary>
    /// How long after a packet was sent it is lost when a later one is acknowledged:
    /// 9/8 of the larger of the latest and the smoothed RTT, and at least <see cref="Granularity"/>
    /// (RFC 9002 section 6.1.2).
    /// </summary>
    public TimeSpan LossDelay
    {
        get
        {
            var larger = LatestRtt > SmoothedRtt ? LatestRtt : SmoothedRtt;
            var delay = TimeSpan.FromTicks(larger.Ticks * 9 / 8);
            return delay > Granularity ? delay : Granularity;
        }
    }

    /// <summary>The probe timeout before backoff and without the peer's <c>max_ack_delay</c>: <c>smoothed_rtt + max(4 * rttvar, kGranularity)</c> (RFC 9002 section 6.2.1).</summary>
    public TimeSpan ProbeTimeout
    {
        get
        {
            var variation = RttVariation * 4;
            return SmoothedRtt + (variation > Granularity ? variation : Granularity);
        }
    }

    /// <summary>How long a run of lost packets must span to be persistent congestion (RFC 9002 section 7.6.1).</summary>
    /// <param name="maxAckDelay">The peer's <c>max_ack_delay</c>.</param>
    /// <returns>Three probe timeouts with the peer's acknowledgment delay.</returns>
    public TimeSpan PersistentCongestionDuration(TimeSpan maxAckDelay) => (ProbeTimeout + maxAckDelay) * PersistentCongestionThreshold;

    /// <summary>Takes one RTT sample (RFC 9002 section 5.3).</summary>
    /// <param name="latestRtt">The time from sending the largest packet newly acknowledged to receiving its acknowledgment.</param>
    /// <param name="ackDelay">The acknowledgment delay the peer reported; zero for Initial and Handshake packets.</param>
    /// <param name="maxAckDelay">The peer's <c>max_ack_delay</c>, which caps <paramref name="ackDelay"/> once the handshake is confirmed.</param>
    /// <param name="handshakeConfirmed">Whether the handshake is confirmed.</param>
    public void Update(TimeSpan latestRtt, TimeSpan ackDelay, TimeSpan maxAckDelay, bool handshakeConfirmed)
    {
        LatestRtt = latestRtt;
        if (!HasSample)
        {
            HasSample = true;
            MinRtt = latestRtt;
            SmoothedRtt = latestRtt;
            RttVariation = latestRtt / 2;
            return;
        }

        MinRtt = latestRtt < MinRtt ? latestRtt : MinRtt;
        var delay = handshakeConfirmed && ackDelay > maxAckDelay ? maxAckDelay : ackDelay;
        var adjusted = latestRtt >= MinRtt + delay ? latestRtt - delay : latestRtt;
        var variationSample = (SmoothedRtt - adjusted).Duration();
        RttVariation = TimeSpan.FromTicks(((3 * RttVariation.Ticks) + variationSample.Ticks) / 4);
        SmoothedRtt = TimeSpan.FromTicks(((7 * SmoothedRtt.Ticks) + adjusted.Ticks) / 8);
    }
}
