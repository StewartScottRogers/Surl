namespace Surl.Quic;

/// <summary>RTT estimation as RFC 9002 section 5.3 gives it; every expected value is that section's formulas worked by hand.</summary>
[TestClass]
public sealed class QuicRttEstimatorTests
{
    private static readonly TimeSpan MaxAckDelay = TimeSpan.FromMilliseconds(24);

    [TestMethod]
    public void New_BeforeAnySample_AssumesRfc9002InitialRtt()
    {
        var rtt = new QuicRttEstimator();

        Assert.IsFalse(rtt.HasSample);
        Assert.AreEqual(TimeSpan.FromMilliseconds(333), rtt.SmoothedRtt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(166.5), rtt.RttVariation);
        Assert.AreEqual(TimeSpan.FromMilliseconds(333), rtt.LatestRtt);
        Assert.AreEqual(TimeSpan.Zero, rtt.MinRtt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(999), rtt.ProbeTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(374.625), rtt.LossDelay);
        Assert.AreEqual(TimeSpan.FromMilliseconds(3072), rtt.PersistentCongestionDuration(TimeSpan.FromMilliseconds(25)));
    }

    [TestMethod]
    public void Update_ScriptedSeriesOfAcks_FollowsRfc9002Section53()
    {
        var rtt = new QuicRttEstimator();

        rtt.Update(TimeSpan.FromMilliseconds(80), TimeSpan.Zero, MaxAckDelay, true);
        var first = (rtt.SmoothedRtt, rtt.RttVariation, rtt.MinRtt);
        rtt.Update(TimeSpan.FromMilliseconds(96), TimeSpan.FromMilliseconds(8), MaxAckDelay, true);
        var second = (rtt.SmoothedRtt, rtt.RttVariation, rtt.MinRtt);
        rtt.Update(TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(40), MaxAckDelay, true);

        Assert.AreEqual((TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(80)), first);
        Assert.AreEqual((TimeSpan.FromMilliseconds(81), TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(80)), second);
        Assert.AreEqual(TimeSpan.FromMilliseconds(87.875), rtt.SmoothedRtt, "The ack delay of 40 ms is capped at max_ack_delay, 24 ms: adjusted_rtt is 136 ms.");
        Assert.AreEqual(TimeSpan.FromMilliseconds(37.75), rtt.RttVariation);
        Assert.AreEqual(TimeSpan.FromMilliseconds(160), rtt.LatestRtt);
        Assert.IsTrue(rtt.HasSample);
    }

    [TestMethod]
    public void Update_SampleBelowMinRttPlusAckDelay_IsNotAdjusted()
    {
        var rtt = new QuicRttEstimator();
        rtt.Update(TimeSpan.FromMilliseconds(80), TimeSpan.Zero, MaxAckDelay, true);

        rtt.Update(TimeSpan.FromMilliseconds(70), TimeSpan.FromMilliseconds(20), MaxAckDelay, true);

        Assert.AreEqual(TimeSpan.FromMilliseconds(70), rtt.MinRtt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(78.75), rtt.SmoothedRtt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(32.5), rtt.RttVariation);
    }

    [TestMethod]
    public void Update_BeforeTheHandshakeIsConfirmed_DoesNotCapTheAckDelay()
    {
        var rtt = new QuicRttEstimator();
        rtt.Update(TimeSpan.FromMilliseconds(80), TimeSpan.Zero, MaxAckDelay, false);

        rtt.Update(TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(40), MaxAckDelay, false);

        Assert.AreEqual(TimeSpan.FromMilliseconds(85), rtt.SmoothedRtt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(40), rtt.RttVariation);
        Assert.AreEqual(TimeSpan.FromMilliseconds(180), rtt.LossDelay, "9/8 of the latest RTT, 160 ms, which is above the smoothed RTT.");
    }

    [TestMethod]
    public void LossDelayAndProbeTimeout_ZeroRtt_AreAtLeastTheGranularity()
    {
        var rtt = new QuicRttEstimator();

        rtt.Update(TimeSpan.Zero, TimeSpan.Zero, MaxAckDelay, true);

        Assert.AreEqual(QuicRttEstimator.Granularity, rtt.LossDelay);
        Assert.AreEqual(QuicRttEstimator.Granularity, rtt.ProbeTimeout);
    }
}
