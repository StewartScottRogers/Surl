using System.Text;

namespace Surl.Api;

// BL-1199: reading back the mail store, the content store and the message bus (ADR-0164, decision 5).
public sealed partial class SurlServerTests
{
    private static SurlServerOptions ReadBackOptions()
    {
        var options = StoreOptions(Http);
        options.Queues.Add("orders");
        return options;
    }

    private async Task<SurlServer> StartSeededForReadBackAsync()
    {
        var server = await SurlServer.StartAsync(ReadBackOptions(), new ApiTestSeams().Host, TestContext.CancellationToken);
        var token = TestContext.CancellationToken;
        await server.Mail.DeliverAsync("alice@x", Encoding.ASCII.GetBytes(SeededMail), token);
        await server.Files.PutAsync("/reports/a.txt", "file bytes"u8.ToArray(), token);
        await server.Messages.EnqueueAsync("orders", "queued"u8.ToArray(), token);
        await server.Messages.PublishAsync("t/1", "retained"u8.ToArray(), retain: true, token);
        return server;
    }

    [TestMethod]
    public async Task Mail_ReadBack_GivesTheMailboxesTheMessageListAndTheExactBytes()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        var boxes = await server.Mail.ListMailboxesAsync("alice", token);
        var page = await server.Mail.ListMessagesAsync("alice", "inbox", 0, 100, token);
        var message = await server.Mail.ReadMessageAsync("alice", "INBOX", page.Messages[0].Uid, token);
        var afterIt = await server.Mail.ListMessagesAsync("alice", "INBOX", page.Messages[0].Uid, 1, token);

