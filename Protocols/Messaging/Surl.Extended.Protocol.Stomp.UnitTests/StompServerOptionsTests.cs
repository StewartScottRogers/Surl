namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>The server's heart-beat offer (ADR-0118 decision 7).</summary>
[TestClass]
public sealed class StompServerOptionsTests
{
    [TestMethod]
    public void HeartBeatOffer_Default_IsOneSecondAndAQuarterOfTheIdleTimeout()
    {
        Assert.AreEqual(new StompHeartBeat(1000, 30000), StompServerOptions.Default.HeartBeatOffer);
    }

    [TestMethod]
    public void HeartBeatWanted_IdleTimeoutGiven_IsAQuarterOfIt()
    {
        var options = new StompServerOptions { IdleTimeout = TimeSpan.FromSeconds(8) };

        Assert.AreEqual(TimeSpan.FromSeconds(2), options.HeartBeatWanted);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void HeartBeatWanted_IdleTimeoutOff_IsThirtySeconds(int idleTimeoutMilliseconds)
    {
        var options = new StompServerOptions { IdleTimeout = TimeSpan.FromMilliseconds(idleTimeoutMilliseconds) };

        Assert.AreEqual(TimeSpan.FromSeconds(30), options.HeartBeatWanted);
    }
}
