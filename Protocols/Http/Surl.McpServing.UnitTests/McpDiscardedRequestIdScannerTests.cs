using System.Text;

namespace Surl.McpServing;

[TestClass]
public sealed class McpDiscardedRequestIdScannerTests
{
    [TestMethod]
    [DataRow("""{"id":7,"method":"x"}""", "7")]
    [DataRow("""{"a":{"id":1},"id":"abc"}""", "\"abc\"")]
    [DataRow("""{"id":7,"id":8}""", "7")]
    [DataRow("""{ "id" : 12 }""", "12")]
    [DataRow("""{"id"  	:  -3.5e2,"a":1}""", "-3.5e2")]
    [DataRow("{\"id\"\r\n:\r\n4\r\n}", "4")]
    [DataRow("{\"id\":\n\t5\t}", "5")]
    [DataRow("""{"id":+1}""", null)]
    [DataRow("""{"id":7]""", "7")]
    [DataRow("""{"id":"a\\"}""", "\"a\\\\\"")]
    [DataRow("""{"a":"x\\","b":"\"id\":5","c":[1,2,{"id":9}],"id":"last"}""", "\"last\"")]
    [DataRow("""{"a":{"id":1}}""", null)]
    [DataRow("""[{"id":1}]""", null)]
    [DataRow("""{"a":"\"id\":5"}""", null)]
    [DataRow("""{"method":"x"}""", null)]
    [DataRow("""{"id":null}""", null)]
    [DataRow("""{"id":true}""", null)]
    [DataRow("""{"id":{"x":1}}""", null)]
    [DataRow("""{"id":[1]}""", null)]
    [DataRow("""{"id"}""", null)]
    [DataRow("""{"id":-}""", null)]
    [DataRow("""{"id":01}""", null)]
    [DataRow("""{"id":"a\q"}""", null)]
    [DataRow("""{"id":7""", null)]
    [DataRow("""{"ide":5}""", null)]
    [DataRow("""{"i":5}""", null)]
    [DataRow("""{"i\d":5}""", null)]
    public void Finish_AfterTheMessage_ReturnsTheTopLevelIdOrNull(string message, string? expected)
    {
        McpDiscardedRequestIdScanner scanner = new();

        scanner.Feed(Encoding.UTF8.GetBytes(message));
        JsonRpcRequestId? id = scanner.Finish();

        Assert.AreEqual(expected, id?.Value.GetRawText());
    }

    [TestMethod]
    public void Feed_OneByteAtATime_ReadsTheSameId()
    {
        McpDiscardedRequestIdScanner scanner = new();

        foreach (byte value in Encoding.UTF8.GetBytes("""{"params":{"a":"\"id\":1"},"id":"a\"b"}"""))
        {
            scanner.Feed([value]);
        }

        Assert.AreEqual("\"a\\\"b\"", scanner.Finish()?.Value.GetRawText());
    }

    [TestMethod]
    public void Finish_IdOfExactlyTheMaximumBytes_ReturnsIt()
    {
        McpDiscardedRequestIdScanner scanner = new();
        string text = "\"" + new string('a', McpDiscardedRequestIdScanner.MaximumIdBytes - 2) + "\"";

        scanner.Feed(Encoding.UTF8.GetBytes("{\"id\":" + text + "}"));

        Assert.AreEqual(text, scanner.Finish()?.Value.GetRawText());
    }

    [TestMethod]
    public void Finish_IdOneByteOverTheMaximum_ReturnsNull()
    {
        McpDiscardedRequestIdScanner scanner = new();
        string text = "\"" + new string('a', McpDiscardedRequestIdScanner.MaximumIdBytes - 1) + "\"";

        scanner.Feed(Encoding.UTF8.GetBytes("{\"id\":" + text + "}"));

        Assert.IsNull(scanner.Finish());
    }

    [TestMethod]
    public void Finish_AfterAMessage_ForgetsItForTheNext()
    {
        McpDiscardedRequestIdScanner scanner = new();
        scanner.Feed("""{"id":1,"a":{"""u8);
        _ = scanner.Finish();

        scanner.Feed("""{"b":2}"""u8);

        Assert.IsNull(scanner.Finish());
    }
}
