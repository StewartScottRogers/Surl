using System.Text;
using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class JsonRpcMessageParserTests
{
    [TestMethod]
    public void Parse_Request_ReadsIdMethodAndParams()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":"x","method":"m","params":{"a":1},"extra":true}""");

        Assert.AreEqual("x", request.Id.Value.GetString());
        Assert.AreEqual("m", request.Method);
        Assert.AreEqual(1, request.Params!.Value.GetProperty("a").GetInt32());
    }

    [TestMethod]
    public void Parse_NotificationWithArrayParams_ReadsThem()
    {
        JsonRpcNotification notification = (JsonRpcNotification)Parse("""{"jsonrpc":"2.0","method":"n","params":[1,2]}""");

        Assert.AreEqual("n", notification.Method);
        Assert.AreEqual(JsonValueKind.Array, notification.Params!.Value.ValueKind);
    }

    [TestMethod]
    public void Parse_NullParams_IsAbsent()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":1,"method":"m","params":null}""");

        Assert.IsNull(request.Params);
    }

    [TestMethod]
    public void Parse_KeyGivenTwice_TakesItsLastValue()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":1,"method":"first","method":"last"}""");

        Assert.AreEqual("last", request.Method);
    }

    [TestMethod]
    public void Parse_NotUtf8_IsAParseError()
    {
        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)JsonRpcMessageParser.Parse(new byte[] { (byte)'"', 0xFF, (byte)'"' });

        Assert.AreEqual(JsonRpcError.ParseError, invalid.Error);
        Assert.IsNull(invalid.Id);
    }

    [TestMethod]
    public void Parse_NestedDeeperThan64Levels_IsAParseError()
    {
        string deep = """{"jsonrpc":"2.0","id":1,"method":"m","params":""" + new string('[', 64) + new string(']', 64) + "}";

        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)Parse(deep);

        Assert.AreEqual(JsonRpcErrorCodes.ParseError, invalid.Error.Code);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("""[{"jsonrpc":"2.0","id":1,"method":"ping"}]""")]
    [DataRow("42")]
    [DataRow("""{"jsonrpc":"2.0","id":true,"method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":{},"method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":[],"method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":null,"method":"m"}""")]
    public void Parse_NotARequestOrABadId_IsInvalidRequestWithNoId(string message)
    {
        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)Parse(message);

        Assert.AreEqual(JsonRpcError.InvalidRequest, invalid.Error);
        Assert.IsNull(invalid.Id);
    }

    [TestMethod]
    [DataRow("""{"id":7,"method":"m"}""")]
    [DataRow("""{"jsonrpc":2.0,"id":7,"method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.1","id":7,"method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":7}""")]
    [DataRow("""{"jsonrpc":"2.0","id":7,"method":3}""")]
    [DataRow("""{"jsonrpc":"2.0","id":7,"method":"m","params":"text"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":7,"method":"m","params":5}""")]
    public void Parse_BadVersionMethodOrParams_IsInvalidRequestEchoingTheId(string message)
    {
        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)Parse(message);

        Assert.AreEqual(JsonRpcError.InvalidRequest, invalid.Error);
        Assert.AreEqual(7, invalid.Id!.Value.GetInt32());
    }

    [TestMethod]
    public void Parse_NotificationWithBadVersion_IsInvalidRequestWithNoId()
    {
        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)Parse("""{"jsonrpc":"1.0","method":"m"}""");

        Assert.IsNull(invalid.Id);
    }

    [TestMethod]
    public void Parse_ResultWithAMethod_IsReadAsACall()
    {
        Assert.IsInstanceOfType<JsonRpcRequest>(Parse("""{"jsonrpc":"2.0","id":1,"method":"m","result":{}}"""));
    }

    [TestMethod]
    public void Parse_ClientResponse_IsTheOneInstance()
    {
        Assert.AreSame(JsonRpcClientResponse.Instance, Parse("""{"jsonrpc":"2.0","id":1,"result":{}}"""));
    }

    private static JsonRpcIncoming Parse(string message) => JsonRpcMessageParser.Parse(Encoding.UTF8.GetBytes(message));
}
