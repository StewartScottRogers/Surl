namespace Surl.Quic;

/// <summary>Keeps received packet numbers as ACK ranges, largest first (RFC 9000 section 13.2).</summary>
[TestClass]
public sealed class QuicReceivedPacketNumbersTests
{
    [TestMethod]
    public void Add_NumbersOutOfOrder_MergesThemIntoRangesLargestFirst()
    {
        var received = new QuicReceivedPacketNumbers();

        foreach (var packetNumber in new ulong[] { 5, 1, 3, 2, 9, 10, 8, 0 })
        {
            received.Add(packetNumber);
        }

        CollectionAssert.AreEqual(
            new[] { new QuicAckRange(8, 10), new QuicAckRange(5, 5), new QuicAckRange(0, 3) },
            received.Ranges.ToArray());
        Assert.AreEqual((ulong)10, received.Largest);
    }

    [TestMethod]
    public void Add_NumberBetweenTwoRanges_JoinsThem()
    {
        var received = new QuicReceivedPacketNumbers();
        received.Add(1);
        received.Add(3);

        received.Add(2);

        CollectionAssert.AreEqual(new[] { new QuicAckRange(1, 3) }, received.Ranges.ToArray());
    }

    [TestMethod]
    public void Contains_ReceivedAndNotReceived_Answers()
    {
        var received = new QuicReceivedPacketNumbers();
        received.Add(4);

        Assert.IsTrue(received.Contains(4));
        Assert.IsFalse(received.Contains(3));
        Assert.IsFalse(received.Contains(5));
        Assert.IsNull(new QuicReceivedPacketNumbers().Largest);
    }

    [TestMethod]
    public void Add_OneRangeMoreThanKept_ForgetsTheLowestAndCountsBelowItAsReceived()
    {
        var received = new QuicReceivedPacketNumbers();

        for (ulong index = 0; index <= QuicReceivedPacketNumbers.MaxRanges; index++)
        {
            received.Add(index * 2);
        }

        Assert.HasCount(QuicReceivedPacketNumbers.MaxRanges, received.Ranges);
        Assert.AreEqual(new QuicAckRange(2, 2), received.Ranges[^1]);
        Assert.IsTrue(received.Contains(0));
        Assert.IsTrue(received.Contains(1));
        Assert.IsFalse(received.Contains(3));
    }
}
