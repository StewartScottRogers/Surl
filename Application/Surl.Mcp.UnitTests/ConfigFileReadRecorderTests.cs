namespace Surl.Mcp;

[TestClass]
public sealed class ConfigFileReadRecorderTests
{
    [TestMethod]
    public void WasReadRequested_BeforeAnyRead_IsFalse() =>
        Assert.IsFalse(new ConfigFileReadRecorder().WasReadRequested);

    [TestMethod]
    public void Read_AnyFile_ReturnsNoBytesAndRecordsTheRequest()
    {
        var recorder = new ConfigFileReadRecorder();

        var content = recorder.Read("surl.conf", 1);

        Assert.IsNull(content);
        Assert.IsTrue(recorder.WasReadRequested);
    }

    [TestMethod]
    public void IdentityOf_APath_IsThePathAsGiven() =>
        Assert.AreEqual("surl.conf", new ConfigFileReadRecorder().IdentityOf("surl.conf"));
}
