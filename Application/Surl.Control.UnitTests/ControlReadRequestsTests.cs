using static Surl.Control.ControlHarness;

namespace Surl.Control;

[TestClass]
public sealed class ControlReadRequestsTests
{
    private static readonly DateTimeOffset When = new(2026, 11, 15, 10, 30, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ListMailboxesAsync_AnOwner_ListsEachMailbox()
    {
        var target = new FakeControlTarget();
        target.Mailboxes.Add(new ControlMailbox("INBOX", 2, 1, 7));

        var response = await Serve(target, Request("GET", "/v1/mail/bob"));

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        Assert.AreEqual("{\"mailboxes\":[{\"name\":\"INBOX\",\"message_count\":2,\"unseen_count\":1,\"uid_validity\":7}]}", Body(response));
        CollectionAssert.AreEqual(new[] { "ListMailboxes bob" }, target.Calls);
    }

    [TestMethod]
    public async Task ListMailboxesAsync_AMailboxWithAUse_ListsTheUseAfterTheUidValidity()
    {
        var target = new FakeControlTarget();
        target.Mailboxes.Add(new ControlMailbox("Sent Items", 0, 0, 8, "sent"));

        var response = await Serve(target, Request("GET", "/v1/mail/bob"));

        Assert.AreEqual("{\"mailboxes\":[{\"name\":\"Sent Items\",\"message_count\":0,\"unseen_count\":0,\"uid_validity\":8,\"use\":\"sent\"}]}", Body(response));
    }

    [TestMethod]
    public async Task ListMessagesAsync_NoQuery_AsksForTheDefaultsAndListsEach()
    {
        var target = new FakeControlTarget { MessagePage = new([new(4, 120, ["\\Seen", "\\Flagged"], When)], true) };

        var response = await Serve(target, Request("GET", "/v1/mail/bob/INBOX"));

        Assert.AreEqual(
            "{\"messages\":[{\"uid\":4,\"size\":120,\"flags\":[\"\\\\Seen\",\"\\\\Flagged\"],\"internal_date\":\"2026-11-15T10:30:00.0000000\\u002B00:00\"}],\"has_more\":true}",
            Body(response));
        CollectionAssert.AreEqual(new[] { "ListMessages bob INBOX 0 100" }, target.Calls);
    }

    [TestMethod]
    public async Task ListMessagesAsync_AfterUidAndLimit_PassesThem()
    {
        var target = new FakeControlTarget();

        _ = await Serve(target, Request("GET", "/v1/mail/bob/Sent%20Items?after_uid=9&limit=5"));

        CollectionAssert.AreEqual(new[] { "ListMessages bob Sent Items 9 5" }, target.Calls);
    }

    [TestMethod]
    [DataRow("limit=0")]
    [DataRow("limit=1001")]
    [DataRow("limit=x")]
    [DataRow("after_uid=-1")]
    [DataRow("other=1")]
    public async Task ListMessagesAsync_ABadQuery_Is400(string query)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("GET", "/v1/mail/bob/INBOX?" + query));

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task ReadMessageAsync_NoAccept_AnswersTheBytesWithItsFlagsAndUid()
    {
        var target = new FakeControlTarget { Message = new(new(4, 12, ["\\Seen", "\\Draft"], When), "Subject: x\r\n"u8.ToArray()) };

        var response = await Serve(target, Request("GET", "/v1/mail/bob/INBOX/4"));

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        StringAssert.Contains(response, "Content-Type: message/rfc822\r\n");
        StringAssert.Contains(response, "Content-Length: 12\r\n");
        StringAssert.Contains(response, "X-Surl-Uid: 4\r\n");
        StringAssert.Contains(response, "X-Surl-Flags: \\Seen \\Draft\r\n");
        Assert.AreEqual("Subject: x\r\n", Body(response));
    }

    [TestMethod]
    public async Task ReadMessageAsync_AcceptJson_AnswersTheMessageAsJsonWithTextOrBase64()
    {
        var target = new FakeControlTarget { Message = new(new(4, 3, [], When), "hi\n"u8.ToArray()) };

        var text = await Serve(target, RequestWith("GET", "/v1/mail/bob/INBOX/4", $"Authorization: Bearer {Token}\r\nAccept: application/json\r\n"));
        target.Message = new(new(5, 2, [], When), new byte[] { 0, 255 });
        var binary = await Serve(target, RequestWith("GET", "/v1/mail/bob/INBOX/5", $"Authorization: Bearer {Token}\r\nAccept: application/json\r\n"));

        Assert.AreEqual("{\"uid\":4,\"size\":3,\"flags\":[],\"internal_date\":\"2026-11-15T10:30:00.0000000\\u002B00:00\",\"text\":\"hi\\n\"}", Body(text));
        Assert.AreEqual("{\"uid\":5,\"size\":2,\"flags\":[],\"internal_date\":\"2026-11-15T10:30:00.0000000\\u002B00:00\",\"base64\":\"AP8=\"}", Body(binary));
    }

