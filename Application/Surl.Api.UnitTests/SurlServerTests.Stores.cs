using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-810: seeding a running server's mail, files, messages and accounts, and resetting, snapshotting and restoring
// them (ADR-0124, decision 3), each proved by a fake client of the protocol that reads the store.
public sealed partial class SurlServerTests
{
    private const string Pop3 = "pop3://127.0.0.1:0";
    private const string Mqtt = "mqtt://127.0.0.1:0";
    private const string SeededMail = "Subject: seeded\r\n\r\nseeded for alice\r\n";

    // MQTT 3.1.1 CONNECT (clean session, client "c", user "alice", password "secret"), SUBSCRIBE packet 1 to "t/1"
    // at QoS 0, DISCONNECT (MQTT 3.1.1 sections 3.1, 3.8 and 3.14).
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

    [TestMethod]
    public async Task Mail_DeliverAsync_TheSeededMessageIsFetchedOverPop3()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Pop3), seams.Host, TestContext.CancellationToken);

        await server.Mail.DeliverAsync("alice@x", Encoding.ASCII.GetBytes(SeededMail), TestContext.CancellationToken);
        var written = await ServeAsync(seams, "pop3", "USER alice\r\nPASS secret\r\nRETR 1\r\nQUIT\r\n");

        StringAssert.Contains(written, "seeded for alice\r\n.\r\n");
    }

    [TestMethod]
    public async Task Files_PutAsync_TheSeededFileIsFetchedOverHttp()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ApiTestSeams.Options(Http), seams.Host, TestContext.CancellationToken);

        await server.Files.PutAsync("/a.txt", "seeded file"u8.ToArray(), TestContext.CancellationToken);
        var written = await ServeAsync(seams, "http", HttpGet("/a.txt", null));

        StringAssert.StartsWith(written, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(written, "seeded file");
    }

    [TestMethod]
    public async Task Files_DeleteAsync_TheDeletedFileIsNotFoundOverHttpAndDeletingItAgainThrowsAbsent()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ApiTestSeams.Options(Http), seams.Host, TestContext.CancellationToken);
        await server.Files.PutAsync("/a.txt", "seeded file"u8.ToArray(), TestContext.CancellationToken);

        await server.Files.DeleteAsync("/a.txt");
        var written = await ServeAsync(seams, "http", HttpGet("/a.txt", null));
        var again = await Assert.ThrowsExactlyAsync<SurlSeedRefusedException>(() => server.Files.DeleteAsync("/a.txt"));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Files.DeleteAsync(null!));

        StringAssert.StartsWith(written, "HTTP/1.1 404 ");
        Assert.AreEqual(StoreKind.Content, again.Store);
        Assert.AreEqual("Absent", again.Refusal);
    }

    [TestMethod]
    public async Task Messages_PublishAsyncRetained_IsDeliveredToAnMqttSubscriber()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Mqtt), seams.Host, TestContext.CancellationToken);

        await server.Messages.PublishAsync("t/1", "hi"u8.ToArray(), retain: true, TestContext.CancellationToken);
        var written = await ServeBytesAsync(seams, "mqtt", MqttSubscribeToT1AsAlice);

        Assert.IsTrue(Contains(written, RetainedHiOnT1), Convert.ToHexString(written));
    }

    [TestMethod]
    public async Task Accounts_AddThenRemove_TheAccountLogsInOverHttpBasicOnlyWhileItIsThere()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Http), seams.Host, TestContext.CancellationToken);

        var before = await ServeAsync(seams, "http", HttpGet("/hello.txt", "bob:builder"));
        server.Accounts.Add("bob", "builder");
        var added = await ServeAsync(seams, "http", HttpGet("/hello.txt", "bob:builder"));
        var removed = server.Accounts.Remove("bob");
        var after = await ServeAsync(seams, "http", HttpGet("/hello.txt", "bob:builder"));

        StringAssert.StartsWith(before, "HTTP/1.1 401 ");
        StringAssert.StartsWith(added, "HTTP/1.1 200 OK\r\n");
        Assert.IsTrue(removed);
        StringAssert.StartsWith(after, "HTTP/1.1 401 ");
        Assert.IsFalse(server.Accounts.Remove("bob"));
    }

    [TestMethod]
    public async Task ResetAsync_AfterSeedingAllFour_RemovesThemAndKeepsTheConfiguredMailboxStartFilesAndAccounts()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Pop3, Http, Mqtt), seams.Host, TestContext.CancellationToken);
        await SeedAllFourAsync(server);

        await server.ResetAsync(TestContext.CancellationToken);
        var pop3 = await ServeAsync(seams, "pop3", "USER alice\r\nPASS secret\r\nSTAT\r\nQUIT\r\n");
        var seededFile = await ServeAsync(seams, "http", HttpGet("/a.txt", "alice:secret"));
        var startFile = await ServeAsync(seams, "http", HttpGet("/hello.txt", "alice:secret"));
        var addedAccount = await ServeAsync(seams, "http", HttpGet("/hello.txt", "bob:builder"));
        var mqtt = await ServeBytesAsync(seams, "mqtt", MqttSubscribeToT1AsAlice);

        StringAssert.Contains(pop3, "+OK 0 0\r\n");
        StringAssert.StartsWith(seededFile, "HTTP/1.1 404 ");
        StringAssert.EndsWith(startFile, "hello");
        StringAssert.StartsWith(addedAccount, "HTTP/1.1 401 ");
        Assert.IsFalse(Contains(mqtt, RetainedHiOnT1), Convert.ToHexString(mqtt));
    }

    [TestMethod]
    public async Task RestoreAsync_SnapshotChangeRestore_GivesBackTheSnapshotsState()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Pop3, Http, Mqtt), seams.Host, TestContext.CancellationToken);
        await SeedAllFourAsync(server);

        var snapshot = await server.SnapshotAsync(TestContext.CancellationToken);
        await server.ResetAsync(TestContext.CancellationToken);
        await server.Files.PutAsync("/b.txt", "after the snapshot"u8.ToArray(), TestContext.CancellationToken);
        server.Accounts.Add("carol", "c");
        await server.RestoreAsync(snapshot, TestContext.CancellationToken);
        var pop3 = await ServeAsync(seams, "pop3", "USER alice\r\nPASS secret\r\nRETR 1\r\nQUIT\r\n");
        var seededFile = await ServeAsync(seams, "http", HttpGet("/a.txt", "bob:builder"));
        var laterFile = await ServeAsync(seams, "http", HttpGet("/b.txt", "alice:secret"));
        var laterAccount = await ServeAsync(seams, "http", HttpGet("/hello.txt", "carol:c"));
        var mqtt = await ServeBytesAsync(seams, "mqtt", MqttSubscribeToT1AsAlice);

        Assert.IsGreaterThan(0L, snapshot.ByteCount);
        StringAssert.Contains(pop3, "seeded for alice\r\n.\r\n");
        StringAssert.EndsWith(seededFile, "seeded file");
        StringAssert.StartsWith(laterFile, "HTTP/1.1 404 ");
        StringAssert.StartsWith(laterAccount, "HTTP/1.1 401 ");
        Assert.IsTrue(Contains(mqtt, RetainedHiOnT1), Convert.ToHexString(mqtt));
    }

    [TestMethod]
    public async Task Seeding_PublishesSeededOnTheEventStreamSoAWaitSeesIt()
    {
        var seams = new ApiTestSeams();
        var options = StoreOptions(Http);
        options.RecordExchanges = true;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        await server.Files.PutAsync("/a.txt", "seeded file"u8.ToArray(), TestContext.CancellationToken);
        var seeded = (StoreChangedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Content }, ExchangeTimeLimit, TestContext.CancellationToken);

        Assert.AreEqual(StoreChange.Seeded, seeded.Change);
    }

    [TestMethod]
    [DataRow("mail-recipient", "Mail", "NoSuchAccount", DisplayName = "mail for an unknown mailbox")]
    [DataRow("mail-size", "Mail", "MessageTooLarge", DisplayName = "mail past the size limit")]
    [DataRow("file-path", "Content", "NotPermitted", DisplayName = "a file at a .surl path")]
    [DataRow("file-size", "Content", "TooLarge", DisplayName = "a file past the size limit")]
    [DataRow("topic", "Bus", "InvalidTopic", DisplayName = "a publish on a wildcard topic")]
    [DataRow("queue", "Bus", "UnknownQueue", DisplayName = "a message for a queue that does not exist")]
    public async Task Seeding_WhatAPeerWouldBeRefused_ThrowsSurlSeedRefusedExceptionNamingTheStoreAndTheRefusal(
        string seed, string store, string refusal)
    {
        var seams = new ApiTestSeams();
        var options = StoreOptions(Http);
        options.MaxFileSize = 64;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var tooLarge = new byte[65];
        Func<Task> seeding = seed switch
        {
            "mail-recipient" => () => server.Mail.DeliverAsync("nobody@x", Encoding.ASCII.GetBytes(SeededMail), TestContext.CancellationToken),
            "mail-size" => () => server.Mail.DeliverAsync("alice@x", tooLarge, TestContext.CancellationToken),
            "file-path" => () => server.Files.PutAsync("/.surl/a.txt", "x"u8.ToArray(), TestContext.CancellationToken),
            "file-size" => () => server.Files.PutAsync("/a.txt", tooLarge, TestContext.CancellationToken),
            "topic" => () => server.Messages.PublishAsync("t/#", "x"u8.ToArray(), cancellationToken: TestContext.CancellationToken),
            _ => () => server.Messages.EnqueueAsync("no-such-queue", "x"u8.ToArray(), TestContext.CancellationToken),
        };

        var exception = await Assert.ThrowsExactlyAsync<SurlSeedRefusedException>(seeding);

        Assert.AreEqual(Enum.Parse<StoreKind>(store), exception.Store);
        Assert.AreEqual(refusal, exception.Refusal);
        StringAssert.EndsWith(exception.Message, $" was refused: {refusal}.");
    }

    [TestMethod]
    public async Task Accounts_AddMisused_ThrowsArgumentException()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Http), seams.Host, TestContext.CancellationToken);

        var colon = Assert.ThrowsExactly<ArgumentException>(() => server.Accounts.Add("a:b", "pw"));
        Assert.ThrowsExactly<ArgumentException>(() => server.Accounts.Add("alice", "again"));

        Assert.AreEqual("userName", colon.ParamName);
    }

    [TestMethod]
    public async Task RestoreAsync_ASnapshotFromAnotherServer_ThrowsArgumentException()
    {
        var seams = new ApiTestSeams();
        await using var first = await SurlServer.StartAsync(ApiTestSeams.Options(Http), seams.Host, TestContext.CancellationToken);
        await using var second = await SurlServer.StartAsync(ApiTestSeams.Options(Http), seams.Host, TestContext.CancellationToken);
        var snapshot = await first.SnapshotAsync(TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => second.RestoreAsync(snapshot, TestContext.CancellationToken));

        Assert.AreEqual("snapshot", exception.ParamName);
    }

    [TestMethod]
    public async Task ResetAsync_ADataDirectory_ThrowsInvalidOperationExceptionAndSnapshotDoesToo()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.DataDirectory = "served";
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        var reset = await Assert.ThrowsAsync<InvalidOperationException>(() => server.ResetAsync(TestContext.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.SnapshotAsync(TestContext.CancellationToken));

        StringAssert.StartsWith(reset.Message, "Content: ");
    }

    [TestMethod]
    public async Task Stores_NullArguments_ThrowArgumentNullException()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ApiTestSeams.Options(Http), seams.Host, TestContext.CancellationToken);
        var token = TestContext.CancellationToken;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Mail.DeliverAsync(null!, default, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Files.PutAsync(null!, default, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Messages.PublishAsync(null!, default, cancellationToken: token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Messages.EnqueueAsync(null!, default, token));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.Accounts.Add(null!, "pw"));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.Accounts.Add("bob", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.Accounts.Remove(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.RestoreAsync(null!, token));
    }

    // Accounts alice:secret over plain text, hello.txt from the start files, on each of listenUrls.
    private static SurlServerOptions StoreOptions(params string[] listenUrls)
    {
        var options = ApiTestSeams.Options(listenUrls);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.AllowPlaintextAuthentication = true;
        options.Files["hello.txt"] = "hello"u8.ToArray();
        return options;
    }

    private static string HttpGet(string path, string? userColonPassword)
    {
        var authorization = userColonPassword is null
            ? string.Empty
            : $"Authorization: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(userColonPassword))}\r\n";
        return $"GET {path} HTTP/1.1\r\nHost: 127.0.0.1:{FakeListenerFactory.BoundPort}\r\n{authorization}Connection: close\r\n\r\n";
    }

    private async Task SeedAllFourAsync(SurlServer server)
    {
        await server.Mail.DeliverAsync("alice@x", Encoding.ASCII.GetBytes(SeededMail), TestContext.CancellationToken);
        await server.Files.PutAsync("/a.txt", "seeded file"u8.ToArray(), TestContext.CancellationToken);
        await server.Messages.PublishAsync("t/1", "hi"u8.ToArray(), retain: true, TestContext.CancellationToken);
        server.Accounts.Add("bob", "builder");
    }

    private async Task<string> ServeAsync(ApiTestSeams seams, string scheme, string clientText) =>
        Encoding.ASCII.GetString(await ServeBytesAsync(seams, scheme, Encoding.ASCII.GetBytes(clientText)));

    // Brings one fake client in on the listener for scheme and waits until its exchange has ended.
    private async Task<byte[]> ServeBytesAsync(ApiTestSeams seams, string scheme, byte[] clientBytes)
    {
        var connection = new FakeConnection(clientBytes);
        seams.ListenerFactory.Accept(scheme, connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return connection.WrittenToClient;
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;
}
