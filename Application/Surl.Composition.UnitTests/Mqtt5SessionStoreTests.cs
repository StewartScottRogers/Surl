using Surl.Cli;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The MQTT 5 brokers' sessions under whole-instance reset, snapshot and restore (ADR-0159 decisions 2 and 3;
/// BL-1171), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class Mqtt5SessionStoreTests
{
    private const string ListenUrl = "mqtt5://127.0.0.1:0";

    // CONNECT 5.0 for client s with a Session Expiry Interval of 60 seconds, Clean Start 1 and 0.
    private static readonly byte[] SubscriberConnectClean = [0x10, 0x13, 0, 4, 0x4D, 0x51, 0x54, 0x54, 5, 0x02, 0, 0, 5, 0x11, 0, 0, 0, 60, 0, 1, 0x73];
    private static readonly byte[] SubscriberConnectResuming = [0x10, 0x13, 0, 4, 0x4D, 0x51, 0x54, 0x54, 5, 0x00, 0, 0, 5, 0x11, 0, 0, 0, 60, 0, 1, 0x73];

    // SUBSCRIBE t at QoS 1; DISCONNECT.
    private static readonly byte[] SubscribeToT = [0x82, 0x07, 0, 1, 0, 0, 1, 0x74, 0x01];
    private static readonly byte[] Disconnect = [0xE0, 0x00];

    // CONNECT for client p; PUBLISH t "a" at QoS 1.
    private static readonly byte[] PublisherConnect = [0x10, 0x0E, 0, 4, 0x4D, 0x51, 0x54, 0x54, 5, 0x02, 0, 0, 0, 0, 1, 0x70];
    private static readonly byte[] PublishToT = [0x32, 0x07, 0, 1, 0x74, 0, 1, 0, 0x61];

    // CONNACK Success with Session Present 0 and 1; and the queued PUBLISH as the broker sends it to s.
    private static readonly byte[] ConnackNoSessionPresent = [0x20, 0x11, 0x00, 0x00];
    private static readonly byte[] ConnackSessionPresent = [0x20, 0x11, 0x01, 0x00];
    private static readonly byte[] QueuedPublish = [0x32, 0x07, 0, 1, 0x74, 0, 1, 0, 0x61];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ResetAsync_DoesNotDeliverAQoS1MessageQueuedForAnOfflineSessionWhenItsClientReconnects()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = running.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await ClientSendingAsync(factory, SubscriberConnectClean, SubscribeToT, Disconnect);
        await ClientSendingAsync(factory, PublisherConnect, PublishToT, Disconnect);

        await running.Stores.ResetAsync(TestContext.CancellationToken);
        var reconnected = await ClientSendingAsync(factory, SubscriberConnectResuming, Disconnect);
        await stop.CancelAsync();
        await serve;

        Assert.IsTrue(Contains(reconnected.WrittenBytes, ConnackNoSessionPresent));
        Assert.IsFalse(Contains(reconnected.WrittenBytes, QueuedPublish));
    }

    [TestMethod]
    public async Task Stores_WithoutAReset_DeliverAQoS1MessageQueuedForAnOfflineSessionWhenItsClientReconnects()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = running.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await ClientSendingAsync(factory, SubscriberConnectClean, SubscribeToT, Disconnect);
        await ClientSendingAsync(factory, PublisherConnect, PublishToT, Disconnect);

        var reconnected = await ClientSendingAsync(factory, SubscriberConnectResuming, Disconnect);
        await stop.CancelAsync();
        await serve;

        Assert.IsTrue(Contains(reconnected.WrittenBytes, ConnackSessionPresent));
        Assert.IsTrue(Contains(reconnected.WrittenBytes, QueuedPublish));
    }

    [TestMethod]
    public async Task Stores_SnapshotResetRestore_GivesBackTheOfflineSessionWithItsSubscription()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = running.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await ClientSendingAsync(factory, SubscriberConnectClean, SubscribeToT, Disconnect);
        var snapshot = await running.Stores.TakeSnapshotAsync(TestContext.CancellationToken);
        await running.Stores.ResetAsync(TestContext.CancellationToken);

        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        var resumed = await ClientSendingAsync(factory, SubscriberConnectResuming, Disconnect);
        await stop.CancelAsync();
        await serve;

        Assert.IsTrue(Contains(resumed.WrittenBytes, ConnackSessionPresent));
    }

    [TestMethod]
    public async Task Stores_ResetAndRestore_PublishOneSessionsChangeEach()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        var snapshot = await running.Stores.TakeSnapshotAsync(StoreKind.Sessions, TestContext.CancellationToken);
        await running.Stores.ResetAsync(StoreKind.Sessions, TestContext.CancellationToken);
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<StoreChangedEvent> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            changes.Add((StoreChangedEvent)surlEvent!);
        }

        CollectionAssert.AreEqual(
            new[] { (StoreKind.Sessions, StoreChange.Reset), (StoreKind.Sessions, StoreChange.Reset), (StoreKind.Sessions, StoreChange.Restored), (StoreKind.Sessions, StoreChange.Restored) },
            changes.Select(change => (change.Store, change.Change)).ToArray());
        CollectionAssert.AreEqual(
            new[] { StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts, StoreKind.Keyspace, StoreKind.Directory, StoreKind.Exchanges, StoreKind.Producers, StoreKind.Sessions, StoreKind.Zones, StoreKind.Uploads },
            running.Stores.RegisteredStoreOrder.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutAnMqtt5ListenUrl_RegistersNoSessionsStore()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-anonymous", "http://127.0.0.1:0/"), new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        CollectionAssert.AreEqual((StoreKind[])[.. ComposedStores.StoreOrder, StoreKind.Directory], serving.Stores.RegisteredStoreOrder.ToArray());
    }

    [TestMethod]
    public void For_NoBrokers_IsNull()
    {
        var registration = Mqtt5SessionStoreRegistration.For([], new SurlEventStream(TimeProvider.System));

        Assert.IsNull(registration);
    }

    private async Task<(ComposedServing Serving, FakeListenerFactory Factory)> ComposeAsync()
    {
        var factory = new FakeListenerFactory();
        var seams = new StartTestSeams(factory).Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", ListenUrl], ["mqtt5"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return (start.Serving!, factory);
    }

    private async Task<FakeConnection> ClientSendingAsync(FakeListenerFactory factory, params byte[][] packets)
    {
        var connection = new FakeConnection([.. packets.SelectMany(packet => packet)]);
        await factory.ConnectionsFor(ListenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        return connection;
    }

    private static bool Contains(byte[] written, byte[] frame) => written.AsSpan().IndexOf(frame) >= 0;
}
