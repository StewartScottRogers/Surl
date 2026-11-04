namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WebSocketCloseCodesTests
{
    [TestMethod]
    [DataRow(1000)]
    [DataRow(1003)]
    [DataRow(1007)]
    [DataRow(1011)]
    [DataRow(1014)]
    [DataRow(3000)]
    [DataRow(4999)]
    public void IsAllowedOnTheWire_AllowedCode_IsTrue(int code)
    {
        Assert.IsTrue(WebSocketCloseCodes.IsAllowedOnTheWire(code));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(999)]
    [DataRow(1004)]
    [DataRow(1005)]
    [DataRow(1006)]
    [DataRow(1015)]
    [DataRow(2999)]
    [DataRow(5000)]
    public void IsAllowedOnTheWire_ReservedOrUnassignedCode_IsFalse(int code)
    {
        Assert.IsFalse(WebSocketCloseCodes.IsAllowedOnTheWire(code));
    }
}
