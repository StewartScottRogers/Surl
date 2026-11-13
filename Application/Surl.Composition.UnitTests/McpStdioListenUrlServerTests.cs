using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class McpStdioListenUrlServerTests
{
    [TestMethod]
    public void Schemes_AreMcpStdioAlone()
    {
        var server = new McpStdioListenUrlServer();

        CollectionAssert.AreEqual(new[] { "mcp+stdio" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public void ServeAsync_AConnection_Throws()
    {
        var server = new McpStdioListenUrlServer();

        Assert.ThrowsExactly<InvalidOperationException>(() => server.ServeAsync(null!, null!));
    }
}
