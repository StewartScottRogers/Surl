namespace Surl.McpServing;

[TestClass]
public sealed class JsonRpcErrorTests
{
    [TestMethod]
    public void ErrorTable_Members_HaveTheAdrsCodesAndMessages()
    {
        Assert.AreEqual(new JsonRpcError(-32700, "Parse error"), JsonRpcError.ParseError);
        Assert.AreEqual(new JsonRpcError(-32600, "Invalid Request"), JsonRpcError.InvalidRequest);
        Assert.AreEqual(new JsonRpcError(-32601, "Method not found"), JsonRpcError.MethodNotFound);
        Assert.AreEqual(new JsonRpcError(-32602, "Invalid params: cursor"), JsonRpcError.InvalidParams("cursor"));
        Assert.AreEqual(-32603, JsonRpcErrorCodes.InternalError);
        Assert.AreEqual(-32002, JsonRpcErrorCodes.ResourceNotFound);
    }
}
