using System.Text;
using Surl.Cli;
using Surl.Core;
using Surl.Extended.Protocol.Redis;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The Redis keyspace under whole-instance seed, reset, snapshot and restore, shared by the instance's
/// <c>redis</c> and <c>rediss</c> server (ADR-0158 decisions 3 to 5; BL-1161), over
/// <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class RedisKeyspaceStoreTests
{
    private const string FirstListenUrl = "redis://127.0.0.1:6379";
    private const string SecondListenUrl = "redis://127.0.0.1:6380";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Exchange_ASetKey_IsSeenByAnotherListenerOfTheSameInstanceAndByNoOtherInstance()
    {
        var (first, firstFactory) = await ComposeAsync();
        var (second, secondFactory) = await ComposeAsync();
        await using var running = first;
        await using var other = second;

        await ExchangeAsync(running, firstFactory, FirstListenUrl, "SET k v\r\nQUIT\r\n");
        var sameInstance = await ExchangeAsync(running, firstFactory, SecondListenUrl, "GET k\r\nQUIT\r\n");
        var otherInstance = await ExchangeAsync(other, secondFactory, FirstListenUrl, "GET k\r\nQUIT\r\n");

        StringAssert.Contains(sameInstance, "$1\r\nv\r\n");
        StringAssert.Contains(otherInstance, "$-1\r\n");
    }

    [TestMethod]
    public async Task Stores_SeedKeyAsync_IsReadOverRedis()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;

        await running.Stores.SeedKeyAsync(0, Encoding.ASCII.GetBytes("seeded"), Encoding.ASCII.GetBytes("yes"), null, TestContext.CancellationToken);
        var answer = await ExchangeAsync(running, factory, FirstListenUrl, "GET seeded\r\nQUIT\r\n");

        StringAssert.Contains(answer, "$3\r\nyes\r\n");
    }

    [TestMethod]
    public async Task Stores_ResetAsync_EmptiesTheKeyspaceAndRestoreBringsItsKeysBack()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        await running.Stores.SeedKeyAsync(0, Encoding.ASCII.GetBytes("k"), Encoding.ASCII.GetBytes("v"), null, TestContext.CancellationToken);
        var snapshot = await running.Stores.TakeSnapshotAsync(TestContext.CancellationToken);

        await running.Stores.ResetAsync(TestContext.CancellationToken);
        var afterReset = await ExchangeAsync(running, factory, FirstListenUrl, "GET k\r\nQUIT\r\n");
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        var afterRestore = await ExchangeAsync(running, factory, FirstListenUrl, "GET k\r\nQUIT\r\n");

        StringAssert.Contains(afterReset, "$-1\r\n");
        StringAssert.Contains(afterRestore, "$1\r\nv\r\n");
        CollectionAssert.AreEqual(
            new[] { StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts, StoreKind.Keyspace, StoreKind.Exchanges, StoreKind.Producers, StoreKind.Sessions, StoreKind.Uploads },
            running.Stores.RegisteredStoreOrder.ToArray());
    }

    [TestMethod]
    public async Task Stores_ResetAsync_KeepsAKeySeededToBeKeptAndDropsOneSeededWithoutTheFlag()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await running.Stores.SeedKeyAsync(0, "kept"u8.ToArray(), "yes"u8.ToArray(), null, keepOnReset: true, TestContext.CancellationToken);
        await running.Stores.SeedKeyAsync(0, "dropped"u8.ToArray(), "no"u8.ToArray(), null, TestContext.CancellationToken);

        await running.Stores.ResetAsync(StoreKind.Keyspace, TestContext.CancellationToken);

        CollectionAssert.AreEqual("yes"u8.ToArray(), running.Stores.ReadKey(0, "kept"u8.ToArray()).Value!.Value!.Value.ToArray());
        Assert.AreEqual(ComposedReadOutcome.NotFound, running.Stores.ReadKey(0, "dropped"u8.ToArray()).Outcome);
    }

    [TestMethod]
    public async Task Stores_TakeSnapshotAsync_CountsTheKeyspaceAgainstTheSnapshotBound()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await running.Stores.SeedKeyAsync(0, Encoding.ASCII.GetBytes("key"), new byte[100], null, TestContext.CancellationToken);

        var snapshot = await running.Stores.TakeSnapshotAsync(StoreKind.Keyspace, TestContext.CancellationToken);
        var bounded = new ComposedStores(
            running.Stores.Content,
            running.Stores.MetadataIndex,
            running.Stores.Mail,
            running.Stores.Bus,
            running.Stores.Accounts,
            maxSnapshotBytes: 50,
            additionalRegistrations: [RedisKeyspaceStoreRegistration.For(KeyspaceWith(100))]);
        var refusal = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => bounded.TakeSnapshotAsync(TestContext.CancellationToken));

        Assert.AreEqual(103, snapshot.ByteCount);
        Assert.AreEqual(StoreKind.Keyspace, refusal.Store);
    }

    [TestMethod]
    public async Task Stores_SeedResetAndRestore_PublishTheirChangesOnTheInstancesStream()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        await running.Stores.SeedKeyAsync(0, Encoding.ASCII.GetBytes("k"), Encoding.ASCII.GetBytes("v"), null, TestContext.CancellationToken);
        var snapshot = await running.Stores.TakeSnapshotAsync(StoreKind.Keyspace, TestContext.CancellationToken);
        await running.Stores.ResetAsync(StoreKind.Keyspace, TestContext.CancellationToken);
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<StoreChange> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            changes.Add(((StoreChangedEvent)surlEvent!).Change);
        }

        CollectionAssert.AreEqual(new[] { StoreChange.Seeded, StoreChange.Reset, StoreChange.Restored }, changes);
    }

    [TestMethod]
    public async Task Stores_SeedKeyAsync_RefusesACancelledTokenAndAnInstanceWithoutRedis()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var without = new ComposedStores(
            running.Stores.Content, running.Stores.MetadataIndex, running.Stores.Mail, running.Stores.Bus, running.Stores.Accounts);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => running.Stores.SeedKeyAsync(0, new byte[1], new byte[1], null, cancelled.Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => without.SeedKeyAsync(0, new byte[1], new byte[1], null, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ComposeAsync_WithExtensionsOff_RegistersNoKeyspace()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", "http://127.0.0.1:0"]).CommandLine!,
            new StartTestSeams(new FakeListenerFactory()).Seams,
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var running = start.Serving!;

        CollectionAssert.DoesNotContain(running.Stores.RegisteredStoreOrder.ToArray(), StoreKind.Keyspace);
    }

    private static RedisKeyspace KeyspaceWith(int valueBytes)
    {
        var keyspace = new RedisKeyspace(TimeProvider.System, DiscardingSurlEventPublisher.Instance);
        keyspace.SeedKey(0, new byte[] { 1 }, new byte[valueBytes]);
        return keyspace;
    }

    private async Task<(ComposedServing Serving, FakeListenerFactory Factory)> ComposeAsync()
    {
        var factory = new FakeListenerFactory();
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", FirstListenUrl, SecondListenUrl], ["redis", "rediss"]).CommandLine!,
            new StartTestSeams(factory).Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return (start.Serving!, factory);
    }

    private async Task<string> ExchangeAsync(ComposedServing serving, FakeListenerFactory factory, string listenUrl, string request)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(request));
        await factory.ConnectionsFor(listenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
        return Encoding.Latin1.GetString(connection.WrittenBytes);
    }
}
