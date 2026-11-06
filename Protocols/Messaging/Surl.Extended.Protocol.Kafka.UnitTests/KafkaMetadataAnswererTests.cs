using System.Net;
using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Metadata v0 to v8 over the shared bus (BL-518; ADR-0105 decision 2), every byte written by
/// hand from the Kafka protocol guide's Metadata schemas.
/// </summary>
[TestClass]
public sealed class KafkaMetadataAnswererTests
{
    private static readonly byte[] NullArray = [0xFF, 0xFF, 0xFF, 0xFF];

    // brokers: one, node 0, host "127.0.0.1", port 9092.
    private static readonly byte[] BrokerZero = [0, 0, 0, 1, 0, 0, 0, 0, .. String("127.0.0.1"), 0, 0, 0x23, 0x84];

    // partition_index 0, leader 0, replicas [0], isr [0], after error_code 0.
    private static readonly byte[] PartitionZero = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0];

    private static readonly byte[] OnePartition = [0, 0, 0, 1, .. PartitionZero];

    private static readonly byte[] NoPartitions = [0, 0, 0, 0];

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullBus_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaMetadataAnswerer(null!));

    [TestMethod]
    public void ClusterId_Is22Base64UrlCharactersAndDiffersPerAnswerer()
    {
        var first = EmptyBusMetadata().ClusterId;

        Assert.AreEqual(22, first.Length);
        Assert.IsTrue(first.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.AreNotEqual(first, EmptyBusMetadata().ClusterId);
        Assert.AreEqual(KafkaApiKey.Metadata, EmptyBusMetadata().ApiKey);
    }

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = EmptyBusMetadata();
        var request = new KafkaRequest(new KafkaRequestHeader(3, 0, 1, null), KafkaServedApis.Ranges[3], new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_MetadataFourForAllTopics_ListsEveryTopicInOrdinalOrderOfItsKafkaName()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.Publish(new BusMessage("z", Encoding.UTF8.GetBytes("1")));
        bus.Publish(new BusMessage("a/b", Encoding.UTF8.GetBytes("2")));
        bus.Publish(new BusMessage("not kafka", Encoding.UTF8.GetBytes("3")));
        var metadata = new KafkaMetadataAnswerer(bus);

        var written = await ServeAnonymousAsync(metadata, Request(KafkaApiKey.Metadata, 4, [.. NullArray, 0x01]));

        byte[] body =
        [
            0, 0, 0, 0,
            .. BrokerZero, 0xFF, 0xFF,
            .. String(metadata.ClusterId),
            0, 0, 0, 0,
            0, 0, 0, 2,
            0, 0, .. String("a.b"), 0x00, .. OnePartition,
            0, 0, .. String("z"), 0x00, .. OnePartition,
        ];
        CollectionAssert.AreEqual(Response(body), written);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataFourForAMissingTopicWithoutAutoCreation_IsUnknownTopicOrPartition()
    {
        var metadata = EmptyBusMetadata();

        var written = await ServeAnonymousAsync(metadata, Request(KafkaApiKey.Metadata, 4, [0, 0, 0, 1, .. String("orders"), 0x00]));

        byte[] body = [0, 0, 0, 0, .. BrokerZero, 0xFF, 0xFF, .. String(metadata.ClusterId), 0, 0, 0, 0, 0, 0, 0, 1, 0, 3, .. String("orders"), 0x00, .. NoPartitions];
        CollectionAssert.AreEqual(Response(body), written);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataFourForAMissingTopicWithAutoCreation_CreatesItWithPartitionZero()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var metadata = new KafkaMetadataAnswerer(bus);

        var written = await ServeAnonymousAsync(metadata, Request(KafkaApiKey.Metadata, 4, [0, 0, 0, 1, .. String("shop.orders"), 0x01]));

        byte[] body = [0, 0, 0, 0, .. BrokerZero, 0xFF, 0xFF, .. String(metadata.ClusterId), 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, .. String("shop.orders"), 0x00, .. OnePartition];
        CollectionAssert.AreEqual(Response(body), written);
        CollectionAssert.AreEqual(new[] { "shop/orders" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_MetadataFourForAnExistingTopicWithoutAutoCreation_IsAnswered()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.CreateLog("orders");
        var metadata = new KafkaMetadataAnswerer(bus);

        var written = await ServeAnonymousAsync(metadata, Request(KafkaApiKey.Metadata, 4, [0, 0, 0, 1, .. String("orders"), 0x00]));

        byte[] body = [0, 0, 0, 0, .. BrokerZero, 0xFF, 0xFF, .. String(metadata.ClusterId), 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, .. String("orders"), 0x00, .. OnePartition];
        CollectionAssert.AreEqual(Response(body), written);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataZeroWithAnEmptyArray_ListsEveryTopicAndCreatesNamedOnes()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.CreateLog("t");
        var metadata = new KafkaMetadataAnswerer(bus);

        var written = await ServeAnonymousAsync(metadata, Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]), Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 1, .. String("new")]));

        byte[] all = [.. BrokerZero, 0, 0, 0, 1, 0, 0, .. String("t"), .. OnePartition];
        byte[] created = [.. BrokerZero, 0, 0, 0, 1, 0, 0, .. String("new"), .. OnePartition];
        CollectionAssert.AreEqual(new byte[][] { Response(all), Response(created) }.SelectMany(bytes => bytes).ToArray(), written);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataOneWithAnEmptyArray_ListsNoTopic()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.CreateLog("t");

        var written = await ServeAnonymousAsync(new KafkaMetadataAnswerer(bus), Request(KafkaApiKey.Metadata, 1, [0, 0, 0, 0]));

        CollectionAssert.AreEqual(Response([.. BrokerZero, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0]), written);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataEightAskingForOperations_AnswersTheCallersRights()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, configuration: new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("r", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe), new TopicAccessGrant("w", TopicAccessGrant.Everyone, TopicAccessRights.Publish)],
        });
        var metadata = new KafkaMetadataAnswerer(bus);

        var written = await ServeAnonymousAsync(metadata, Request(KafkaApiKey.Metadata, 8, [0, 0, 0, 2, .. String("r"), .. String("w"), 0x01, 0x01, 0x01]));

        // A v8 partition: leader_epoch 0 after the leader, offline_replicas [] at the end.
        byte[] partition =
        [
            0, 0, 0, 1,
            0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 1, 0, 0, 0, 0,
            0, 0, 0, 1, 0, 0, 0, 0,
            0, 0, 0, 0,
        ];
        byte[] body =
        [
            0, 0, 0, 0,
            .. BrokerZero, 0xFF, 0xFF,
            .. String(metadata.ClusterId),
            0, 0, 0, 0,
            0, 0, 0, 2,
            0, 0, .. String("r"), 0x00, .. partition, 0, 0, 0x01, 0x08,
            0, 0, .. String("w"), 0x00, .. partition, 0, 0, 0x01, 0x10,
            0, 0, 0x01, 0x00,
        ];
        CollectionAssert.AreEqual(Response(body), written);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataEightNotAskingForOperations_AnswersNotRequested()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.CreateLog("t");
        var metadata = new KafkaMetadataAnswerer(bus);

        var written = await ServeAnonymousAsync(metadata, Request(KafkaApiKey.Metadata, 8, [.. NullArray, 0x01, 0x00, 0x00]));

        byte[] partition = [0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .. new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, .. new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, 0, 0, 0, 0];
        byte[] body =
        [
            0, 0, 0, 0,
            .. BrokerZero, 0xFF, 0xFF,
            .. String(metadata.ClusterId),
            0, 0, 0, 0,
            0, 0, 0, 1,
            0, 0, .. String("t"), 0x00, .. partition, 0x80, 0, 0, 0,
            0x80, 0, 0, 0,
        ];
        CollectionAssert.AreEqual(Response(body), written);
    }

    [TestMethod]
    public async Task ServeAsync_TopicTheCallerMayNotUse_IsTopicAuthorizationFailedAndLeftOutOfTheListing()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, configuration: new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("open", TopicAccessGrant.Everyone, TopicAccessRights.PublishAndSubscribe)],
        });
        bus.CreateLog("open");
        bus.CreateLog("secret");
        var metadata = new KafkaMetadataAnswerer(bus);

        var written = await ServeAnonymousAsync(
            metadata,
            Request(KafkaApiKey.Metadata, 1, [0, 0, 0, 2, .. String("secret"), .. String("absent")]),
            Request(KafkaApiKey.Metadata, 1, NullArray));

        byte[] named = [.. BrokerZero, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 2, 0, 29, .. String("secret"), 0x00, .. NoPartitions, 0, 29, .. String("absent"), 0x00, .. NoPartitions];
        byte[] listed = [.. BrokerZero, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, .. String("open"), 0x00, .. OnePartition];
        CollectionAssert.AreEqual(new byte[][] { Response(named), Response(listed) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "open", "secret" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_InvalidTopicName_IsInvalidTopicException()
    {
        var written = await ServeAnonymousAsync(EmptyBusMetadata(), Request(KafkaApiKey.Metadata, 1, [0, 0, 0, 1, .. String("no/slash")]));

        CollectionAssert.AreEqual(Response([.. BrokerZero, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 1, 0, 17, .. String("no/slash"), 0x00, .. NoPartitions]), written);
    }

    [TestMethod]
    public async Task ServeAsync_AutoCreationPastTheBussMostTopics_IsPolicyViolation()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, maxTopics: 1);
        bus.CreateLog("full");

        var written = await ServeAnonymousAsync(new KafkaMetadataAnswerer(bus), Request(KafkaApiKey.Metadata, 1, [0, 0, 0, 1, .. String("more")]));

        CollectionAssert.AreEqual(Response([.. BrokerZero, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 1, 0, 44, .. String("more"), 0x00, .. NoPartitions]), written);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataBeforeALogin_ClosesWithNoBytesAndAsksThePolicyOnce()
    {
        var policy = new UnitTestKafkaAuthenticationPolicy(false, "PLAIN");

        var (written, log) = await ServeAsync(policy, EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.Metadata, 4, [.. NullArray, 0x01]));

        Assert.IsEmpty(written);
        Assert.AreEqual("Metadata came before a login; closed with no reply.", log.Notes.Single());
        Assert.AreEqual(1, policy.AnonymousChecks);
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousAllowed_ServesMetadataAndAsksThePolicyOnceAConnection()
    {
        var policy = new UnitTestKafkaAuthenticationPolicy(true, "PLAIN");

        var (written, _) = await ServeAsync(policy, EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]), Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]));

        CollectionAssert.AreEqual(Response([.. BrokerZero, 0, 0, 0, 0]).Concat(Response([.. BrokerZero, 0, 0, 0, 0])).ToArray(), written);
        Assert.AreEqual(1, policy.AnonymousChecks);
    }

    [TestMethod]
    [DataRow("10.0.0.5", "0.0.0.0", "10.0.0.5")]
    [DataRow("::ffff:10.0.0.5", "::", "10.0.0.5")]
    [DataRow("fe80::1", "::", "fe80::1")]
    [DataRow("10.0.0.5", "192.168.1.1", "192.168.1.1")]
    [DataRow("10.0.0.5", "kafka.example", "kafka.example")]
    public void AdvertisedHost_WildcardListener_IsTheConnectionsLocalAddress(string local, string listenHost, string expected)
    {
        var context = Context(TestContext.CancellationToken, listenUrl: new ListenUrl("kafka", listenHost, 9092), localEndPoint: new IPEndPoint(IPAddress.Parse(local), 9092));

        Assert.AreEqual(expected, KafkaMetadataAnswerer.AdvertisedHost(context));
    }

    [TestMethod]
    public void AdvertisedHost_WildcardListenerWithoutAnIpLocalEndPoint_IsTheListenHost()
    {
        var context = Context(TestContext.CancellationToken, listenUrl: new ListenUrl("kafka", "0.0.0.0", 9092), localEndPoint: new DnsEndPoint("local", 9092));

        Assert.AreEqual("0.0.0.0", KafkaMetadataAnswerer.AdvertisedHost(context));
    }

    [TestMethod]
    public async Task ServeAsync_ListenerNotYetBound_AdvertisesTheListenPort()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0])]);
        var context = Context(TestContext.CancellationToken, log, new ListenUrl("kafka", "127.0.0.1", 19092));

        await new KafkaBroker(new AnonymousAuthenticationPolicy(), [EmptyBusMetadata()]).ServeAsync(connection, context);

        CollectionAssert.AreEqual(Response([0, 0, 0, 1, 0, 0, 0, 0, .. String("127.0.0.1"), 0, 0, 0x4A, 0x94, 0, 0, 0, 0]), connection.WrittenBytes);
    }

    private async Task<byte[]> ServeAnonymousAsync(KafkaMetadataAnswerer metadata, params byte[][] requests) =>
        (await ServeAsync(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), metadata, TestContext.CancellationToken, requests)).Written;
}
