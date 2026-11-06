using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-812: seed_mail, put_file, publish_message, add_account, reset_server, snapshot_server and restore_server
// (ADR-0124 decision 8), each proved by a fake client of the protocol that reads the store.
[TestClass]
public sealed class StoreToolsTests
{
    private const string SeededMail = "Subject: seeded\r\n\r\nseeded for alice\r\n";
    private const string Pop3Retr = "USER alice\r\nPASS secret\r\nRETR 1\r\nQUIT\r\n";
    private const string Pop3Stat = "USER alice\r\nPASS secret\r\nSTAT\r\nQUIT\r\n";

    private static readonly TimeSpan ExchangeTimeLimit = TimeSpan.FromSeconds(30);

    // MQTT 3.1.1 CONNECT (clean session, client "c", user "alice", password "secret"), SUBSCRIBE packet 1 to "t/1"
    // at QoS 0, DISCONNECT (MQTT 3.1.1 sections 3.1, 3.8 and 3.14), as Surl.Api's store tests send.
    private static readonly byte[] MqttSubscribeToT1AsAlice =
    [
        0x10, 0x1C, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0xC2, 0x00, 0x3C,
        0x00, 0x01, (byte)'c',
        0x00, 0x05, (byte)'a', (byte)'l', (byte)'i', (byte)'c', (byte)'e',
        0x00, 0x06, (byte)'s', (byte)'e', (byte)'c', (byte)'r', (byte)'e', (byte)'t',
        0x82, 0x08, 0x00, 0x01, 0x00, 0x03, (byte)'t', (byte)'/', (byte)'1', 0x00,
        0xE0, 0x00,
    ];

    // The PUBLISH a subscriber to "t/1" is sent for the retained "hi": retain flag set, QoS 0.
    private static readonly byte[] RetainedHiOnT1 = [0x31, 0x07, 0x00, 0x03, (byte)'t', (byte)'/', (byte)'1', (byte)'h', (byte)'i'];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeedMail_ThenAFakePop3Fetch_GetExchangesShowsTheSeededBytesSent()
    {
        await using var host = await StartMailHostAsync();

        var result = await CallAsync(host.SeedMail, $$"""{"server_id": "s1", "to": "alice@x", "message": {{JsonSerializer.Serialize(SeededMail)}}}""");
        await ServeAsync(host, "pop3", Pop3Retr);
        var exchanges = await CallAsync(host.GetExchanges, """{"server_id": "s1"}""");

        AssertResult(result, """{"delivered":true}""");
        var exchange = Assert.ContainsSingle(exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray());
        var sent = string.Concat(exchange.GetProperty("chunks").EnumerateArray()
            .Where(chunk => chunk.GetProperty("direction").GetString() == "sent")
            .Select(chunk => chunk.GetProperty("data").GetString()));
        StringAssert.Contains(sent, "seeded for alice\r\n.\r\n");
    }

    [TestMethod]
    public async Task SeedMail_GivenAsBase64_IsDelivered()
    {
        await using var host = await StartMailHostAsync();

        var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(SeededMail));
        var result = await CallAsync(host.SeedMail, $$"""{"server_id": "s1", "to": "alice@x", "message_base64": "{{base64}}"}""");

