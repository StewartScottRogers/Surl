namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class FaultRuleCloseTests
{
    [TestMethod]
    public void Close_NotGiven_IsReset()
    {
        var rule = new FaultRule("f1", FaultKind.Drop);

        Assert.AreEqual(FaultClose.Reset, rule.Close);
        Assert.AreEqual(0, (int)FaultClose.Reset);
    }

    [TestMethod]
    public void Close_Graceful_RoundTrips()
    {
        var rule = new FaultRule("f1", FaultKind.Drop) { Close = FaultClose.Graceful };

        Assert.AreEqual(FaultClose.Graceful, rule.Close);
    }
}
