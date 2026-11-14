using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using Surl.Extended.Protocol.Kafka;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Mqtt;

namespace Surl.Composition;

/// <summary>
/// The Kafka broker the extended servers compose for <c>kafka</c> and <c>kafkas</c> (ADR-0105, BL-973): over the one
/// message bus the MQTT server shares, switched off with the other extended servers, advertising its own bound port.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    // Metadata v1 with a null topic array (every topic), correlation id 7 and a null client id.
    private static readonly byte[] MetadataV1AllTopicsRequest =
    [
        0, 0, 0, 14,
        0, 3, 0, 1, 0, 0, 0, 7, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
    ];

    [TestMethod]
    public void ComposeKafkaBroker_ThisBuild_AnswersMetadataOverTheOneBusTheMqttServerIsBuiltOver()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var broker = ExtendedProtocolServers.Compose(services).OfType<KafkaBroker>().Single();
        var mqttServer = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, services.ComposedServices)
            .OfType<MqttProtocolServer>().Single();

        var answerers = ComposedServerFields.Single<System.Collections.Frozen.FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer>>(broker);
        var metadataBus = ComposedServerFields.Single<MessageBus.MessageBus>(answerers[KafkaApiKey.Metadata]);
        Assert.AreSame(services.ComposedServices.MessageBus, metadataBus);
        Assert.AreSame(metadataBus, ComposedServerFields.Single<MessageBus.MessageBus>(mqttServer));
    }

    [TestMethod]
    public void ComposeKafkaBroker_ThisBuild_GivesProduceAndInitProducerIdTheOneProducerStates()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var broker = ExtendedProtocolServers.Compose(services).OfType<KafkaBroker>().Single();

        var answerers = ComposedServerFields.Single<System.Collections.Frozen.FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer>>(broker);
        Assert.AreSame(
            AnyField<KafkaProducerStates>(answerers[KafkaApiKey.Produce]),
            AnyField<KafkaProducerStates>(answerers[KafkaApiKey.InitProducerId]));
    }

    [TestMethod]
    public void ComposeKafkaBroker_OneInstance_GivesEveryGroupAnswererTheOneCoordinatorForKafkaAndKafkas()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var broker = ExtendedProtocolServers.Compose(services).OfType<KafkaBroker>().Single();

        var answerers = ComposedServerFields.Single<System.Collections.Frozen.FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer>>(broker);
        var coordinators = new[]
        {
            KafkaApiKey.JoinGroup, KafkaApiKey.SyncGroup, KafkaApiKey.Heartbeat, KafkaApiKey.LeaveGroup,
            KafkaApiKey.OffsetCommit, KafkaApiKey.DescribeGroups, KafkaApiKey.ListGroups,
        }.Select(key => AnyField<KafkaGroupCoordinator>(answerers[key])).ToArray();
        Assert.IsTrue(coordinators.All(coordinator => ReferenceEquals(coordinator, coordinators[0])));
        CollectionAssert.AreEqual(new[] { "kafka", "kafkas" }, broker.Schemes.ToArray());
    }

    [TestMethod]
    public void ComposeKafkaBroker_TwoInstances_DoNotShareACoordinatorOrProducerStates()
    {
        var first = ExtendedProtocolServers.ComposeKafkaBroker(SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System).ComposedServices);
        var second = ExtendedProtocolServers.ComposeKafkaBroker(SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System).ComposedServices);

        var firstAnswerers = ComposedServerFields.Single<System.Collections.Frozen.FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer>>(first);
        var secondAnswerers = ComposedServerFields.Single<System.Collections.Frozen.FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer>>(second);
        Assert.AreNotSame(
            AnyField<KafkaGroupCoordinator>(firstAnswerers[KafkaApiKey.JoinGroup]),
            AnyField<KafkaGroupCoordinator>(secondAnswerers[KafkaApiKey.JoinGroup]));
        Assert.AreNotSame(
            AnyField<KafkaProducerStates>(firstAnswerers[KafkaApiKey.InitProducerId]),
            AnyField<KafkaProducerStates>(secondAnswerers[KafkaApiKey.InitProducerId]));
    }

    [TestMethod]
    public async Task ComposeAsync_KafkaApiVersions_ListsTheRangesTheDecisionGives()
    {
        // ApiVersions v0: header v1 (api key 18, version 0, correlation id 9, null client id), no body.
        byte[] request = [0, 0, 0, 10, 0, 18, 0, 0, 0, 0, 0, 9, 0xFF, 0xFF];
        var connection = new FakeConnection(request);
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["kafka"] = Task.FromResult(connection) },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithKafka("--allow-anonymous", "kafka://127.0.0.1:0/"), seams.Seams with { AreExtensionsTurnedOn = true }, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await connection.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        var response = connection.WrittenBytes;
        Assert.AreEqual(9, BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(4)));
        Assert.AreEqual(0, BinaryPrimitives.ReadInt16BigEndian(response.AsSpan(8)));
        var count = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(10));
        Assert.AreEqual(KafkaServedApis.Ranges.Count, count);
        var served = Enumerable.Range(0, count).ToDictionary(
            row => BinaryPrimitives.ReadInt16BigEndian(response.AsSpan(14 + (row * 6))),
            row => (Min: BinaryPrimitives.ReadInt16BigEndian(response.AsSpan(16 + (row * 6))), Max: BinaryPrimitives.ReadInt16BigEndian(response.AsSpan(18 + (row * 6)))));
        Assert.AreEqual((0, 5), served[(short)KafkaApiKey.JoinGroup]);
        Assert.AreEqual((2, 4), served[(short)KafkaApiKey.CreateTopics]);
        Assert.AreEqual((0, 1), served[(short)KafkaApiKey.InitProducerId]);
        Assert.AreEqual((2, 7), served[(short)KafkaApiKey.OffsetCommit]);
        Assert.AreEqual((1, 5), served[(short)KafkaApiKey.OffsetFetch]);
    }

    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchOnOrOff_ComposesTheKafkaBrokerForKafkaAndKafkasOrNone()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var on = SurlServerComposition.ComposeExtendedProtocolServers(true, null, services);
        var off = SurlServerComposition.ComposeExtendedProtocolServers(false, null, services);

        CollectionAssert.AreEqual(new[] { "kafka", "kafkas" }, on.OfType<KafkaBroker>().Single().Schemes.ToArray());
        Assert.IsFalse(off.Any(server => server.Schemes.Contains("kafka") || server.Schemes.Contains("kafkas")));
        Assert.AreEqual(1, ExtendedProtocolServers.Registrations.Count(registration => registration.Schemes.Contains("kafka")));
        Assert.IsTrue(ExtendedProtocolServers.Registrations.Any(registration => registration.Schemes.Contains("kafkas")));
    }

    [TestMethod]
    public async Task ComposeAsync_KafkaOnPortZero_AdvertisesTheBoundPortInMetadata()
    {
        var connection = new FakeConnection(MetadataV1AllTopicsRequest);
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["kafka"] = Task.FromResult(connection) },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithKafka("--allow-anonymous", "kafka://127.0.0.1:0/"), seams.Seams with { AreExtensionsTurnedOn = true }, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await connection.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        var response = connection.WrittenBytes;
        Assert.AreEqual(7, BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(4)));
        Assert.AreEqual(1, BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(8)));
        var hostLength = BinaryPrimitives.ReadInt16BigEndian(response.AsSpan(16));
        Assert.AreEqual("127.0.0.1", Encoding.ASCII.GetString(response, 18, hostLength));
        Assert.AreEqual(FakeListenerFactory.BoundPort, BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(18 + hostLength)));
    }

    [TestMethod]
    public async Task ComposeAsync_KafkasWithoutACertificate_IsRefusedWithTheMissingCertificateExitCode()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithKafka("--allow-anonymous", "kafkas://127.0.0.1:0/"), new StartTestSeams().Seams with { AreExtensionsTurnedOn = true }, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
    }

    [TestMethod]
    [DataRow("--kafka-fetch-compression", "zstd", KafkaCompressionCodec.Zstd)]
    [DataRow("--kafka-fetch-compression", "gzip", KafkaCompressionCodec.Gzip)]
    [DataRow("--allow-anonymous", "", KafkaCompressionCodec.None)]
    public async Task ComposeAsync_KafkaFetchCompression_ReachesTheBrokersFetchAnswerer(string option, string value, KafkaCompressionCodec expected)
    {
        KafkaFetchAnswerer? fetchAnswerer = null;
        var seams = new StartTestSeams().Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = services =>
            {
                var broker = ExtendedProtocolServers.ComposeKafkaBroker(services.ComposedServices);
                var answerers = ComposedServerFields.Single<System.Collections.Frozen.FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer>>(broker);
                fetchAnswerer = (KafkaFetchAnswerer)answerers[KafkaApiKey.Fetch];
                return [broker];
            },
        };
        var arguments = value.Length == 0
            ? new[] { option, "kafka://127.0.0.1:0/" }
            : [option, value, "--allow-anonymous", "kafka://127.0.0.1:0/"];

        var start = await SurlServerComposition.ComposeAsync(StartTestSeams.ParseWithKafka(arguments), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        var servedCompression = fetchAnswerer!.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(KafkaCompressionCodec))
            .GetValue(fetchAnswerer);
        Assert.AreEqual(expected, servedCompression);
    }

    private static T AnyField<T>(object owner)
        where T : class =>
        (T)owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(T))
            .GetValue(owner)!;
}
