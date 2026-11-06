namespace Surl.HttpMessage;

[TestClass]
public sealed class HttpMessageProtocolTests
{
    [TestMethod]
    public void Http11_NamesHttpUpToMinorVersionOne()
    {
        var protocol = HttpMessageProtocol.Http11;

        Assert.AreEqual("HTTP", protocol.Name);
        Assert.AreEqual(1, protocol.HighestMinorVersion);
        Assert.AreEqual("HTTP/1.1", protocol.HighestVersionText);
        CollectionAssert.AreEqual("HTTP/"u8.ToArray(), protocol.VersionPrefix);
    }

    [TestMethod]
    public void Rtsp10_NamesRtspUpToMinorVersionZero()
    {
        var protocol = HttpMessageProtocol.Rtsp10;

        Assert.AreEqual("RTSP", protocol.Name);
        Assert.AreEqual(0, protocol.HighestMinorVersion);
        Assert.AreEqual("RTSP/1.0", protocol.HighestVersionText);
        CollectionAssert.AreEqual("RTSP/"u8.ToArray(), protocol.VersionPrefix);
    }
}
