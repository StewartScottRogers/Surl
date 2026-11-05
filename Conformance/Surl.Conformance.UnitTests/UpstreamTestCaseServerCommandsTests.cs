using Surl.Protocol.Http;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseServerCommandsTests
{
    [TestMethod]
    [DataRow("http", "auth_required", true)]
    [DataRow("http-ipv6", "idle", true)]
    [DataRow("https", "skip:", true)]
    [DataRow("https-mtls", "writedelay:", true)]
    [DataRow("http", "upgrade", false)]
    [DataRow("http", "skip", false)]
    [DataRow("http", "postcmd", false)]
    [DataRow("ftp", "REPLY", false)]
    [DataRow("rtsp", "idle", false)]
    public void IsSupported_TheServerHalfsTable_SaysWhichCommandsItImplements(string server, string command, bool expected)
    {
        var supported = UpstreamTestCaseServerCommands.IsSupported(server, command);

        Assert.AreEqual(expected, supported);
    }

    [TestMethod]
    public void Http_IsTheCommandsTheHttpServerHalfAccepts()
    {
        var table = UpstreamTestCaseServerCommands.Http.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(HttpUpstreamTestCaseReplies.SupportedServerCommands.Order(StringComparer.Ordinal).ToArray(), table);
    }

    [TestMethod]
    public void IsSupported_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseServerCommands.IsSupported(null!, "idle"));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseServerCommands.IsSupported("http", null!));
    }
}