        AssertResult(result, """{"delivered":true}""");
        StringAssert.Contains(await ServeAsync(host, "pop3", Pop3Retr), "seeded for alice\r\n.\r\n");
    }

    [TestMethod]
    public async Task SeedMail_ForAnUnknownRecipient_IsAnErrorWithTheStoresRefusal()
    {
        await using var host = await StartMailHostAsync();

        var result = await CallAsync(host.SeedMail, """{"server_id": "s1", "to": "nobody@x", "message": "x"}""");

        Assert.IsTrue(result.IsError);
        StringAssert.Contains(Assert.ContainsSingle(result.TextContent), "NoSuchAccount");
    }

    [TestMethod]
    [DataRow("""{"server_id": "s1", "to": "alice@x"}""", "give exactly one of message and message_base64")]
    [DataRow("""{"server_id": "s1", "to": "alice@x", "message": "a", "message_base64": "YQ=="}""", "give exactly one of message and message_base64")]
    [DataRow("""{"server_id": "s1", "to": "alice@x", "message_base64": "not base64!"}""", "message_base64: expected a base64 string")]
    public async Task SeedMail_ContentNotExactlyOneValidProperty_IsAnErrorSayingWhy(string arguments, string expected)
    {
        await using var host = await StartMailHostAsync();

        var result = await CallAsync(host.SeedMail, arguments);

        AssertFailure(result, expected);
    }

    [TestMethod]
    public async Task PutFile_PastSixteenMebibytes_IsAnErrorAndWritesNothing()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");
        var text = new string('a', SeededContent.MaxBytes + 1);

        var result = await CallAsync(host.PutFile, $$"""{"server_id": "s1", "path": "/big.txt", "text": "{{text}}"}""");

        AssertFailure(result, "text: 16777217 bytes is more than the 16777216 (16 MiB) one seed may carry");
    }

    [TestMethod]
    public async Task PutFile_ThenAFakeHttpGet_ServesTheFileAndAnswersItsPathAndByteCount()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");

        var result = await CallAsync(host.PutFile, """{"server_id": "s1", "path": "/a.txt", "base64": "c2VlZGVk"}""");
        var written = await ServeAsync(host, "http", "GET /a.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n");

        AssertResult(result, """{"path":"/a.txt","byte_count":6}""");
        StringAssert.StartsWith(written, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(written, "seeded");
    }

    [TestMethod]
    public async Task PutFile_AtAPathNoClientMayWrite_IsAnErrorWithTheStoresRefusal()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");

        var result = await CallAsync(host.PutFile, """{"server_id": "s1", "path": "/.surl/x", "text": "x"}""");

        Assert.IsTrue(result.IsError);
        StringAssert.Contains(Assert.ContainsSingle(result.TextContent), "NotPermitted");
    }

    [TestMethod]
    public async Task PublishMessage_RetainedOnATopic_IsDeliveredToAnMqttSubscriber()
    {
        await using var host = await StartHostAsync("mqtt://127.0.0.1:0", "--user", "alice:secret", "--allow-plaintext-auth");

        var result = await CallAsync(host.PublishMessage, """{"server_id": "s1", "topic": "t/1", "payload": "hi", "retain": true}""");
        var written = await ServeBytesAsync(host, "mqtt", MqttSubscribeToT1AsAlice);

        AssertResult(result, """{"published":true}""");
        Assert.IsGreaterThanOrEqualTo(0, written.AsSpan().IndexOf(RetainedHiOnT1));
    }

    [TestMethod]
    public async Task PublishMessage_NotRetained_IsPublished()
    {
        await using var host = await StartHostAsync("mqtt://127.0.0.1:0");

        var result = await CallAsync(host.PublishMessage, """{"server_id": "s1", "topic": "t/1", "payload_base64": "aGk=", "retain": false}""");

        AssertResult(result, """{"published":true}""");
    }

    [TestMethod]
    public async Task PublishMessage_IntoAQueueThatDoesNotExist_IsAnErrorWithTheBussRefusal()
    {
        await using var host = await StartHostAsync("mqtt://127.0.0.1:0");

        var result = await CallAsync(host.PublishMessage, """{"server_id": "s1", "queue": "q", "payload": "hi"}""");

        Assert.IsTrue(result.IsError);
        StringAssert.Contains(Assert.ContainsSingle(result.TextContent), "UnknownQueue");
    }

    [TestMethod]
    [DataRow("""{"server_id": "s1", "payload": "hi"}""", "give exactly one of topic and queue")]
    [DataRow("""{"server_id": "s1", "topic": "t", "queue": "q", "payload": "hi"}""", "give exactly one of topic and queue")]
    [DataRow("""{"server_id": "s1", "queue": "q", "payload": "hi", "retain": true}""", "retain applies to a topic, not a queue")]
    [DataRow("""{"server_id": "s1", "topic": "t"}""", "give exactly one of payload and payload_base64")]
    public async Task PublishMessage_ArgumentsThatDoNotMakeOneMessage_IsAnErrorSayingWhy(string arguments, string expected)
    {
        await using var host = await StartHostAsync("mqtt://127.0.0.1:0");

        var result = await CallAsync(host.PublishMessage, arguments);

        AssertFailure(result, expected);
    }

    [TestMethod]
    public async Task AddAccount_ThenAFakePop3Login_IsAcceptedAndNoResultOrLogLineHoldsThePassword()
    {
        await using var host = await StartMailHostAsync();

        var result = await CallAsync(host.AddAccount, """{"server_id": "s1", "user": "bob", "password": "pa55-w0rd"}""");
        var again = await CallAsync(host.AddAccount, """{"server_id": "s1", "user": "bob", "password": "pa55-w0rd"}""");
        var written = await ServeAsync(host, "pop3", "USER bob\r\nPASS pa55-w0rd\r\nQUIT\r\n");
        var events = await CallAsync(host.GetEvents, """{"server_id": "s1"}""");

        AssertResult(result, """{"user":"bob"}""");
        AssertFailure(again, "user bob already has an account");
        StringAssert.Contains(written, "+OK");
        Assert.DoesNotContain("-ERR", written);
        foreach (var text in new[] { result.TextContent[0], again.TextContent[0], events.TextContent[0], host.ErrorOutput.ToString() })
        {
            Assert.DoesNotContain("pa55-w0rd", text);
        }
    }

    [TestMethod]
    public async Task AddAccount_AUserNameHoldingAColon_IsAnError()
    {
        await using var host = await StartMailHostAsync();

        var result = await CallAsync(host.AddAccount, """{"server_id": "s1", "user": "b:ob", "password": "secret"}""");

        AssertFailure(result, "user: a user name may not hold ':'");
    }

    [TestMethod]
    public async Task ResetServer_AfterASeed_EmptiesTheMailbox()
    {
        await using var host = await StartMailHostAsync();
        await CallAsync(host.SeedMail, """{"server_id": "s1", "to": "alice@x", "message": "Subject: a\r\n\r\nb\r\n"}""");

        var result = await CallAsync(host.ResetServer, """{"server_id": "s1"}""");

        AssertResult(result, """{"server_id":"s1"}""");
        StringAssert.Contains(await ServeAsync(host, "pop3", Pop3Stat), "+OK 0 0\r\n");
    }

    [TestMethod]
    public async Task SnapshotServer_ThenResetThenRestoreServer_BringsTheSeededMessageBack()
    {
        await using var host = await StartMailHostAsync();
        await CallAsync(host.SeedMail, $$"""{"server_id": "s1", "to": "alice@x", "message": {{JsonSerializer.Serialize(SeededMail)}}}""");

        var snapshot = await CallAsync(host.SnapshotServer, """{"server_id": "s1"}""");
        await CallAsync(host.ResetServer, """{"server_id": "s1"}""");
        var restore = await CallAsync(host.RestoreServer, """{"server_id": "s1", "snapshot_id": "p1"}""");

        AssertResult(snapshot, """{"snapshot_id":"p1"}""");
        AssertResult(restore, """{"server_id":"s1","snapshot_id":"p1"}""");
        StringAssert.Contains(await ServeAsync(host, "pop3", Pop3Retr), "seeded for alice\r\n.\r\n");
    }

    [TestMethod]
    public async Task RestoreServer_PastTheSnapshotBound_TheOldestIsDroppedAndNamingItIsAnError()
    {
        await using var host = await StartMailHostAsync();
        for (var taken = 0; taken <= ServerSnapshots.MaxSnapshots; taken++)
        {
            await CallAsync(host.SnapshotServer, """{"server_id": "s1"}""");
        }

        var oldest = await CallAsync(host.RestoreServer, """{"server_id": "s1", "snapshot_id": "p1"}""");
        var oldestKept = await CallAsync(host.RestoreServer, """{"server_id": "s1", "snapshot_id": "p2"}""");

        AssertFailure(oldest, "server s1 has no snapshot p1; only its newest 4 snapshots are kept");
        Assert.IsFalse(oldestKept.IsError);
    }

    [TestMethod]
    public async Task RestoreServer_AnUnknownSnapshotId_IsAnError()
    {
        await using var host = await StartMailHostAsync();

        var result = await CallAsync(host.RestoreServer, """{"server_id": "s1", "snapshot_id": "p9"}""");

        AssertFailure(result, RestoreServerTool.NoSnapshot("s1", "p9"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ResetAndSnapshot_OfAStoreOnDisk_AreErrorsWithTheStoresRefusal()
    {
        var directory = Directory.CreateTempSubdirectory("surl-mcp-store-tools-");
        try
        {
            await using var host = new McpTestHost(new SurlMcpHostSettings { Root = directory.FullName });
            await CallAsync(host.StartServer, $$"""{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["--directory", {{JsonSerializer.Serialize(directory.FullName)}}]}""");

            var reset = await CallAsync(host.ResetServer, """{"server_id": "s1"}""");
            var snapshot = await CallAsync(host.SnapshotServer, """{"server_id": "s1"}""");

            Assert.IsTrue(reset.IsError);
            Assert.IsTrue(snapshot.IsError);
            StringAssert.Contains(reset.TextContent[0], "disk");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(SeedMailTool.Name, """{"server_id": "s9", "to": "a@x", "message": "x"}""")]
    [DataRow(PutFileTool.Name, """{"server_id": "s9", "path": "/a", "text": "x"}""")]
    [DataRow(PublishMessageTool.Name, """{"server_id": "s9", "topic": "t", "payload": "x"}""")]
    [DataRow(AddAccountTool.Name, """{"server_id": "s9", "user": "a", "password": "b"}""")]
    [DataRow(ResetServerTool.Name, """{"server_id": "s9"}""")]
    [DataRow(SnapshotServerTool.Name, """{"server_id": "s9"}""")]
    [DataRow(RestoreServerTool.Name, """{"server_id": "s9", "snapshot_id": "p1"}""")]
    [DataRow(AddFaultTool.Name, """{"server_id": "s9", "fault": "drop"}""")]
    [DataRow(RemoveFaultTool.Name, """{"server_id": "s9", "fault_id": "f1"}""")]
    public async Task EveryNewTool_AnUnknownServerId_IsStopServersError(string toolName, string arguments)
    {
        await using var host = await StartMailHostAsync();
        McpTool[] tools =
        [
            host.SeedMail, host.PutFile, host.PublishMessage, host.AddAccount, host.ResetServer, host.SnapshotServer,
            host.RestoreServer, host.AddFault, host.RemoveFault,
        ];

        var result = await CallAsync(tools.Single(tool => tool.Name == toolName), arguments);

        AssertFailure(result, StopServerTool.NoRunningServer("s9"));
    }

    [TestMethod]
    public void Create_NullServers_Throws()
    {
        Func<RunningSurlServers, McpTool>[] creates =
        [
            SeedMailTool.Create, PutFileTool.Create, PublishMessageTool.Create, AddAccountTool.Create, ResetServerTool.Create,
            SnapshotServerTool.Create, RestoreServerTool.Create, AddFaultTool.Create, RemoveFaultTool.Create,
        ];

        foreach (var create in creates)
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => create(null!));
        }
    }

    [TestMethod]
    public async Task ServerSnapshotsAndFind_NullArguments_Throw()
    {
        await using var host = new McpTestHost();
        var snapshots = new ServerSnapshots();

        Assert.ThrowsExactly<ArgumentNullException>(() => snapshots.Keep(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => snapshots.Find(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => host.Servers.Find(null!));
    }

    internal static void AssertResult(McpToolResult result, string expectedJson)
    {
        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        Assert.AreEqual(expectedJson, result.StructuredContent!.Value.GetRawText());
        Assert.AreEqual(expectedJson, Assert.ContainsSingle(result.TextContent));
    }

    internal static void AssertFailure(McpToolResult result, string expectedText)
    {
        Assert.IsTrue(result.IsError);
        Assert.AreEqual(expectedText, Assert.ContainsSingle(result.TextContent));
    }

    private async Task<McpTestHost> StartMailHostAsync() =>
        await StartHostAsync("pop3://127.0.0.1:0", "--user", "alice:secret", "--allow-plaintext-auth");

    private async Task<McpTestHost> StartHostAsync(string listenUrl, params string[] words)
    {
        var host = new McpTestHost();
        var started = await CallAsync(
            host.StartServer, $$"""{"listen_urls": ["{{listenUrl}}"], "arguments": {{JsonSerializer.Serialize(words)}}}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        return host;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    private async Task<string> ServeAsync(McpTestHost host, string scheme, string clientText) =>
        Encoding.ASCII.GetString(await ServeBytesAsync(host, scheme, Encoding.ASCII.GetBytes(clientText)));

    // Brings one fake client in on the listener for scheme and waits until its exchange has ended.
    private async Task<byte[]> ServeBytesAsync(McpTestHost host, string scheme, byte[] clientBytes)
    {
        var connection = new FakeConnection(clientBytes);
        host.Seams.ListenerFactory.Accept(scheme, connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return connection.WrittenToClient;
    }
}
