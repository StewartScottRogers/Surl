namespace Surl.Quic;

/// <summary>NewReno as RFC 9002 section 7 and Appendix B give it, driven by scripted sends, acknowledgments and losses.</summary>
[TestClass]
public sealed class QuicCongestionControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void New_BeforeAnything_HasRfc9002InitialWindow()
    {
        var controller = new QuicCongestionController();

        Assert.AreEqual(12000, controller.CongestionWindow);
        Assert.AreEqual(long.MaxValue, controller.SlowStartThreshold);
        Assert.AreEqual(0, controller.BytesInFlight);
        Assert.AreEqual(12000, controller.AvailableWindow);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_InSlowStart_GrowsTheWindowByTheBytesAcknowledged()
    {
        var controller = new QuicCongestionController();
        var packets = Send(controller, 10, Start);
        var full = controller.AvailableWindow;

        controller.OnPacketsAcknowledged(packets.Take(5));

        Assert.AreEqual(0, full);
        Assert.AreEqual(18000, controller.CongestionWindow);
        Assert.AreEqual(6000, controller.BytesInFlight);
        Assert.AreEqual(12000, controller.AvailableWindow);
    }

    [TestMethod]
    public void OnPacketsLost_FirstLoss_HalvesTheWindowAndStartsRecovery()
    {
        var controller = new QuicCongestionController();
        var before = Send(controller, 4, Start);
        var lossTime = Start.AddMilliseconds(100);

        controller.OnPacketsLost([before[0]], lossTime, false);
        controller.OnPacketsAcknowledged([before[1]]);
        var afterAckFromBeforeRecovery = controller.CongestionWindow;
        controller.OnPacketsLost([before[2]], lossTime.AddMilliseconds(5), false);
        var afterSecondLossInRecovery = controller.CongestionWindow;
        var after = Send(controller, 1, lossTime.AddMilliseconds(10));
        controller.OnPacketsAcknowledged(after);

        Assert.AreEqual(6000, controller.SlowStartThreshold);
        Assert.AreEqual(6000, afterAckFromBeforeRecovery, "A packet sent before recovery began does not grow the window.");
        Assert.AreEqual(6000, afterSecondLossInRecovery, "One reduction per recovery period.");
        Assert.AreEqual(6000 + (1200 * 1200 / 6000), controller.CongestionWindow, "Congestion avoidance: one datagram per window.");
        Assert.AreEqual(1200, controller.BytesInFlight);
        Assert.IsTrue(controller.InRecovery(Start));
        Assert.IsFalse(controller.InRecovery(lossTime.AddMilliseconds(10)));
    }

    [TestMethod]
    public void OnPacketsLost_RepeatedLosses_NeverGoBelowTheMinimumWindow()
    {
        var controller = new QuicCongestionController();
        var windows = new List<long>();

        for (var round = 0; round < 4; round++)
        {
            var sent = Start.AddSeconds(round);
            controller.OnPacketsLost(Send(controller, 1, sent), sent.AddMilliseconds(1), false);
            windows.Add(controller.CongestionWindow);
        }

        CollectionAssert.AreEqual(new long[] { 6000, 3000, 2400, 2400 }, windows);
    }

    [TestMethod]
    public void OnPacketsLost_PersistentCongestion_CollapsesToTheMinimumWindowAndEndsRecovery()
    {
        var controller = new QuicCongestionController();
        var sent = Send(controller, 3, Start);
        controller.OnPacketsAcknowledged([sent[0]]);

        controller.OnPacketsLost([sent[1]], Start.AddSeconds(1), true);
        var collapsed = controller.CongestionWindow;
        controller.OnPacketsAcknowledged([sent[2]]);

        Assert.AreEqual(QuicCongestionController.MinimumWindow, collapsed);
        Assert.AreEqual(3600, controller.CongestionWindow, "With recovery ended, even a packet sent before it grows the window, in slow start below the threshold of 6000.");
        Assert.AreEqual(0, controller.BytesInFlight);
    }

    [TestMethod]
    public void PacketsNotInFlight_AreNotCounted()
    {
        var controller = new QuicCongestionController();
        var ackOnly = new QuicSentPacket(0, Start, false, false, 50, []);

        controller.OnPacketSent(ackOnly);
        var inFlight = controller.BytesInFlight;
        controller.OnPacketsLost([ackOnly], Start.AddSeconds(1), false);
        controller.OnPacketsAcknowledged([ackOnly]);

        Assert.AreEqual(0, inFlight);
        Assert.AreEqual(QuicCongestionController.InitialWindow, controller.CongestionWindow);
        Assert.IsFalse(controller.InRecovery(Start));
    }

    [TestMethod]
    public void Discard_PacketsOfADiscardedSpace_LeaveFlightWithoutALoss()
    {
        var controller = new QuicCongestionController();
        var sent = Send(controller, 3, Start);

        controller.Discard([.. sent, new QuicSentPacket(9, Start, false, false, 50, [])]);

        Assert.AreEqual(0, controller.BytesInFlight);
        Assert.AreEqual(QuicCongestionController.InitialWindow, controller.CongestionWindow);
    }

    private static List<QuicSentPacket> Send(QuicCongestionController controller, int count, DateTimeOffset time)
    {
        var packets = Enumerable.Range(0, count).Select(index => new QuicSentPacket((ulong)index, time, true, true, 1200, [])).ToList();
        packets.ForEach(controller.OnPacketSent);
        return packets;
    }
}
