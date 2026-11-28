namespace Surl.Mcp;

// BL-2079: what restore_server tells an agent it replaces and clears (ADR-0167 decision 2). What a restore does is in
// StoreToolsTests.
[TestClass]
public sealed class RestoreServerToolTests
{
    [TestMethod]
    [DataRow("files, mail, messages, accounts, proxy accounts, TLS-SRP accounts, Redis keys, LDAP directory, AMQP 0-9-1 exchanges and bindings, Kafka producer state, MQTT 5 sessions kept with no connection and DNS zones with a snapshot")]
    [DataRow("What a snapshot does not copy is cleared, even if it began before the snapshot: the messages of queues exclusive to a connection (the queues are kept);")]
    [DataRow("Kafka consumer groups; MQTT 5 pending Wills, queued deliveries and unacknowledged publishes;")]
    [DataRow("every open or staged object upload (S3, Azure Storage, Google Cloud Storage, OCI) and Azure Storage's remembered committed block lists;")]
    [DataRow("every OCI registry token; all MCP server OAuth state (clients, codes and tokens); and every MCP HTTP session.")]
    public void Description_SaysWhatARestoreReplacesAndClears(string phrase) =>
        StringAssert.Contains(RestoreServerTool.Description, phrase);
}