    [TestMethod]
    public async Task ReadMessageAsync_AUidThatIsNotANumber_Is400()
    {
        var response = await Serve(new FakeControlTarget(), Request("GET", "/v1/mail/bob/INBOX/last"));

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"the UID must be a whole number\"}", Body(response));
    }

    [TestMethod]
    public async Task ReadFileOrListFolderAsync_AFile_AnswersItsBytesTypeAndModifiedTime()
    {
        var target = new FakeControlTarget { File = new("a,b\n"u8.ToArray(), 4, When, "text/csv") };

        var response = await Serve(target, Request("GET", "/v1/files/reports/a.csv"));

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        StringAssert.Contains(response, "Content-Type: text/csv\r\n");
        StringAssert.Contains(response, "Content-Length: 4\r\n");
        StringAssert.Contains(response, "Last-Modified: Sun, 15 Nov 2026 10:30:00 GMT\r\n");
        Assert.AreEqual("a,b\n", Body(response));
        CollectionAssert.AreEqual(new[] { "ReadFile /reports/a.csv" }, target.Calls);
    }

    [TestMethod]
    public async Task ReadFileOrListFolderAsync_AFileWithAQuery_Is400()
    {
        var response = await Serve(new FakeControlTarget(), Request("GET", "/v1/files/a.csv?limit=3"));

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
    }

    [TestMethod]
    [DataRow("/v1/files/reports/", "ListFolder /reports - 100")]
    [DataRow("/v1/files//", "ListFolder / - 100")]
    [DataRow("/v1/files/reports/?after_name=b&limit=2", "ListFolder /reports b 2")]
    public async Task ReadFileOrListFolderAsync_ATrailingSlash_ListsTheFolder(string requestTarget, string call)
    {
        var target = new FakeControlTarget { FolderPage = new([new("a.csv", false, 4), new("sub", true, null)], true) };

        var response = await Serve(target, Request("GET", requestTarget));

        Assert.AreEqual("{\"entries\":[{\"name\":\"a.csv\",\"kind\":\"file\",\"byte_count\":4},{\"name\":\"sub\",\"kind\":\"folder\"}],\"has_more\":true}", Body(response));
        CollectionAssert.AreEqual(new[] { call }, target.Calls);
    }

    [TestMethod]
    public async Task BrowseQueueAsync_ReadyMessages_ListsEachWithHeadersAndCounts()
    {
        var target = new FakeControlTarget
        {
            QueuePage = new(
                [
                    new(0, "one"u8.ToArray(), [new("k", "v")]),
                    new(1, new byte[] { 0, 255 }, []),
                ],
                5,
                2,
                true),
        };

        var response = await Serve(target, Request("GET", "/v1/queues/orders/messages?after_position=3&limit=2"));

        Assert.AreEqual(
            "{\"messages\":[{\"position\":0,\"text\":\"one\",\"headers\":[{\"name\":\"k\",\"value\":\"v\"}]},{\"position\":1,\"base64\":\"AP8=\",\"headers\":[]}],\"ready_count\":5,\"in_flight_count\":2,\"has_more\":true}",
            Body(response));
        CollectionAssert.AreEqual(new[] { "BrowseQueue orders 3 2" }, target.Calls);
    }

    [TestMethod]
    public async Task BrowseQueueAsync_NoQuery_StartsBeforeTheFirstPosition()
    {
        var target = new FakeControlTarget();

        _ = await Serve(target, Request("GET", "/v1/queues/orders/messages"));

        CollectionAssert.AreEqual(new[] { "BrowseQueue orders -1 100" }, target.Calls);
    }

    [TestMethod]
    public async Task ReadRetainedMessageAsync_ARetainedMessage_AnswersItsTopicQosAndPayload()
    {
        var target = new FakeControlTarget { Retained = new("sensors/a", 1, "21"u8.ToArray()) };

        var response = await Serve(target, Request("GET", "/v1/topics/sensors%2Fa/retained"));

        Assert.AreEqual("{\"topic\":\"sensors/a\",\"qos\":1,\"text\":\"21\"}", Body(response));
        CollectionAssert.AreEqual(new[] { "ReadRetained sensors/a" }, target.Calls);
    }

    [TestMethod]
    public async Task ReadLogAsync_Records_ListsEachWithOffsetTimestampAndKey()
    {
        var target = new FakeControlTarget
        {
            LogPage = new(
                [
                    new(3, When, "k"u8.ToArray(), "v"u8.ToArray()),
                    new(4, When, null, new byte[] { 0, 255 }),
                ],
                5,
                false),
        };

        var response = await Serve(target, Request("GET", "/v1/topics/t/log?from_offset=3&limit=2"));

        Assert.AreEqual(
            "{\"records\":[{\"offset\":3,\"timestamp\":\"2026-11-15T10:30:00.0000000\\u002B00:00\",\"key\":\"aw==\",\"text\":\"v\"},{\"offset\":4,\"timestamp\":\"2026-11-15T10:30:00.0000000\\u002B00:00\",\"base64\":\"AP8=\"}],\"next_offset\":5,\"has_more\":false}",
            Body(response));
        CollectionAssert.AreEqual(new[] { "ReadLog t 3 2" }, target.Calls);
    }

    [TestMethod]
    [DataRow("/v1/mail/bob")]
    [DataRow("/v1/mail/bob/INBOX")]
    [DataRow("/v1/mail/bob/INBOX/1")]
    [DataRow("/v1/files/a.csv")]
    [DataRow("/v1/files/dir/")]
    [DataRow("/v1/queues/q/messages")]
    [DataRow("/v1/topics/t/retained")]
    [DataRow("/v1/topics/t/log")]
    public async Task ReadRoutes_AnItemThatIsAbsent_Is404NotFound(string requestTarget)
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.NotFound, "nothing there") };

        var response = await Serve(target, Request("GET", requestTarget));

        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLine(response));
        Assert.AreEqual("{\"error\":\"not_found\",\"message\":\"nothing there\"}", Body(response));
    }

    [TestMethod]
    public async Task ReadRoutes_AnItemPastTheBound_Is413TooLarge()
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.TooLarge, "the file is 20000000 bytes") };

        var response = await Serve(target, Request("GET", "/v1/files/big.bin"));

        Assert.AreEqual("HTTP/1.1 413 Content Too Large", StatusLine(response));
        Assert.AreEqual("{\"error\":\"too_large\",\"message\":\"the file is 20000000 bytes\"}", Body(response));
    }

    [TestMethod]
    [DataRow("/v1/mail/bob")]
    [DataRow("/v1/mail/bob/INBOX")]
    [DataRow("/v1/mail/bob/INBOX/1")]
    [DataRow("/v1/files/a.csv")]
    [DataRow("/v1/files/dir/")]
    [DataRow("/v1/queues/q/messages")]
    [DataRow("/v1/topics/t/retained")]
    [DataRow("/v1/topics/t/log")]
    public async Task ReadRoutes_WithoutTheToken_Are401(string requestTarget)
    {
        var target = new FakeControlTarget();
        var time = new ManualTimeProvider();
        var serving = ServeAsync(Endpoint(target), RequestWith("GET", requestTarget, string.Empty), time, TestContext.CancellationToken);

        await time.WaitForTimerAsync(ControlEndpoint.RefusalDelay, TestContext.CancellationToken);
        time.Advance(ControlEndpoint.RefusalDelay);
        var response = await serving;

        Assert.AreEqual("HTTP/1.1 401 Unauthorized", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    private Task<string> Serve(FakeControlTarget target, string request) =>
        ServeAsync(Endpoint(target), request, new ManualTimeProvider(), TestContext.CancellationToken);

    [TestMethod]
    public async Task ReadKeyAsync_AKey_AnswersItsTypeSizeExpiryAndText()
    {
        var target = new FakeControlTarget
        {
            KeyContents = new(new ControlKeyInfo("user:1"u8.ToArray(), "string", 5, When), "alice"u8.ToArray()),
        };

        var response = await Serve(target, Request("GET", "/v1/keys/user:1?database=2"));

        Assert.AreEqual(
            "{\"key\":\"user:1\",\"type\":\"string\",\"byte_count\":5,\"expires_at\":\"2026-11-15T10:30:00.0000000\\u002B00:00\",\"text\":\"alice\"}",
            Body(response));
        CollectionAssert.AreEqual(new[] { "ReadKey 2 user:1" }, target.Calls);
    }

    [TestMethod]
    public async Task ReadKeyAsync_BinaryKeyAndValueAndAKeyOfAnotherType_AnswerBase64OrNoValue()
    {
        var target = new FakeControlTarget { KeyContents = new(new ControlKeyInfo(new byte[] { 255 }, "string", 1, null), new byte[] { 0 }) };
        var binary = await Serve(target, Request("GET", "/v1/keys/k"));
        target.KeyContents = new(new ControlKeyInfo("l"u8.ToArray(), "list", 3, null), null);
        var list = await Serve(target, Request("GET", "/v1/keys/l"));

        Assert.AreEqual("{\"key_base64\":\"/w==\",\"type\":\"string\",\"byte_count\":1,\"base64\":\"AA==\"}", Body(binary));
        Assert.AreEqual("{\"key\":\"l\",\"type\":\"list\",\"byte_count\":3}", Body(list));
    }

    [TestMethod]
    public async Task ListKeysAsync_NoQuery_AsksForEveryKeyWithTheDefaults()
    {
        var target = new FakeControlTarget { KeyPage = new([new ControlKeyInfo("a"u8.ToArray(), "hash", 9, null)], true) };

        var response = await Serve(target, Request("GET", "/v1/keys"));

        Assert.AreEqual("{\"keys\":[{\"key\":\"a\",\"type\":\"hash\",\"byte_count\":9}],\"has_more\":true}", Body(response));
        CollectionAssert.AreEqual(new[] { "ListKeys 0 *  100" }, target.Calls);
    }

    [TestMethod]
    public async Task ListKeysAsync_PatternAfterKeyLimitAndDatabase_PassesThem()
    {
        var target = new FakeControlTarget();

        _ = await Serve(target, Request("GET", "/v1/keys?pattern=user%3A*&after_key=user%3A1&limit=5&database=3"));

        CollectionAssert.AreEqual(new[] { "ListKeys 3 user:* user:1 5" }, target.Calls);
    }

    [TestMethod]
    [DataRow("/v1/keys?limit=0")]
    [DataRow("/v1/keys?database=16")]
    [DataRow("/v1/keys?other=1")]
    [DataRow("/v1/keys/k?limit=3")]
    public async Task KeyReads_ABadQuery_Is400(string path)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("GET", path));

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task ReadDirectoryAsync_AnswersTheLdifAsTextLdif()
    {
        var target = new FakeControlTarget { DirectoryLdif = "version: 1\n\ndn: o=x\no: café\n" };

        var response = await Serve(target, Request("GET", "/v1/directory"));

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        StringAssert.Contains(response, "Content-Type: text/ldif; charset=utf-8\r\n");
        Assert.AreEqual("version: 1\n\ndn: o=x\no: café\n", Body(response));
        CollectionAssert.AreEqual(new[] { "ReadDirectory" }, target.Calls);
    }

    [TestMethod]
    public async Task ReadDirectoryAsync_AQueryKey_Is400()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("GET", "/v1/directory?x=1"));

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task ListZonesAsync_AnswersTheApexesAsJson()
    {
        var target = new FakeControlTarget { ZoneApexes = ["a.test.", "b.test."] };

        var response = await Serve(target, Request("GET", "/v1/zones"));

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        Assert.AreEqual("""{"zones":["a.test.","b.test."]}""", Body(response));
        CollectionAssert.AreEqual(new[] { "ListZones" }, target.Calls);
    }

    [TestMethod]
    public async Task ReadZoneAsync_AnswersTheZoneFileAsTextDns()
    {
        var target = new FakeControlTarget { ZoneText = "a.test. 60 IN A 192.0.2.1\n" };

        var response = await Serve(target, Request("GET", "/v1/zones/a.test."));

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLine(response));
        StringAssert.Contains(response, "Content-Type: text/dns; charset=utf-8\r\n");
        Assert.AreEqual("a.test. 60 IN A 192.0.2.1\n", Body(response));
        CollectionAssert.AreEqual(new[] { "ReadZone a.test." }, target.Calls);
    }

    [TestMethod]
    public async Task ZoneReads_AQueryKey_Is400()
    {
        var target = new FakeControlTarget();

        var list = await Serve(target, Request("GET", "/v1/zones?x=1"));
        var read = await Serve(target, Request("GET", "/v1/zones/a.test.?x=1"));

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(list));
        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(read));
        Assert.IsEmpty(target.Calls);
    }
}
