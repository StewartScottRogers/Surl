using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpServerIdentityTests
{
    [TestMethod]
    public void CreateServerInfo_AVersion_IsSurlWithItsTitleAndThatVersion()
    {
        McpServerInfo serverInfo = McpServerIdentity.CreateServerInfo("8.21.0");

        Assert.AreEqual(new McpServerInfo("surl", "Surl MCP server", "8.21.0"), serverInfo);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void CreateServerInfo_BlankVersion_Throws(string surlVersion) =>
        Assert.ThrowsExactly<ArgumentException>(() => McpServerIdentity.CreateServerInfo(surlVersion));
}
