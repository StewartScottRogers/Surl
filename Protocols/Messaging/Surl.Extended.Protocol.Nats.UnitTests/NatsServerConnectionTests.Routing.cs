using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>
/// PUB, HPUB, SUB, UNSUB, MSG and HMSG through the shared bus (BL-526; ADR-0113 decisions 4 and 7),
/// every client line and every expected answer written by hand from the NATS client protocol page,
/// the subjects page, the "Queue Groups" page and the headers ADR.
/// </summary>
public sealed partial class NatsServerConnectionTests
{
    private const string AliceLogin = "\"user\":\"alice\",\"pass\":\"secret\"";

    [TestMethod]
    public async Task ServeAsync_PubWithTwoSubscribers_IsDeliveredAsMsgToEach()
    {
        var server = NewServer();
        var first = await OpenSubscribedAsync(server, Connect(), "SUB foo 1\r\n");
        var second = await OpenSubscribedAsync(server, Connect(), "SUB foo 9\r\n");
        var publisher = Open(server, Connect());

        publisher.Connection.Send("PUB foo inbox.1 5\r\nhe\r\nl\r\n");

        await ExpectAnswersAsync(first, "PONG\r\nMSG foo 1 inbox.1 5\r\nhe\r\nl\r\n");
        await ExpectAnswersAsync(second, "PONG\r\nMSG foo 9 inbox.1 5\r\nhe\r\nl\r\n");
        await CloseAsync(first, second, publisher);
    }

