using System.Text;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.Amqp0;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The AMQP 0-9-1 brokers' exchanges and bindings under whole-instance reset, snapshot and restore
/// (ADR-0159 decisions 2 to 4; BL-1170), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class Amqp0ExchangeStoreTests
{
    private const string ListenUrl = "amqp://127.0.0.1:0";

    // exchange.declare-ok on channel 1: frame type 1, channel 1, size 4, class 40, method 11, frame end.
    private static readonly byte[] DeclareOkOnChannelOne = [1, 0, 1, 0, 0, 0, 4, 0, 40, 0, 11, 0xCE];

    private static readonly byte[] ProtocolHeader = [0x41, 0x4D, 0x51, 0x50, 0, 0, 9, 1];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ResetAsync_RemovesAnExchangeDeclaredOverAmqpAndADeclareAfterItSucceeds()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = running.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        var first = await ClientDeclaringAsync(factory, "orders", "direct");
        Assert.IsTrue(Contains(first.WrittenBytes, DeclareOkOnChannelOne));

        var otherTypeBeforeReset = await ClientDeclaringAsync(factory, "orders", "fanout");
        await running.Stores.ResetAsync(TestContext.CancellationToken);
        var otherTypeAfterReset = await ClientDeclaringAsync(factory, "orders", "fanout");
        await stop.CancelAsync();
        await serve;

        Assert.IsFalse(Contains(otherTypeBeforeReset.WrittenBytes, DeclareOkOnChannelOne));
        Assert.IsTrue(Contains(otherTypeAfterReset.WrittenBytes, DeclareOkOnChannelOne));
    }

    [TestMethod]
    public async Task Stores_SnapshotChangeRestore_GivesBackTheExchangesDeclaredBeforeTheSnapshot()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = running.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await ClientDeclaringAsync(factory, "kept", "direct");
        var snapshot = await running.Stores.TakeSnapshotAsync(TestContext.CancellationToken);
        await running.Stores.ResetAsync(TestContext.CancellationToken);

        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        var otherType = await ClientDeclaringAsync(factory, "kept", "fanout");
        await stop.CancelAsync();
        await serve;

        CollectionAssert.AreEqual(
            new[] { StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts, StoreKind.Keyspace, StoreKind.Directory, StoreKind.Exchanges },
            running.Stores.RegisteredStoreOrder.ToArray());
        Assert.IsFalse(Contains(otherType.WrittenBytes, DeclareOkOnChannelOne));
    }

    [TestMethod]
    public async Task Stores_ResetAndRestore_PublishOneExchangesChangeEach()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        var snapshot = await running.Stores.TakeSnapshotAsync(StoreKind.Exchanges, TestContext.CancellationToken);
        await running.Stores.ResetAsync(StoreKind.Exchanges, TestContext.CancellationToken);
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<StoreChangedEvent> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            changes.Add((StoreChangedEvent)surlEvent!);
        }

        CollectionAssert.AreEqual(
            new[] { (StoreKind.Exchanges, StoreChange.Reset), (StoreKind.Exchanges, StoreChange.Restored) },
            changes.Select(change => (change.Store, change.Change)).ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutAnAmqpListenUrl_RegistersNoExchangesStore()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-anonymous", "http://127.0.0.1:0/"), new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        CollectionAssert.AreEqual((StoreKind[])[.. ComposedStores.StoreOrder, StoreKind.Directory], serving.Stores.RegisteredStoreOrder.ToArray());
    }

    [TestMethod]
    public void For_NoBrokers_IsNull()
    {
        var registration = Amqp0ExchangeStoreRegistration.For([], new SurlEventStream(TimeProvider.System));

        Assert.IsNull(registration);
    }

    [TestMethod]
    public async Task For_BrokersKeepingExchangesInFiles_RefuseResetAndSnapshot()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var services = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);
        var broker = new Amqp0Broker(services.AuthenticationPolicy, services.MessageBus);
        await broker.LoadExchangesAsync(new Amqp0ExchangeFiles(fileSystem, Path.Join(InMemoryContentFileSystem.RootPath, ".surl")), TestContext.CancellationToken);
        var registration = Amqp0ExchangeStoreRegistration.For([broker], new SurlEventStream(TimeProvider.System))!;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.ResetAsync(TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.TakeSnapshotAsync(long.MaxValue, TestContext.CancellationToken));
    }

    private async Task<(ComposedServing Serving, FakeListenerFactory Factory)> ComposeAsync()
    {
        var factory = new FakeListenerFactory();
        var seams = new StartTestSeams(factory).Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", ListenUrl], ["amqp"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return (start.Serving!, factory);
    }

    private async Task<FakeConnection> ClientDeclaringAsync(FakeListenerFactory factory, string exchange, string type)
    {
        var connection = new FakeConnection(
        [
            .. ProtocolHeader,
            .. Method(0, 10, 11, [.. Long(0), .. ShortString("ANONYMOUS"), .. Long(0), .. ShortString("en_US")]),
            .. Method(0, 10, 31, [.. Short(0), .. Long(0), .. Short(0)]),
            .. Method(0, 10, 40, [.. ShortString("/"), 0, 0]),
            .. Method(1, 20, 10, ShortString(string.Empty)),
            .. Method(1, 40, 10, [.. Short(0), .. ShortString(exchange), .. ShortString(type), 0, .. Long(0)]),
            .. Method(0, 10, 50, [.. Short(200), .. ShortString("bye"), .. Short(0), .. Short(0)]),
        ]);
        await factory.ConnectionsFor(ListenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        return connection;
    }

    private static bool Contains(byte[] written, byte[] frame) => written.AsSpan().IndexOf(frame) >= 0;

    private static byte[] Short(int value) => [(byte)(value >> 8), (byte)value];

    private static byte[] Long(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] ShortString(string text) => [(byte)text.Length, .. Encoding.ASCII.GetBytes(text)];

    private static byte[] Method(int channel, int classId, int methodId, byte[] arguments)
    {
        byte[] payload = [.. Short(classId), .. Short(methodId), .. arguments];
        return [1, .. Short(channel), .. Long(payload.Length), .. payload, 0xCE];
    }
}
