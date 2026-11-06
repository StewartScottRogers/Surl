namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The <c>max-frame-size</c> the container offers for each <c>--max-message</c> (ADR-0107 decisions 3 and 7).
/// </summary>
[TestClass]
public sealed class Amqp1FrameLimitsTests
{
    [TestMethod]
    [DataRow(0L, 65536u, DisplayName = "no limit")]
    [DataRow(100L, 512u, DisplayName = "below MIN-MAX-FRAME-SIZE")]
    [DataRow(1000L, 1000u, DisplayName = "between")]
    [DataRow(1048576L, 65536u, DisplayName = "the 1 MiB default")]
    public void OfferedMaxFrameSize_MaxMessage_IsClampedTo512Through65536(long maxMessageBytes, uint expected)
    {
        Assert.AreEqual(expected, Amqp1FrameLimits.OfferedMaxFrameSize(maxMessageBytes));
    }

    [TestMethod]
    public void OfferedMaxFrameSize_NegativeMaxMessage_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Amqp1FrameLimits.OfferedMaxFrameSize(-1));
    }
}