    [TestMethod]
    public async Task ServeAsync_StarAndFullWildcards_MatchOneTokenAndOneOrMore()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect() + "SUB foo.* 1\r\nSUB foo.> 2\r\nPUB foo.bar 1\r\na\r\nPUB foo.bar.baz 1\r\nb\r\nPUB foo 1\r\nc\r\nPING\r\n");

        Assert.AreEqual("MSG foo.bar 1 1\r\na\r\nMSG foo.bar 2 1\r\na\r\nMSG foo.bar.baz 2 1\r\nb\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_QueueGroupOfTwo_GivesEachMemberOneOfTwoMessages()
    {
        var server = NewServer();
        var first = await OpenSubscribedAsync(server, Connect(), "SUB work workers 1\r\n");
        var second = await OpenSubscribedAsync(server, Connect(), "SUB work workers 1\r\n");
        var publisher = Open(server, Connect());

        publisher.Connection.Send("PUB work 1\r\na\r\nPUB work 1\r\nb\r\nPING\r\n");

        await ExpectAnswersAsync(publisher, "PONG\r\n");
        await ExpectAnswersAsync(first, "PONG\r\nMSG work 1 1\r\na\r\n");
        await ExpectAnswersAsync(second, "PONG\r\nMSG work 1 1\r\nb\r\n");
        await CloseAsync(first, second, publisher);
    }

    [TestMethod]
    public async Task ServeAsync_UnsubWithMaxTwo_EndsAfterTwoMessages()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect() + "SUB foo 1\r\nUNSUB 1 2\r\nPUB foo 1\r\na\r\nPUB foo 1\r\nb\r\nPUB foo 1\r\nc\r\nPING\r\n");

        Assert.AreEqual("MSG foo 1 1\r\na\r\nMSG foo 1 1\r\nb\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_UnsubWithMaxAlreadyReached_EndsAtOnceAndTheSidMayBeReused()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect() + "SUB foo 1\r\nPUB foo 1\r\na\r\nUNSUB 1 1\r\nPUB foo 1\r\nb\r\nSUB bar 1\r\nPUB bar 1\r\nc\r\nPING\r\n");

        Assert.AreEqual("MSG foo 1 1\r\na\r\nMSG bar 1 1\r\nc\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_UnsubWithoutMaxAndUnknownSidUnderVerbose_EndsAtOnceAndAnswersOk()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect("\"verbose\":true,") + "SUB foo 1\r\nUNSUB 1\r\nUNSUB 7\r\nPUB foo 1\r\na\r\nPING\r\n");

        Assert.AreEqual("+OK\r\n+OK\r\n+OK\r\n+OK\r\n+OK\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_SubReusingALiveSid_IsIgnored()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect("\"verbose\":true,") + "SUB foo 1\r\nSUB bar 1\r\nPUB bar 1\r\nb\r\nPUB foo 1\r\nf\r\n");

        Assert.AreEqual("+OK\r\n+OK\r\n+OK\r\n+OK\r\nMSG foo 1 1\r\nf\r\n+OK\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_EchoFalse_SuppressesSelfDeliveryButNotOthers()
    {
        var server = NewServer();
        var subscriber = await OpenSubscribedAsync(server, Connect("\"echo\":false,"), "SUB foo 1\r\n");
        var other = Open(server, Connect());

        subscriber.Connection.Send("PUB foo 1\r\na\r\nPING\r\n");
        await ExpectAnswersAsync(subscriber, "PONG\r\nPONG\r\n");
        other.Connection.Send("PUB foo 1\r\nb\r\n");

        await ExpectAnswersAsync(subscriber, "PONG\r\nPONG\r\nMSG foo 1 1\r\nb\r\n");
        await CloseAsync(subscriber, other);
    }

    [TestMethod]
    public async Task ServeAsync_Hpub_IsDeliveredAsHmsgWithItsHeaderBlockByteForByte()
    {
        const string Block = "NATS/1.0\r\nX-Trace:  abc \r\nx-trace: 2\r\n\r\n";
        var server = NewServer();
        var withHeaders = await OpenSubscribedAsync(server, Connect("\"headers\":true,"), "SUB foo 1\r\n");
        var withoutHeaders = await OpenSubscribedAsync(server, Connect(), "SUB foo 2\r\n");
        var publisher = Open(server, Connect("\"headers\":true,"));

        publisher.Connection.Send($"HPUB foo reply {Block.Length} {Block.Length + 5}\r\n{Block}hello\r\n");

        await ExpectAnswersAsync(withHeaders, $"PONG\r\nHMSG foo 1 reply {Block.Length} {Block.Length + 5}\r\n{Block}hello\r\n");
        await ExpectAnswersAsync(withoutHeaders, "PONG\r\nMSG foo 2 reply 5\r\nhello\r\n");
        await CloseAsync(withHeaders, withoutHeaders, publisher);
    }

    [TestMethod]
    public async Task ServeAsync_HpubHeaders_ReachOtherProtocolsAsBusHeaders()
    {
        const string Block = "NATS/1.0\r\nX-Trace:  abc \r\n\r\n";
        var bus = UnitTestMessageBus.Create();
        var received = new List<BusMessage>();
        using var mqtt = bus.Connect();
        _ = mqtt.Subscribe("foo", received.Add);

        _ = await ServeAsync(bus, Connect("\"headers\":true,") + $"HPUB foo {Block.Length} {Block.Length + 2}\r\n{Block}hi\r\nPING\r\n");

        Assert.HasCount(1, received);
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("X-Trace", "abc") }, received[0].Headers.ToArray());
        Assert.AreEqual("hi", Encoding.UTF8.GetString(received[0].Payload.Span));
    }

    [TestMethod]
    public async Task ServeAsync_HpubWithoutHeaders_IsUnknownProtocolOperationAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), Connect() + "HPUB foo 12 12\r\nNATS/1.0\r\n\r\n\r\nPING\r\n");

        Assert.AreEqual("-ERR 'Unknown Protocol Operation'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_HpubWithAnEmptyHeaderBlock_IsDeliveredAsMsg()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect("\"headers\":true,") + "SUB foo 1\r\nHPUB foo 0 2\r\nhi\r\nPING\r\n");

        Assert.AreEqual("MSG foo 1 2\r\nhi\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_HeadersFromAnotherProtocol_ReachAHeadersSubscriberAsANatsBlock()
    {
        var bus = UnitTestMessageBus.Create();
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), bus);
        var subscriber = await OpenSubscribedAsync(server, Connect("\"headers\":true,"), "SUB foo 1\r\nSUB bar 2\r\n");

        _ = bus.Publish(new BusMessage("foo", "x"u8.ToArray()) { Headers = [new("Trace", "1"), new("bad:name", "2"), new("Span", "a b")] });
        _ = bus.Publish(new BusMessage("bar", "y"u8.ToArray()) { Headers = [new("Broken", "a\r\nb")] });

        await ExpectAnswersAsync(subscriber, "PONG\r\nHMSG foo 1 33 34\r\nNATS/1.0\r\nTrace: 1\r\nSpan: a b\r\n\r\nx\r\nMSG bar 2 1\r\ny\r\n");
        await CloseAsync(subscriber);
    }

    [TestMethod]
    public async Task ServeAsync_RequestWithNoSubscriberUnderNoResponders_IsAnswered503()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect("\"headers\":true,\"no_responders\":true,") + "SUB _INBOX.x 1\r\nSUB _INBOX.* 2\r\nSUB other 3\r\nPUB service _INBOX.x 2\r\nhi\r\nPING\r\n");

        Assert.AreEqual("HMSG _INBOX.x 1 16 16\r\nNATS/1.0 503\r\n\r\n\r\nHMSG _INBOX.x 2 16 16\r\nNATS/1.0 503\r\n\r\n\r\nPONG\r\n", answers);
    }

    [TestMethod]
    [DataRow("\"headers\":true,", "PUB service _INBOX.x 2\r\nhi\r\n")]
    [DataRow("\"headers\":true,\"no_responders\":true,", "PUB service 2\r\nhi\r\n")]
    [DataRow("\"headers\":true,\"no_responders\":true,", "SUB service 2\r\nPUB service _INBOX.x 2\r\nhi\r\n")]
    public async Task ServeAsync_RequestWithoutNoRespondersOrReplyToOrWithASubscriber_GetsNo503(string options, string operations)
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect(options) + "SUB _INBOX.x 1\r\n" + operations + "PING\r\n");

        Assert.DoesNotContain("503", answers);
        Assert.EndsWith("PONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_PayloadOverMaxPayload_IsMaximumPayloadViolationAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), Connect() + "PUB foo 1048577\r\nPING\r\n");

        Assert.AreEqual("-ERR 'Maximum Payload Violation'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_HpubTotalOverMaxPayload_IsMaximumPayloadViolationAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), Connect("\"headers\":true,") + "HPUB foo 12 1048577\r\nPING\r\n");

        Assert.AreEqual("-ERR 'Maximum Payload Violation'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_SlowConsumer_IsCutOffWithSlowConsumer()
    {
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create(), new NatsServerOptions { PendingBytesLimit = 1000 });
        var subscriber = await OpenSubscribedAsync(server, Connect(), "SUB foo 1\r\n", new UnitTestNatsClientConnection { HoldWritesAfter = 2 });
        var publisher = Open(server, Connect());

        // The first MSG (616 bytes) is held by the transport; the second takes the unwritten bytes past 1000.
        publisher.Connection.Send($"PUB foo 600\r\n{new string('a', 600)}\r\n");
        await WaitUntilAsync(() => subscriber.Connection.WriteAttempts == 3);
        publisher.Connection.Send($"PUB foo 600\r\n{new string('b', 600)}\r\nPUB foo 600\r\n{new string('c', 600)}\r\nPING\r\n");

        await subscriber.Serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        Assert.AreEqual("PONG\r\n-ERR 'Slow Consumer'\r\n", Answers(subscriber.Connection.Written));
        CollectionAssert.Contains(subscriber.Log.Notes.ToList(), "More than 1000 bytes waited unwritten; answered -ERR 'Slow Consumer' and closed.");
        await ExpectAnswersAsync(publisher, "PONG\r\n");
        await CloseAsync(publisher);
    }

    [TestMethod]
    public async Task ServeAsync_SlowConsumerWhoseErrorWriteFails_StillCloses()
    {
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create(), new NatsServerOptions { PendingBytesLimit = 1000 });
        var subscriber = await OpenSubscribedAsync(server, Connect(), "SUB foo 1\r\n", new UnitTestNatsClientConnection { FailWritesAfter = 2 });

        subscriber.Connection.Send($"PUB foo 1001\r\n{new string('a', 1001)}\r\n");

        await subscriber.Serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        Assert.AreEqual("PONG\r\n", Answers(subscriber.Connection.Written));
        CollectionAssert.Contains(subscriber.Log.Notes.ToList(), "The slow consumer's -ERR was not written (IOException).");
    }

    [TestMethod]
    public async Task ServeAsync_SlowConsumerWhoseTransportStaysStuck_GivesUpOnTheErrorAfterFiveSeconds()
    {
        var clock = new ManualTimeProvider();
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create(), new NatsServerOptions { PendingBytesLimit = 1000 });
        var subscriber = await OpenSubscribedAsync(server, Connect(), "SUB foo 1\r\n", new UnitTestNatsClientConnection { HoldWritesAfter = 2, KeepsHoldingAfterCancel = true }, clock);

        subscriber.Connection.Send($"PUB foo 1001\r\n{new string('a', 1001)}\r\n");
        await WaitUntilAsync(() => subscriber.Log.Notes.Any(note => note.StartsWith("More than", StringComparison.Ordinal)) && clock.PendingTimerCount == 1);
        clock.Advance(NatsServerOptions.SlowConsumerErrorTimeout);

        await subscriber.Serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        CollectionAssert.Contains(subscriber.Log.Notes.ToList(), "The slow consumer's -ERR was not written (OperationCanceledException).");
    }

    [TestMethod]
    public async Task ServeAsync_MessagePublishedUnderAnMqttName_IsDeliveredAsMsgOnTheMappedSubject()
    {
        var bus = UnitTestMessageBus.Create();
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), bus);
        var subscriber = await OpenSubscribedAsync(server, Connect(), "SUB sensors.* 1\r\n");
        var topic = bus.Names.ToCanonicalTopic("mqtt", "sensors/temp");

        _ = bus.Publish(new BusMessage(topic.Name!, "21"u8.ToArray()) { ResponseTopic = "replies/temp" });
        _ = bus.Publish(new BusMessage("sensors/a.b", "x"u8.ToArray()));
        _ = bus.Publish(new BusMessage("sensors/hum", "40"u8.ToArray()) { ResponseTopic = "replies/a.b" });

        await ExpectAnswersAsync(subscriber, "PONG\r\nMSG sensors.temp 1 replies.temp 2\r\n21\r\nMSG sensors.hum 1 2\r\n40\r\n");
        await CloseAsync(subscriber);
    }

    [TestMethod]
    public async Task ServeAsync_Pub_ReachesAnMqttSubscriberOnTheMappedTopic()
    {
        var bus = UnitTestMessageBus.Create();
        var received = new List<BusMessage>();
        using var mqtt = bus.Connect();
        _ = mqtt.Subscribe("sensors/+", received.Add);

        _ = await ServeAsync(bus, Connect() + "PUB sensors.temp replies.temp 2\r\n21\r\nPING\r\n");

        Assert.HasCount(1, received);
        Assert.AreEqual("sensors/temp", received[0].Topic);
        Assert.AreEqual("replies/temp", received[0].ResponseTopic);
        Assert.IsNull(received[0].NativeForm);
    }

    [TestMethod]
    public async Task ServeAsync_PubAndSubRefusedByTopicAccess_AreAnsweredPermissionsViolationAndKept()
    {
        var bus = new Surl.MessageBus.MessageBus(
            TimeProvider.System,
            configuration: new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("open/#", "alice", TopicAccessRights.PublishAndSubscribe)] });

        var answers = await ServeAsync(bus, Connect() + "PUB secret 1\r\na\r\nSUB secret.> 1\r\nSUB open.> 2\r\nPUB open.x 1\r\nb\r\nPING\r\n");

        Assert.AreEqual(
            "-ERR 'Permissions Violation for Publish to secret'\r\n-ERR 'Permissions Violation for Subscription to secret.>'\r\nMSG open.x 2 1\r\nb\r\nPONG\r\n",
            answers);
    }

    [TestMethod]
    [DataRow("SUB foo..bar 1\r\n")]
    [DataRow("SUB foo.>.bar 1\r\n")]
    [DataRow("SUB foo* 1\r\n")]
    [DataRow("SUB foo.b> 1\r\n")]
    [DataRow("SUB foo/bar 1\r\n")]
    public async Task ServeAsync_InvalidSubSubject_IsInvalidSubjectAndKept(string subscribe)
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), Connect() + subscribe + "PING\r\n");

        Assert.AreEqual("-ERR 'Invalid Subject'\r\nPONG\r\n", answers);
    }

    [TestMethod]
    [DataRow("PUB foo.* 1\r\na\r\n")]
    [DataRow("PUB foo.> 1\r\na\r\n")]
    [DataRow("PUB foo..bar 1\r\na\r\n")]
    [DataRow("PUB foo+ 1\r\na\r\n")]
    [DataRow("PUB foo reply.* 1\r\na\r\n")]
    public async Task ServeAsync_InvalidPublishSubjectOrReplyTo_IsInvalidPublishSubjectAndKept(string publish)
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), Connect() + "SUB > 1\r\n" + publish + "PING\r\n");

        Assert.AreEqual("-ERR 'Invalid Publish Subject'\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_SubPastTheBusBound_IsMaximumSubscriptionsExceededAndKept()
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxSubscriptionsPerConnection: 1);

        var answers = await ServeAsync(bus, Connect() + "SUB foo 1\r\nSUB bar 2\r\nPING\r\n");

        Assert.AreEqual("-ERR 'Maximum Subscriptions Exceeded'\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_PubPastTheBusStore_IsResourceLimitExceededAndClosed()
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxTopics: 1);

        var answers = await ServeAsync(bus, Connect() + "PUB foo 1\r\na\r\nPUB bar 1\r\nb\r\nPING\r\n");

        Assert.AreEqual("-ERR 'Resource Limit Exceeded'\r\n", answers);
    }

    [TestMethod]
    [DataRow("PUB foo\r\n")]
    [DataRow("PUB foo a b 1\r\n")]
    [DataRow("PUB foo x\r\n")]
    [DataRow("PUB foo -1\r\n")]
    [DataRow("HPUB foo 3\r\n")]
    [DataRow("HPUB foo 5 4\r\n")]
    [DataRow("SUB foo\r\n")]
    [DataRow("SUB foo q 1 x\r\n")]
    [DataRow("UNSUB\r\n")]
    [DataRow("UNSUB 1 2 3\r\n")]
    [DataRow("UNSUB 1 x\r\n")]
    [DataRow("PUB foo 1\r\nab\r\n")]
    public async Task ServeAsync_MalformedArgumentsOrPayloadEnd_IsParserErrorAndClosed(string operation)
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), Connect("\"headers\":true,") + operation + "PING\r\n");

        Assert.AreEqual("-ERR 'Parser Error'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesInsideAPayload_PublishesNothing()
    {
        var bus = UnitTestMessageBus.Create();
        var received = new List<BusMessage>();
        using var mqtt = bus.Connect();
        _ = mqtt.Subscribe("#", received.Add);

        var answers = await ServeAsync(bus, Connect() + "PUB foo 10\r\nabc");

        Assert.AreEqual(string.Empty, answers);
        Assert.IsEmpty(received);
    }

    [TestMethod]
    public async Task ServeAsync_VerbosePubAndSub_AreAnsweredOkAfterTheirEffect()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            Connect("\"verbose\":true,") + "SUB foo 1\r\nPUB foo 1\r\na\r\nPUB foo.* 1\r\nb\r\n");

        Assert.AreEqual("+OK\r\n+OK\r\nMSG foo 1 1\r\na\r\n+OK\r\n-ERR 'Invalid Publish Subject'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_InfoWriteFails_StopsReadingBeforeConnectAndThrows()
    {
        var connection = new UnitTestNatsClientConnection { FailWritesAfter = 0 };

        var serving = NewServer().ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<IOException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
    }

    private static string Connect(string options = "") => $"CONNECT {{{options}{AliceLogin}}}\r\n";

    private static NatsServer NewServer() => new(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create());

    private async Task<string> ServeAsync(Surl.MessageBus.MessageBus bus, string client)
    {
        var connection = new InMemoryConnection([Encoding.UTF8.GetBytes(client)], new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 4222));

        await new NatsServer(new UnitTestNatsAuthenticationPolicy(), bus).ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: TestContext.CancellationToken));

        return AfterInfo(Encoding.UTF8.GetString(connection.WrittenBytes));
    }

    private OpenClient Open(NatsServer server, string connect, UnitTestNatsClientConnection? connection = null, ManualTimeProvider? clock = null)
    {
        connection ??= new UnitTestNatsClientConnection();
        var log = new RecordingExchangeLog();
        var serving = server.ServeAsync(connection, Context(clock ?? new ManualTimeProvider(), log, TestContext.CancellationToken));
        connection.Send(connect);
        return new OpenClient(connection, serving, log);
    }

    // Opens a client and subscribes, waiting for the PONG that shows its subscriptions are made.
    private async Task<OpenClient> OpenSubscribedAsync(
        NatsServer server, string connect, string subscribe, UnitTestNatsClientConnection? connection = null, ManualTimeProvider? clock = null)
    {
        var client = Open(server, connect, connection, clock);
        client.Connection.Send(subscribe + "PING\r\n");
        await ExpectAnswersAsync(client, "PONG\r\n");
        return client;
    }

    private async Task ExpectAnswersAsync(OpenClient client, string expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (Answers(client.Connection.Written) != expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5, TestContext.CancellationToken);
        }

        Assert.AreEqual(expected, Answers(client.Connection.Written));
    }

    private async Task CloseAsync(params OpenClient[] clients)
    {
        foreach (var client in clients)
        {
            client.Connection.Close();
            await client.Serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        }
    }

    // What the server wrote after its INFO line; empty before INFO is complete.
    private static string Answers(string written)
    {
        var infoEnd = written.IndexOf("\r\n", StringComparison.Ordinal);
        return infoEnd < 0 ? string.Empty : written[(infoEnd + 2)..];
    }

    private sealed record OpenClient(UnitTestNatsClientConnection Connection, Task Serving, RecordingExchangeLog Log);
}
