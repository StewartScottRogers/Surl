using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// Loss detection as RFC 9002 section 6 and Appendix A give it, driven by scripted sends and
/// acknowledgments on three packet number spaces; every expected time is that section's formulas
/// worked by hand.
/// </summary>
[TestClass]
public sealed class QuicLossDetectionTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly QuicPacketNumberSpace initial = new(QuicPacketType.Initial, TlsEncryptionLevel.Initial);
    private readonly QuicPacketNumberSpace handshake = new(QuicPacketType.Handshake, TlsEncryptionLevel.Handshake);
    private readonly QuicPacketNumberSpace application = new(QuicPacketType.OneRtt, TlsEncryptionLevel.Application);

    [TestMethod]
    public void OnAckReceived_ThreeLaterPacketsAcknowledged_DeclaresLostByThePacketThreshold()
    {
        var detection = New();
        Send(detection, application, 0, 5, Start);

        var lost = Acknowledge(detection, application, Start.AddMilliseconds(100), new QuicAckRange(3, 4));

        CollectionAssert.AreEqual(new ulong[] { 0, 1 }, lost.Select(packet => packet.PacketNumber).ToArray());
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), detection.Rtt.SmoothedRtt);
        Assert.AreEqual(Start.AddMilliseconds(112.5), application.LossTime, "Packet 2 is lost 9/8 of the RTT after it was sent.");
        Assert.AreEqual(Start.AddMilliseconds(112.5), detection.Timer(false));
        Assert.AreEqual(1200, detection.Congestion.BytesInFlight);
        Assert.AreEqual(0, detection.ProbeCount);
    }

    [TestMethod]
    public void OnTimeout_AtTheLossTime_DeclaresLostByTheTimeThreshold()
    {
        var detection = New();
        Send(detection, application, 0, 5, Start);
        Acknowledge(detection, application, Start.AddMilliseconds(100), new QuicAckRange(3, 4));

        var (space, lost) = detection.OnTimeout(Start.AddMilliseconds(112.5));

        Assert.AreSame(application, space);
        Assert.AreEqual((ulong)2, lost.Single().PacketNumber);
        Assert.IsNull(application.LossTime);
        Assert.AreEqual(0, detection.ProbeCount);
    }

    [TestMethod]
    public void OnAckReceived_NothingNewlyAcknowledged_ChangesNothing()
    {
        var detection = New();
        Send(detection, application, 0, 2, Start);
        Acknowledge(detection, application, Start.AddMilliseconds(100), new QuicAckRange(1, 1));

        var lost = Acknowledge(detection, application, Start.AddMilliseconds(500), new QuicAckRange(1, 1));

        Assert.IsEmpty(lost);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), detection.Rtt.LatestRtt);
    }

    [TestMethod]
    public void OnAckReceived_LargestAlreadyAcknowledged_TakesNoRttSample()
    {
        var detection = New();
        Send(detection, application, 0, 2, Start);
        Acknowledge(detection, application, Start.AddMilliseconds(100), new QuicAckRange(1, 1));

        Acknowledge(detection, application, Start.AddMilliseconds(500), new QuicAckRange(0, 1));

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), detection.Rtt.LatestRtt);
        Assert.IsEmpty(application.SentPackets);
    }

    [TestMethod]
    public void OnAckReceived_OnlyAckOnlyPacketsAcknowledged_TakesNoRttSample()
    {
        var detection = New();
        detection.OnPacketSent(application, new QuicSentPacket(0, Start, false, false, 40, []));

        Acknowledge(detection, application, Start.AddMilliseconds(100), new QuicAckRange(0, 0));

        Assert.IsFalse(detection.Rtt.HasSample);
    }

    [TestMethod]
    public void OnAckReceived_ApplicationAckDelay_IsTakenOffTheSample()
    {
        var detection = New();
        detection.HandshakeConfirmed = true;
        Send(detection, application, 0, 1, Start);
        Acknowledge(detection, application, Start.AddMilliseconds(80), new QuicAckRange(0, 0));
        Send(detection, application, 1, 1, Start.AddMilliseconds(100));

        detection.OnAckReceived(application, Ack(application, new QuicAckRange(1, 1)), TimeSpan.FromMilliseconds(40), Start.AddMilliseconds(260));

        Assert.AreEqual(TimeSpan.FromMilliseconds(86.875), detection.Rtt.SmoothedRtt, "160 ms less the 40 ms delay capped at the default max_ack_delay of 25 ms: 135 ms.");
    }

    [TestMethod]
    public void Timer_AckElicitingPacketInFlight_IsTheProbeTimeoutAndBacksOff()
    {
        var detection = New();
        Send(detection, initial, 0, 1, Start);
        var first = detection.Timer(false);

        var (space, lost) = detection.OnTimeout(first!.Value);

        Assert.AreEqual(Start.AddMilliseconds(999), first);
        Assert.AreSame(initial, space);
        Assert.IsEmpty(lost);
        Assert.AreEqual(1, detection.ProbeCount);
        Assert.AreEqual(Start.AddMilliseconds(1998), detection.Timer(false));
        Assert.IsNull(detection.Timer(true), "At the anti-amplification limit no probe timeout is armed.");
    }

    [TestMethod]
    public void Timer_NothingAckElicitingInFlight_IsNotArmed()
    {
        var detection = New();
        detection.OnPacketSent(handshake, new QuicSentPacket(0, Start, false, false, 40, []));

        Assert.IsNull(detection.Timer(false));
    }

    [TestMethod]
    public void Timer_ApplicationDataBeforeTheHandshakeIsConfirmed_ArmsNoProbeTimeout()
    {
        var detection = New();
        Send(detection, application, 0, 1, Start);
        var unconfirmed = detection.Timer(false);

        detection.HandshakeConfirmed = true;

        Assert.IsNull(unconfirmed);
        Assert.AreEqual(Start.AddMilliseconds(999 + 25), detection.Timer(false), "Application Data adds max_ack_delay.");
    }

    [TestMethod]
    public void Timer_SeveralSpacesInFlight_IsTheEarliestProbeTimeout()
    {
        var detection = New();
        detection.HandshakeConfirmed = true;
        Send(detection, initial, 0, 1, Start.AddMilliseconds(30));
        Send(detection, handshake, 0, 1, Start);
        Send(detection, application, 0, 1, Start.AddMilliseconds(10));

        var (space, _) = detection.OnTimeout(Start.AddSeconds(1));

        Assert.AreSame(handshake, space);
    }

    [TestMethod]
    public void Timer_LossTimesInSeveralSpaces_IsTheEarliest()
    {
        var detection = New();
        Send(detection, initial, 0, 2, Start.AddMilliseconds(20));
        Send(detection, handshake, 0, 2, Start);
        Acknowledge(detection, handshake, Start.AddMilliseconds(105), new QuicAckRange(1, 1));
        Acknowledge(detection, initial, Start.AddMilliseconds(105), new QuicAckRange(1, 1));

        var (space, lost) = detection.OnTimeout(Start.AddSeconds(1));

        Assert.AreSame(handshake, space);
        Assert.AreEqual((ulong)0, lost.Single().PacketNumber);
        Assert.IsNotNull(initial.LossTime);
    }

    [TestMethod]
    public void Discard_ASpaceWithPacketsInFlight_ForgetsThemAndResetsTheProbeCount()
    {
        var detection = New();
        Send(detection, initial, 0, 2, Start);
        detection.OnTimeout(Start.AddSeconds(1));

        detection.Discard(initial);

        Assert.IsEmpty(initial.SentPackets);
        Assert.IsNull(initial.TimeOfLastAckElicitingPacket);
        Assert.AreEqual(0, detection.ProbeCount);
        Assert.AreEqual(0, detection.Congestion.BytesInFlight);
        Assert.IsNull(detection.Timer(false));
    }

    [TestMethod]
    public void OnAckReceived_LossesSpanningThreeProbeTimeouts_CollapseTheWindowToTheMinimum()
    {
        var detection = New();
        Send(detection, application, 0, 1, Start);
        Acknowledge(detection, application, Start.AddMilliseconds(10), new QuicAckRange(0, 0));
        var sendTimes = new[] { 20, 100, 150, 200, 300 };
        for (var index = 0; index < sendTimes.Length; index++)
        {
            Send(detection, application, (ulong)index + 1, 1, Start.AddMilliseconds(sendTimes[index]));
        }

        Send(detection, application, 6, 1, Start.AddMilliseconds(500));
        var lost = Acknowledge(detection, application, Start.AddMilliseconds(510), new QuicAckRange(6, 6));

        Assert.HasCount(5, lost);
        Assert.AreEqual(TimeSpan.FromMilliseconds(150), detection.Rtt.PersistentCongestionDuration(detection.MaxAckDelay), "(10 + 4 * 3.75 + 25) * 3 ms, after the second sample; the losses span 280 ms.");
        Assert.AreEqual(QuicCongestionController.MinimumWindow + 1200, detection.Congestion.CongestionWindow, "Collapsed to the minimum window, 2400 bytes, then grown in slow start by packet 6, acknowledged after the losses (RFC 9002 Appendix A.7).");
    }

    [TestMethod]
    public void IsPersistentCongestion_BeforeAnRttSample_IsFalse()
    {
        var detection = New();

        Assert.IsFalse(detection.IsPersistentCongestion([Packet(1, Start, true), Packet(2, Start.AddHours(1), true)]));
    }

    [TestMethod]
    [DataRow(new ulong[] { 1, 2, 3 }, new[] { 20, 100, 200 }, new[] { true, true, true }, true, DisplayName = "one run spanning 180 ms")]
    [DataRow(new ulong[] { 1, 3 }, new[] { 20, 200 }, new[] { true, true }, false, DisplayName = "a gap: packet 2 was not lost")]
    [DataRow(new ulong[] { 1, 2 }, new[] { 20, 150 }, new[] { true, true }, false, DisplayName = "a run spanning 130 ms only")]
    [DataRow(new ulong[] { 1, 2, 3 }, new[] { 20, 100, 200 }, new[] { false, true, true }, false, DisplayName = "the run's first ack-eliciting packet is at 100 ms")]
    [DataRow(new ulong[] { 1, 2, 3 }, new[] { 20, 100, 200 }, new[] { true, true, false }, false, DisplayName = "the last packet is not ack-eliciting")]
    [DataRow(new ulong[] { 0, 1, 2 }, new[] { 0, 100, 200 }, new[] { true, true, true }, false, DisplayName = "the first packet was sent before the first RTT sample")]
    public void IsPersistentCongestion_ScriptedLosses_NeedsOneRunOfAckElicitingPacketsLongerThanTheDuration(ulong[] packetNumbers, int[] milliseconds, bool[] ackEliciting, bool expected)
    {
        var detection = New();
        Send(detection, application, 0, 1, Start);
        Acknowledge(detection, application, Start.AddMilliseconds(10), new QuicAckRange(0, 0));
        var lost = packetNumbers.Select((packetNumber, index) => Packet(packetNumber, Start.AddMilliseconds(milliseconds[index]), ackEliciting[index])).ToList();

        Assert.AreEqual(expected, detection.IsPersistentCongestion(lost));
    }

    private static QuicSentPacket Packet(ulong packetNumber, DateTimeOffset time, bool ackEliciting) => new(packetNumber, time, ackEliciting, true, 1200, []);

    private static void Send(QuicLossDetection detection, QuicPacketNumberSpace space, ulong first, int count, DateTimeOffset time)
    {
        for (var index = 0; index < count; index++)
        {
            detection.OnPacketSent(space, Packet(first + (ulong)index, time, true));
            space.NextPacketNumber = first + (ulong)index + 1;
        }
    }

    private static QuicAckFrame Ack(QuicPacketNumberSpace space, params QuicAckRange[] ranges)
    {
        space.LargestAcknowledged = Math.Max(space.LargestAcknowledged ?? 0, ranges[0].Largest);
        return new QuicAckFrame(ranges, 0, null);
    }

    private static List<QuicSentPacket> Acknowledge(QuicLossDetection detection, QuicPacketNumberSpace space, DateTimeOffset now, params QuicAckRange[] ranges) =>
        detection.OnAckReceived(space, Ack(space, ranges), TimeSpan.Zero, now);

    private QuicLossDetection New() => new([initial, handshake, application]);
}
