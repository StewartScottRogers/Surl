namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>The <c>heart-beat</c> header (the specification's "Heart-beating"; ADR-0118 decision 7).</summary>
[TestClass]
public sealed class StompHeartBeatTests
{
    [TestMethod]
    public void TryParse_TwoIntervals_AreRead()
    {
        var parsed = StompHeartBeat.TryParse("4000,0", out var heartBeat);

        Assert.IsTrue(parsed);
        Assert.AreEqual(new StompHeartBeat(4000, 0), heartBeat);
    }

    [TestMethod]
    public void TryParse_Absent_IsZeroZero()
    {
        var parsed = StompHeartBeat.TryParse(null, out var heartBeat);

        Assert.IsTrue(parsed);
        Assert.AreEqual(default, heartBeat);
    }

    [TestMethod]
    [DataRow("1000")]
    [DataRow("1,2,3")]
    [DataRow("-1,0")]
    [DataRow("0,x")]
    [DataRow(" 1,0")]
    [DataRow("99999999999999999999,0")]
    public void TryParse_NotTwoNonNegativeIntegers_IsRefused(string value)
    {
        var parsed = StompHeartBeat.TryParse(value, out var heartBeat);

        Assert.IsFalse(parsed);
        Assert.AreEqual(default, heartBeat);
    }

    [TestMethod]
    public void ToString_IsTheHeaderValue()
    {
        Assert.AreEqual("1000,30000", new StompHeartBeat(1000, 30000).ToString());
    }
}
