using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1201: list_mailboxes, list_messages, read_message, read_file, list_files, browse_queue, read_retained_message
// and read_log (ADR-0164 decision 6), each reading back what seed_mail, put_file and publish_message put in.
[TestClass]
public sealed class ReadBackToolsTests
{
    private const string Mail = "Subject: seeded\r\n\r\nseeded for alice\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeededMail_ReadsBackAsMailboxesMessagesAndTheSameBytes()
    {
        await using var host = await StartSeededHostAsync();

        var mailboxes = await CallAsync(host.ListMailboxes, """{"server_id": "s1", "owner": "alice"}""");
        var messages = await CallAsync(host.ListMessages, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX", "after_uid": 0, "limit": 5}""");
        var defaults = await CallAsync(host.ListMessages, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX"}""");
        var message = await CallAsync(host.ReadMessage, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX", "uid": 1}""");

        var inbox = Assert.ContainsSingle(Ok(mailboxes).GetProperty("mailboxes").EnumerateArray());
        Assert.AreEqual("INBOX", inbox.GetProperty("name").GetString());
        Assert.AreEqual(1, inbox.GetProperty("message_count").GetInt32());
        Assert.AreEqual(1, inbox.GetProperty("unseen_count").GetInt32());
        var listed = Assert.ContainsSingle(Ok(messages).GetProperty("messages").EnumerateArray());
        Assert.AreEqual(1, listed.GetProperty("uid").GetInt32());
        Assert.AreEqual(Mail.Length, listed.GetProperty("size").GetInt32());
        Assert.IsFalse(Ok(messages).GetProperty("has_more").GetBoolean());
        Assert.AreEqual(1, Ok(defaults).GetProperty("messages").GetArrayLength());
        Assert.AreEqual(Mail, Ok(message).GetProperty("text").GetString());
        Assert.AreEqual(1, Ok(message).GetProperty("uid").GetInt32());
    }

