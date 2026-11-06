namespace Surl.Dtls;

[TestClass]
public sealed class DtlsReplayWindowTests
{
    [TestMethod]
    public void IsNew_NothingAccepted_IsTrueForZero()
    {
        var window = new DtlsReplayWindow();

        Assert.IsTrue(window.IsNew(0));
    }

    [TestMethod]
    public void IsNew_AcceptedSequenceNumber_IsFalse()
    {
        var window = new DtlsReplayWindow();
        window.MarkAccepted(7);

        Assert.IsFalse(window.IsNew(7));
    }

    [TestMethod]
    public void IsNew_UnacceptedSequenceNumberInsideTheWindow_IsTrue()
    {
        var window = new DtlsReplayWindow();
        window.MarkAccepted(100);

        Assert.IsTrue(window.IsNew(100 - 63));
    }

    [TestMethod]
    public void IsNew_SequenceNumberOlderThanTheWindow_IsFalse()
    {
        var window = new DtlsReplayWindow();
        window.MarkAccepted(100);

        Assert.IsFalse(window.IsNew(100 - 64));
    }

    [TestMethod]
    public void MarkAccepted_OlderSequenceNumberInsideTheWindow_MakesItAReplay()
    {
        var window = new DtlsReplayWindow();
        window.MarkAccepted(100);

        window.MarkAccepted(90);

        Assert.IsFalse(window.IsNew(90));
        Assert.IsTrue(window.IsNew(91));
    }

    [TestMethod]
    public void MarkAccepted_SlidingBySome_KeepsTheEarlierAcceptancesThatStillFit()
    {
        var window = new DtlsReplayWindow();
        window.MarkAccepted(10);

        window.MarkAccepted(20);

        Assert.IsFalse(window.IsNew(10));
        Assert.IsTrue(window.IsNew(15));
    }

    [TestMethod]
    public void MarkAccepted_SlidingPastTheWholeWindow_ForgetsEveryEarlierAcceptance()
    {
        var window = new DtlsReplayWindow();
        window.MarkAccepted(10);

        window.MarkAccepted(10 + 64);

        Assert.IsFalse(window.IsNew(10));
        Assert.IsTrue(window.IsNew(11));
        Assert.IsFalse(window.IsNew(10 + 64));
    }
}
