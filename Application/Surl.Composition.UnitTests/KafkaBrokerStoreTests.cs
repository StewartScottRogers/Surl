using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text;
using Surl.Cli;
using Surl.Core;
using Surl.Extended.Protocol.Kafka;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The Kafka broker's producer states and consumer groups under whole-instance reset, snapshot and restore
/// (ADR-0159 decisions 2 and 3; BL-1190), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class KafkaBrokerStoreTests
{
    private const string ListenUrl = "kafka://127.0.0.1:0";

    // JoinGroup v3 (api key 11) for group g, an empty member id, protocol type consumer and one protocol, range.
    private static readonly byte[] JoinGroupV3 = Frame(
        [0, 11, 0, 3, 0, 0, 0, 1, 0, 1, (byte)'c'],
        [0, 1, (byte)'g', 0, 0, 0x27, 0x10, 0, 0, 0x27, 0x10, 0, 0, 0, 8, .. "consumer"u8, 0, 0, 0, 1, 0, 5, .. "range"u8, 0, 0, 0, 1, (byte)'m']);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ResetAsync_ClearsTheProducerWindowAndDropsTheGroupAfterAProduceAndAJoinOverKafka()
    {
        var (serving, factory, states) = await ComposeAsync();
        await using var running = serving;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = running.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        KafkaProducedBatch batch = new(1, 0, 0, 0);
        states.Record("t", [batch], [0]);
        var member = MemberOf(await SendAsync(factory, JoinGroupV3));
        Assert.AreEqual(0, ErrorOf(await SendAsync(factory, HeartbeatV0(member))));
        Assert.IsTrue(states.Check("t", [batch]).IsDuplicate);

        await running.Stores.ResetAsync(TestContext.CancellationToken);
        var heartbeat = await SendAsync(factory, HeartbeatV0(member));
        await stop.CancelAsync();
        await serve;

        Assert.AreEqual(25, ErrorOf(heartbeat));
        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("t", [batch]));
    }

    [TestMethod]
    public async Task Stores_SnapshotResetRestore_GivesBackTheProducerWindowAndPublishesOneChangeEach()
    {
        var (serving, _, states) = await ComposeAsync();
        await using var running = serving;
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());
        KafkaProducedBatch batch = new(1, 0, 0, 0);
        states.Record("t", [batch], [4]);

        var snapshot = await running.Stores.TakeSnapshotAsync(StoreKind.Producers, TestContext.CancellationToken);
        await running.Stores.ResetAsync(StoreKind.Producers, TestContext.CancellationToken);
        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("t", [batch]));
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<StoreChangedEvent> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            changes.Add((StoreChangedEvent)surlEvent!);
        }

        Assert.AreEqual(KafkaSequenceCheck.Duplicate(4), states.Check("t", [batch]));
        CollectionAssert.AreEqual(
            new[] { (StoreKind.Producers, StoreChange.Reset), (StoreKind.Producers, StoreChange.Restored) },
            changes.Select(change => (change.Store, change.Change)).ToArray());
        CollectionAssert.AreEqual(
            new[] { StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts, StoreKind.Producers },
            running.Stores.RegisteredStoreOrder.ToArray());
    }

    [TestMethod]
    public void For_NoBrokers_IsNull()
    {
        var registration = KafkaBrokerStoreRegistration.For([], new SurlEventStream(TimeProvider.System));

        Assert.IsNull(registration);
    }

    private async Task<(ComposedServing Serving, FakeListenerFactory Factory, KafkaProducerStates States)> ComposeAsync()
    {
        var factory = new FakeListenerFactory();
        KafkaProducerStates? states = null;
        var seams = new StartTestSeams(factory).Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = services =>
            {
                var broker = ExtendedProtocolServers.ComposeKafkaBroker(services.ComposedServices);
                var answerers = ComposedServerFields.Single<FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer>>(broker);
                states = ComposedServerFields.Single<KafkaProducerStates>(answerers[KafkaApiKey.Produce]);
                return [broker];
            },
        };
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithKafka("--allow-anonymous", ListenUrl), seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return (start.Serving!, factory, states!);
    }

    private async Task<byte[]> SendAsync(FakeListenerFactory factory, byte[] request)
    {
        var connection = new FakeConnection(request);
        await factory.ConnectionsFor(ListenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        return connection.WrittenBytes;
    }

    // A request: size, then the header (api key, version, correlation id, client id) and the body.
    private static byte[] Frame(byte[] header, byte[] body)
    {
        var frame = new byte[4 + header.Length + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, header.Length + body.Length);
        header.CopyTo(frame, 4);
        body.CopyTo(frame, 4 + header.Length);
        return frame;
    }

    // Heartbeat v0 (api key 12) for group g, generation 1 and the member.
    private static byte[] HeartbeatV0(string member) => Frame(
        [0, 12, 0, 0, 0, 0, 0, 2, 0, 1, (byte)'c'],
        [0, 1, (byte)'g', 0, 0, 0, 1, .. ((byte[])[0, (byte)member.Length]), .. Encoding.ASCII.GetBytes(member)]);

    private static int ErrorOf(byte[] heartbeatAnswer) => BinaryPrimitives.ReadInt16BigEndian(heartbeatAnswer.AsSpan(8));

    // The member id of a JoinGroup v3 answer: after size, correlation id, throttle, error, generation, protocol and leader.
    private static string MemberOf(byte[] joinAnswer)
    {
        var offset = 4 + 4 + 4 + 2 + 4;
        offset += 2 + BinaryPrimitives.ReadInt16BigEndian(joinAnswer.AsSpan(offset));
        offset += 2 + BinaryPrimitives.ReadInt16BigEndian(joinAnswer.AsSpan(offset));
        var length = BinaryPrimitives.ReadInt16BigEndian(joinAnswer.AsSpan(offset));
        return Encoding.ASCII.GetString(joinAnswer, offset + 2, length);
    }
}