    [TestMethod]
    public async Task ListMailboxes_ShowsUseOnlyForAMailboxThatHasOne()
    {
        await using var host = new McpTestHost();
        var started = await CallAsync(
            host.StartServer,
            """{"listen_urls": ["smtp://127.0.0.1:0"], "arguments": ["--mail-domain", "example.com", "--mailbox", "alice@example.com:secret;folders=Sent=sent,Work"]}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());

        var result = await CallAsync(host.ListMailboxes, """{"server_id": "s1", "owner": "alice@example.com"}""");

        var boxes = Ok(result).GetProperty("mailboxes").EnumerateArray().ToDictionary(box => box.GetProperty("name").GetString()!);
        Assert.AreEqual(@"\Sent", boxes["Sent"].GetProperty("use").GetString());
        Assert.IsFalse(boxes["Work"].TryGetProperty("use", out _));
        Assert.IsFalse(boxes["INBOX"].TryGetProperty("use", out _));
    }

    [TestMethod]
    public async Task SeededFiles_ReadBackAsTheFileAndTheFolderListing()
    {
        await using var host = await StartSeededHostAsync();

        var file = await CallAsync(host.ReadFile, """{"server_id": "s1", "path": "/reports/a.txt"}""");
        var root = await CallAsync(host.ListFiles, """{"server_id": "s1", "path": "/"}""");
        var paged = await CallAsync(host.ListFiles, """{"server_id": "s1", "path": "/", "after_name": "b.bin", "limit": 1}""");
        var first = await CallAsync(host.ListFiles, """{"server_id": "s1", "path": "/", "limit": 1}""");

        var read = Ok(file);
        Assert.AreEqual("/reports/a.txt", read.GetProperty("path").GetString());
        Assert.AreEqual(10, read.GetProperty("byte_count").GetInt64());
        Assert.AreEqual("file bytes", read.GetProperty("text").GetString());
        Assert.IsFalse(read.TryGetProperty("base64", out _));
        Assert.IsTrue(read.TryGetProperty("modified_at", out _));
        Assert.IsTrue(read.TryGetProperty("content_type", out _));
        var entries = Ok(root).GetProperty("entries").EnumerateArray().ToArray();
        Assert.AreEqual("b.bin", entries[0].GetProperty("name").GetString());
        Assert.AreEqual("file", entries[0].GetProperty("kind").GetString());
        Assert.AreEqual(2, entries[0].GetProperty("byte_count").GetInt64());
        Assert.AreEqual("reports", entries[1].GetProperty("name").GetString());
        Assert.AreEqual("folder", entries[1].GetProperty("kind").GetString());
        Assert.IsFalse(entries[1].TryGetProperty("byte_count", out _));
        Assert.AreEqual("reports", Ok(paged).GetProperty("entries")[0].GetProperty("name").GetString());
        Assert.IsTrue(Ok(first).GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    [DataRow("/wA=", true)]
    [DataRow("YQBi", true)]
    [DataRow("YQk6Cg==", false)]
    public async Task ReadFile_BinaryContentIsBase64AndTextIsText(string base64, bool isBase64)
    {
        await using var host = await StartSeededHostAsync();
        await CallAsync(host.PutFile, $$"""{"server_id": "s1", "path": "/x", "base64": "{{base64}}"}""");

        var read = Ok(await CallAsync(host.ReadFile, """{"server_id": "s1", "path": "/x"}"""));

        Assert.AreEqual(isBase64, read.TryGetProperty("base64", out var encoded));
        Assert.AreEqual(!isBase64, read.TryGetProperty("text", out _));
        if (isBase64)
        {
            Assert.AreEqual(base64, encoded.GetString());
        }
    }

    [TestMethod]
    public async Task SeededBus_ReadsBackTheQueueTheRetainedMessageAndTheLog()
    {
        await using var host = await StartSeededHostAsync();

        var queue = Ok(await CallAsync(host.BrowseQueue, """{"server_id": "s1", "queue": "orders", "after_position": -1, "limit": 10}"""));
        var queueDefaults = Ok(await CallAsync(host.BrowseQueue, """{"server_id": "s1", "queue": "orders"}"""));
        var retained = Ok(await CallAsync(host.ReadRetainedMessage, """{"server_id": "s1", "topic": "t/1"}"""));
        var none = await CallAsync(host.ReadRetainedMessage, """{"server_id": "s1", "topic": "no/such"}""");
        var log = Ok(await CallAsync(host.ReadLog, """{"server_id": "s1", "topic": "t/1", "from_offset": 0, "limit": 10}"""));
        var logDefaults = Ok(await CallAsync(host.ReadLog, """{"server_id": "s1", "topic": "t/1"}"""));

        var queued = Assert.ContainsSingle(queue.GetProperty("messages").EnumerateArray());
        Assert.AreEqual(0, queued.GetProperty("position").GetInt32());
        Assert.AreEqual("queued", queued.GetProperty("text").GetString());
        Assert.AreEqual(1, queue.GetProperty("ready_count").GetInt32());
        Assert.AreEqual(0, queue.GetProperty("in_flight_count").GetInt32());
        Assert.IsFalse(queue.GetProperty("has_more").GetBoolean());
        Assert.AreEqual(1, queueDefaults.GetProperty("messages").GetArrayLength());
        Assert.AreEqual("t/1", retained.GetProperty("topic").GetString());
        Assert.AreEqual(0, retained.GetProperty("qos").GetInt32());
        Assert.AreEqual("retained", retained.GetProperty("text").GetString());
        StoreToolsTests.AssertResult(none, """{"retained":false}""");
        var record = Assert.ContainsSingle(log.GetProperty("records").EnumerateArray());
        Assert.AreEqual(0, record.GetProperty("offset").GetInt64());
        Assert.AreEqual("retained", record.GetProperty("text").GetString());
        Assert.AreEqual(1, log.GetProperty("next_offset").GetInt64());
        Assert.AreEqual(1, logDefaults.GetProperty("records").GetArrayLength());
    }

    [TestMethod]
    [DataRow(ListMailboxesTool.Name, """{"server_id": "s9", "owner": "alice"}""")]
    [DataRow(ListMessagesTool.Name, """{"server_id": "s9", "owner": "alice", "mailbox": "INBOX"}""")]
    [DataRow(ReadMessageTool.Name, """{"server_id": "s9", "owner": "alice", "mailbox": "INBOX", "uid": 1}""")]
    [DataRow(ReadFileTool.Name, """{"server_id": "s9", "path": "/a"}""")]
    [DataRow(ListFilesTool.Name, """{"server_id": "s9", "path": "/"}""")]
    [DataRow(BrowseQueueTool.Name, """{"server_id": "s9", "queue": "orders"}""")]
    [DataRow(ReadRetainedMessageTool.Name, """{"server_id": "s9", "topic": "t"}""")]
    [DataRow(ReadLogTool.Name, """{"server_id": "s9", "topic": "t"}""")]
    public async Task EveryReadBackTool_AnUnknownServerId_IsStopServersError(string toolName, string arguments)
    {
        await using var host = await StartSeededHostAsync();

        var result = await CallAsync(FindTool(host, toolName), arguments);

        StoreToolsTests.AssertFailure(result, StopServerTool.NoRunningServer("s9"));
    }

    [TestMethod]
    [DataRow(ListMailboxesTool.Name, """{"server_id": "s1", "owner": "nobody"}""")]
    [DataRow(ListMessagesTool.Name, """{"server_id": "s1", "owner": "alice", "mailbox": "Nope"}""")]
    [DataRow(ListMessagesTool.Name, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX", "limit": 5000}""")]
    [DataRow(ReadMessageTool.Name, """{"server_id": "s1", "owner": "alice", "mailbox": "INBOX", "uid": 99}""")]
    [DataRow(ReadFileTool.Name, """{"server_id": "s1", "path": "/none.txt"}""")]
    [DataRow(ReadFileTool.Name, """{"server_id": "s1", "path": "/.surl/x"}""")]
    [DataRow(ListFilesTool.Name, """{"server_id": "s1", "path": "/none"}""")]
    [DataRow(BrowseQueueTool.Name, """{"server_id": "s1", "queue": "nope"}""")]
    [DataRow(ReadLogTool.Name, """{"server_id": "s1", "topic": "no/such"}""")]
    public async Task EveryReadBackTool_AnAbsentOrRefusedItem_IsAnErrorWithTheApisMessage(string toolName, string arguments)
    {
        await using var host = await StartSeededHostAsync();

        var result = await CallAsync(FindTool(host, toolName), arguments);

        Assert.IsTrue(result.IsError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Assert.ContainsSingle(result.TextContent)));
    }

    [TestMethod]
    public void Create_NullServers_Throws()
    {
        Func<RunningSurlServers, McpTool>[] creates =
        [
            ListMailboxesTool.Create, ListMessagesTool.Create, ReadMessageTool.Create, ReadFileTool.Create, ListFilesTool.Create,
            BrowseQueueTool.Create, ReadRetainedMessageTool.Create, ReadLogTool.Create,
        ];

        foreach (var create in creates)
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => create(null!));
        }
    }

    [TestMethod]
    public void WriteRecord_WithAKeyAndABinaryPayload_WritesTheKeyAndBase64()
    {
        var record = new SurlLogRecord(7, new DateTimeOffset(2026, 11, 15, 1, 2, 3, TimeSpan.Zero), Encoding.UTF8.GetBytes("k1"), new byte[] { 0xFF, 0x00 });

        var json = WriteObject(writer => ReadLogTool.WriteRecord(writer, record));
        var binaryKey = WriteObject(writer => ReadLogTool.WriteRecord(writer, record with { Key = new byte[] { 0xFF } }));
        var noKey = WriteObject(writer => ReadLogTool.WriteRecord(writer, record with { Key = null }));

        // The JSON writer escapes the plus of the UTC offset.
        Assert.AreEqual("{\"offset\":7,\"timestamp\":\"2026-11-15T01:02:03.0000000" + "\\" + "u002B00:00\",\"key\":\"k1\",\"base64\":\"/wA=\"}", json);
        StringAssert.Contains(binaryKey, "\"key_base64\":\"/w==\",");
        Assert.DoesNotContain("key", noKey);
    }

    [TestMethod]
    public void WriteMessage_WithHeaders_WritesEachNameAndValue()
    {
        var message = new SurlQueueMessage(2, "hi"u8.ToArray(), [KeyValuePair.Create("h", "v")]);

        var json = WriteObject(writer => BrowseQueueTool.WriteMessage(writer, message));

        Assert.AreEqual("""{"position":2,"text":"hi","headers":[{"name":"h","value":"v"}]}""", json);
    }

    private static string WriteObject(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static McpTool FindTool(McpTestHost host, string name) =>
        new[] { host.ListMailboxes, host.ListMessages, host.ReadMessage, host.ReadFile, host.ListFiles, host.BrowseQueue, host.ReadRetainedMessage, host.ReadLog }
            .Single(tool => tool.Name == name);

    private static JsonElement Ok(McpToolResult result)
    {
        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        Assert.AreEqual(result.StructuredContent!.Value.GetRawText(), Assert.ContainsSingle(result.TextContent));
        return result.StructuredContent!.Value;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    // One server holding mail, files and a bus, seeded through the seeding tools.
    private async Task<McpTestHost> StartSeededHostAsync()
    {
        var host = new McpTestHost();
        var started = await CallAsync(
            host.StartServer,
            """{"listen_urls": ["pop3://127.0.0.1:0", "http://127.0.0.1:0", "mqtt://127.0.0.1:0"], "arguments": ["--user", "alice:secret", "--allow-plaintext-auth", "--queue", "orders"]}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        var message = JsonSerializer.Serialize(Mail);
        Assert.IsFalse((await CallAsync(host.SeedMail, $$"""{"server_id": "s1", "to": "alice@x", "message": {{message}}}""")).IsError);
        Assert.IsFalse((await CallAsync(host.PutFile, """{"server_id": "s1", "path": "/reports/a.txt", "text": "file bytes"}""")).IsError);
        Assert.IsFalse((await CallAsync(host.PutFile, """{"server_id": "s1", "path": "/b.bin", "base64": "/wA="}""")).IsError);
        Assert.IsFalse((await CallAsync(host.PublishMessage, """{"server_id": "s1", "queue": "orders", "payload": "queued"}""")).IsError);
        Assert.IsFalse((await CallAsync(host.PublishMessage, """{"server_id": "s1", "topic": "t/1", "payload": "retained", "retain": true}""")).IsError);
        return host;
    }
}
