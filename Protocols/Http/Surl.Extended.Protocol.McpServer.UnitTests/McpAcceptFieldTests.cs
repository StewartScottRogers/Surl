namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpAcceptFieldTests
{
    [TestMethod]
    [DataRow("application/json, text/event-stream")]
    [DataRow("text/event-stream;q=0.5, application/json;q=0.9")]
    [DataRow("*/*")]
    [DataRow("application/*, text/*")]
    [DataRow("APPLICATION/JSON, Text/Event-Stream")]
    [DataRow("*/*, application/xml;q=0")]
    [DataRow("application/json;q=abc, text/event-stream")]
    public void AcceptsJsonAndEventStream_BothAcceptable_IsTrue(string accept)
    {
        bool accepts = McpAcceptField.AcceptsJsonAndEventStream(accept);

        Assert.IsTrue(accepts);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("application/json")]
    [DataRow("text/event-stream")]
    [DataRow("application/json, text/event-stream;q=0")]
    [DataRow("*/*, application/json;q=0")]
    [DataRow("application/json, text/html")]
    [DataRow("application/json, text")]
    public void AcceptsJsonAndEventStream_ATypeMissingOrExcluded_IsFalse(string? accept)
    {
        bool accepts = McpAcceptField.AcceptsJsonAndEventStream(accept);

        Assert.IsFalse(accepts);
    }
}
