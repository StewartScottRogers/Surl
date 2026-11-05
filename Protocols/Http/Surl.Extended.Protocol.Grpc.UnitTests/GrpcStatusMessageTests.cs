namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class GrpcStatusMessageTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("request message did not parse", "request message did not parse")]
    [DataRow(" ~", " ~")]
    [DataRow("%", "%25")]
    [DataRow("a\nb", "a%0Ab")]
    [DataRow("\u007F", "%7F")]
    [DataRow("\u001F", "%1F")]
    [DataRow("é", "%C3%A9")]
    [DataRow("€", "%E2%82%AC")]
    public void Encode_Text_PercentEncodesEveryByteOutsidePrintableAsciiAndPercent(string message, string expected)
    {
        Assert.AreEqual(expected, GrpcStatusMessage.Encode(message));
    }
}
