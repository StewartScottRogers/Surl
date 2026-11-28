namespace Surl.Mcp;

// BL-2079: what reset_server tells an agent it changes (ADR-0167 decision 2). What a reset does is in StoreToolsTests.
[TestClass]
public sealed class ResetServerToolTests
{
    [TestMethod]
    [DataRow("files, mail, messages, accounts, proxy accounts, TLS-SRP accounts, Redis keys, LDAP directory, AMQP 0-9-1 exchanges and bindings, and DNS zones to their state at start")]
    [DataRow("It also clears Kafka producer state and consumer groups, and MQTT 5 sessions kept with no connection, pending Wills, queued deliveries and unacknowledged publishes.")]
    [DataRow("It discards every open or staged object upload (S3, Azure Storage, Google Cloud Storage, OCI) and Azure Storage's remembered committed block lists")]
    [DataRow("ends every OCI registry token, all MCP server OAuth state (clients, codes and tokens) and every MCP HTTP session.")]
    [DataRow("A store on disk refuses.")]
    public void Description_SaysWhatAResetReturnsClearsAndEnds(string phrase) =>
        StringAssert.Contains(ResetServerTool.Description, phrase);
}
