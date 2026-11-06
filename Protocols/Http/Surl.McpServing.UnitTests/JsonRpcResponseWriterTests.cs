using System.Text;
using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class JsonRpcResponseWriterTests
{
    [TestMethod]
    public void WriteSuccess_ResultWithANewline_WritesItEscapedOnOneLine()
    {
        JsonRpcRequestId id = new(JsonDocument.Parse("\"r\"").RootElement);
        JsonElement result = JsonDocument.Parse("""{"text":"a\nb"}""").RootElement;

        string response = Encoding.UTF8.GetString(JsonRpcResponseWriter.WriteSuccess(id, result));

        Assert.AreEqual("""{"jsonrpc":"2.0","id":"r","result":{"text":"a\nb"}}""", response);
        Assert.DoesNotContain("\n", response);
    }

    [TestMethod]
    public void WriteError_MessageWithANewline_WritesItEscaped()
    {
        string response = Encoding.UTF8.GetString(JsonRpcResponseWriter.WriteError(null, new JsonRpcError(-1, "a\nb")));

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-1,"message":"a\nb"}}""", response);
    }
}
