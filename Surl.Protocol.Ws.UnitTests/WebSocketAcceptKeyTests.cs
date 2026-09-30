namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WebSocketAcceptKeyTests
{
    [TestMethod]
    public void Compute_Rfc6455Section1Point3Key_GivesTheRfcsAcceptValue()
    {
        Assert.AreEqual("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", WebSocketAcceptKey.Compute("dGhlIHNhbXBsZSBub25jZQ=="));
    }
}
