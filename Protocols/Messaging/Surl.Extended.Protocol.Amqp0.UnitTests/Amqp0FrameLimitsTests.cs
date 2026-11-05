namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>The frame-size limits of ADR-0112 decisions 3 and 8.</summary>
[TestClass]
public sealed class Amqp0FrameLimitsTests
{
    [TestMethod]
    [DataRow(0L, 131072)]
    [DataRow(1L, 4096)]
    [DataRow(4096L, 4096)]
    [DataRow(65536L, 65536)]
    [DataRow(1048576L, 131072)]
    public void OfferedFrameMax_IsTheSmallerOf131072AndMaxMessageNeverBelow4096(long maxMessageBytes, int expected)
    {
        Assert.AreEqual(expected, Amqp0FrameLimits.OfferedFrameMax(maxMessageBytes));
    }

    [TestMethod]
    public void OfferedFrameMax_NegativeMaxMessage_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Amqp0FrameLimits.OfferedFrameMax(-1));
    }
}