        Assert.AreEqual("INBOX", boxes.Single().Name);
        Assert.AreEqual(1, boxes.Single().UnseenCount);
        Assert.IsFalse(page.HasMore);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(SeededMail), message.Bytes.ToArray());
        Assert.AreEqual(SeededMail.Length, message.Summary.Size);
        Assert.AreEqual(0, message.Summary.Flags.Count);
        Assert.AreEqual(0, afterIt.Messages.Count);
    }

    [TestMethod]
    public async Task Mail_ReadBack_TheSeenFlagIsNamedAndReadingDoesNotSetIt()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        _ = await server.Mail.ReadMessageAsync("alice", "INBOX", 1, token);
        var boxes = await server.Mail.ListMailboxesAsync("alice", token);

        Assert.AreEqual(1, boxes.Single().UnseenCount);
    }

    [TestMethod]
    public async Task Mail_ReadBack_NotFoundAndRefusedCases()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Mail.ListMailboxesAsync("nobody", token));
        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Mail.ListMessagesAsync("alice", "Nope", 0, 100, token));
        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Mail.ReadMessageAsync("alice", "INBOX", 99, token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Mail.ListMessagesAsync("alice", "INBOX", 0, 0, token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Mail.ListMessagesAsync("alice", "INBOX", -1, 10, token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Mail.ReadMessageAsync("alice", "INBOX", uint.MaxValue + 1L, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Mail.ListMailboxesAsync(null!, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Mail.ListMessagesAsync(null!, "INBOX", 0, 1, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Mail.ListMessagesAsync("alice", null!, 0, 1, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Mail.ReadMessageAsync(null!, "INBOX", 1, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Mail.ReadMessageAsync("alice", null!, 1, token));
    }

    [TestMethod]
    public async Task Mail_ReadBack_FlagsAreNamedInImapSpelling()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;
        var seams = new ApiTestSeams();
        await using var imap = await SurlServer.StartAsync(StoreOptions("imap://127.0.0.1:0"), seams.Host, token);
        await imap.Mail.DeliverAsync("alice@x", Encoding.ASCII.GetBytes(SeededMail), token);
        _ = await ServeAsync(seams, "imap", "a LOGIN alice secret\r\nb SELECT INBOX\r\nc STORE 1 +FLAGS (\\Seen \\Answered \\Flagged \\Deleted \\Draft)\r\nd LOGOUT\r\n");

        var message = await imap.Mail.ReadMessageAsync("alice", "INBOX", 1, token);

        CollectionAssert.AreEqual(
            new[] { "\\Seen", "\\Answered", "\\Flagged", "\\Deleted", "\\Draft" },
            message.Summary.Flags.ToArray());
    }

    [TestMethod]
    public async Task Files_AShareClosedByShareAccess_IsListedAndReadAsTheServersOwner()
    {
        var options = StoreOptions(Http);
        options.Shares.Add("team=team");
        options.ShareAccess.Add("team=alice:rw");
        var shareFiles = new Surl.Content.InMemoryContentFileSystem(TimeProvider.System);
        shareFiles.CreateDirectory(Path.GetFullPath("team"));
        var host = new ApiTestSeams().Host with { DataDirectoryFileSystem = shareFiles };
        await using var server = await SurlServer.StartAsync(options, host, TestContext.CancellationToken);
        var token = TestContext.CancellationToken;
        await server.Files.PutAsync("/team/a.txt", "inside"u8.ToArray(), token);

        var listing = await server.Files.ListAsync("/team", null, 100, token);
        var file = await server.Files.ReadAsync("/team/a.txt", token);

        Assert.AreEqual("a.txt", listing.Entries.Single().Name);
        CollectionAssert.AreEqual("inside"u8.ToArray(), file.Bytes.ToArray());
    }

    [TestMethod]
    public async Task Files_ReadBack_GivesTheExactBytesAndTheListing()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        var file = await server.Files.ReadAsync("/reports/a.txt", token);
        var folder = await server.Files.ListAsync("/", null, 100, token);
        var reports = await server.Files.ListAsync("/reports", null, 100, token);

        CollectionAssert.AreEqual("file bytes"u8.ToArray(), file.Bytes.ToArray());
        Assert.AreEqual(10, file.ByteCount);
        Assert.AreEqual("text/plain", file.ContentType.Split(';')[0]);
        CollectionAssert.AreEqual(new[] { "hello.txt", "reports" }, folder.Entries.Select(entry => entry.Name).ToArray());
        Assert.IsTrue(folder.Entries.Single(entry => entry.Name == "reports").IsFolder);
        Assert.AreEqual(5, folder.Entries.Single(entry => entry.Name == "hello.txt").ByteCount);
        Assert.AreEqual("a.txt", reports.Entries.Single().Name);
        Assert.IsFalse(reports.HasMore);
    }

    [TestMethod]
    public async Task Files_ReadBack_NotFoundAndRefusedCases()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Files.ReadAsync("/missing.txt", token));
        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Files.ListAsync("/missing", null, 100, token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Files.ReadAsync("/../x", token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Files.ListAsync("/", null, 0, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Files.ReadAsync(null!, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Files.ListAsync(null!, null, 1, token));
    }

    [TestMethod]
    public async Task Messages_ReadBack_GivesTheQueuedMessageTheRetainedMessageAndTheLogRecordWithoutConsuming()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        var queue = await server.Messages.BrowseQueueAsync("orders", -1, 100, token);
        var again = await server.Messages.BrowseQueueAsync("orders", -1, 100, token);
        var retained = await server.Messages.ReadRetainedAsync("t/1", token);
        var log = await server.Messages.ReadLogAsync("t/1", 0, 100, token);

        CollectionAssert.AreEqual("queued"u8.ToArray(), queue.Messages.Single().Payload.ToArray());
        Assert.AreEqual(0, queue.Messages.Single().Position);
        Assert.AreEqual(1, queue.ReadyCount);
        Assert.AreEqual(0, queue.InFlightCount);
        Assert.AreEqual(1, again.Messages.Count);
        Assert.IsNotNull(retained);
        Assert.AreEqual("t/1", retained.Topic);
        Assert.AreEqual(0, retained.QualityOfService);
        CollectionAssert.AreEqual("retained"u8.ToArray(), retained.Payload.ToArray());
        Assert.AreEqual(0, log.Records.Single().Offset);
        CollectionAssert.AreEqual("retained"u8.ToArray(), log.Records.Single().Payload.ToArray());
        Assert.AreEqual(1, log.NextOffset);
        Assert.IsFalse(log.HasMore);
    }

    [TestMethod]
    public async Task Messages_BrowseQueueAsync_AMessageSentWithAHeader_CarriesTheHeaderNameAndValue()
    {
        var seams = new ApiTestSeams();
        var options = StoreOptions(Stomp);
        options.AreExtensionsTurnedOn = true;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        await ServeAsync(seams, "stomp", StompConnect + "SEND\ndestination:/queue/q1\nx-order:42\n\nwith header\0");
        var page = await server.Messages.BrowseQueueAsync("q1", -1, 100, TestContext.CancellationToken);

        var message = page.Messages.Single();
        CollectionAssert.AreEqual("with header"u8.ToArray(), message.Payload.ToArray());
        Assert.IsTrue(message.Headers.Contains(KeyValuePair.Create("x-order", "42")), string.Join(",", message.Headers));
    }

    [TestMethod]
    public async Task Messages_ReadBack_NotFoundAndRefusedCases()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        Assert.IsNull(await server.Messages.ReadRetainedAsync("no/such", token));
        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Messages.BrowseQueueAsync("nope", -1, 100, token));
        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Messages.ReadLogAsync("no/such", 0, 100, token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Messages.BrowseQueueAsync("orders", -1, 0, token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Messages.ReadLogAsync("t/1", 0, 5000, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Messages.BrowseQueueAsync(null!, -1, 1, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Messages.ReadRetainedAsync(null!, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Messages.ReadLogAsync(null!, 0, 1, token));
    }

    [TestMethod]
    public async Task ReadBack_AfterResetAsync_FindsNothing()
    {
        await using var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;

        await server.ResetAsync(token);

        Assert.AreEqual(0, (await server.Mail.ListMessagesAsync("alice", "INBOX", 0, 100, token)).Messages.Count);
        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Files.ReadAsync("/reports/a.txt", token));
        Assert.AreEqual(0, (await server.Messages.BrowseQueueAsync("orders", -1, 100, token)).Messages.Count);
        Assert.IsNull(await server.Messages.ReadRetainedAsync("t/1", token));
    }

    [TestMethod]
    public async Task ReadBack_AfterStopAsync_ThrowsObjectDisposedException()
    {
        var server = await StartSeededForReadBackAsync();
        var token = TestContext.CancellationToken;
        _ = await server.StopAsync(token);

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Mail.ListMailboxesAsync("alice", token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Mail.ListMessagesAsync("alice", "INBOX", 0, 1, token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Mail.ReadMessageAsync("alice", "INBOX", 1, token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Files.ReadAsync("/a.txt", token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Files.ListAsync("/", null, 1, token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Messages.BrowseQueueAsync("orders", -1, 1, token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Messages.ReadRetainedAsync("t/1", token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Messages.ReadLogAsync("t/1", 0, 1, token));
    }
}
