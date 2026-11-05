using System.Text;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpEventStreamTests
{
    [TestMethod]
    public void Write_AResponse_IsOneDataLineThenABlankLine()
    {
        byte[] body = McpEventStream.Write("""{"jsonrpc":"2.0","id":1,"result":{}}"""u8.ToArray());

        Assert.AreEqual("data: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}\n\n", Encoding.UTF8.GetString(body));
    }

    [TestMethod]
    public void Write_ACancelledCall_IsNoEvent()
    {
        byte[] body = McpEventStream.Write(null);

        Assert.IsEmpty(body);
    }
}
