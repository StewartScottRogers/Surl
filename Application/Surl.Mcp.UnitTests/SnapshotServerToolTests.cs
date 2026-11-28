namespace Surl.Mcp;

// BL-2099: what snapshot_server tells an agent it copies and leaves out (ADR-0124 decision 3, ADR-0167). What a
// snapshot does is in StoreToolsTests.
[TestClass]
public sealed class SnapshotServerToolTests
{
    [TestMethod]
    [DataRow("Take a copy of a running Surl server's files, mail, messages, accounts, proxy accounts, TLS-SRP accounts, Redis keys, LDAP directory, AMQP 0-9-1 exchanges and bindings, Kafka producer state, MQTT 5 sessions kept with no connection and DNS zones, kept in memory by this host,")]
    [DataRow("It does not copy queues exclusive to a connection; Kafka consumer groups; MQTT 5 pending Wills, queued deliveries and unacknowledged publishes;")]
    [DataRow("open or staged object uploads (S3, Azure Storage, Google Cloud Storage, OCI) or Azure Storage's remembered committed block lists;")]
    [DataRow("OCI registry tokens; MCP server OAuth state (clients, codes and tokens); or MCP HTTP sessions.")]
    public void Description_SaysWhatASnapshotCopiesAndLeavesOut(string phrase) =>
        StringAssert.Contains(SnapshotServerTool.Description, phrase);
}
