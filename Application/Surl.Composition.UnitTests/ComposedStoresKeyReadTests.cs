using System.Text;
using Surl.Cli;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A test reads the Redis keyspace back through <see cref="ComposedStores"/> without a Redis client
/// (ADR-0164 decision 9, BL-1218).
/// </summary>
[TestClass]
public sealed class ComposedStoresKeyReadTests
{
    private const string ListenUrl = "redis://127.0.0.1:6379";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadKey_ASeededKey_ReadsBackAndPublishesNothing()
    {
        var (running, _) = await ComposeAsync();
        await using var _1 = running;
        var stores = running.Stores;
        await stores.SeedKeyAsync(0, "k"u8.ToArray(), "v"u8.ToArray(), TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        var read = stores.ReadKey(0, "k"u8.ToArray());
        var listed = stores.ListKeys(0, "*");

        Assert.IsTrue(read.IsFound);
        Assert.AreEqual("v", Encoding.ASCII.GetString(read.Value!.Value!.Value.Span));
        Assert.AreEqual(1, listed.Value!.Keys.Count);
        Assert.IsFalse(listed.Value.HasMore);
        Assert.IsFalse(subscription.TryRead(out _));
    }

    [TestMethod]
    public async Task ReadKey_AbsentKeyOrBadDatabase_IsNotFoundOrRefused()
    {
        var (running, _) = await ComposeAsync();
        await using var _1 = running;

        Assert.AreEqual(ComposedReadOutcome.NotFound, running.Stores.ReadKey(0, "nope"u8.ToArray()).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, running.Stores.ReadKey(16, "k"u8.ToArray()).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, running.Stores.ReadKey(-1, "k"u8.ToArray()).Outcome);
    }

    [TestMethod]
    public async Task ListKeys_BadLimitOrDatabase_IsRefused_AndNullPatternThrows()
    {
        var (running, _) = await ComposeAsync();
        await using var _1 = running;
        var stores = running.Stores;

        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ListKeys(0, "*", default, 0).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ListKeys(0, "*", default, 1001).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ListKeys(16, "*").Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ListKeys(-1, "*").Outcome);
        Assert.ThrowsExactly<ArgumentNullException>(() => stores.ListKeys(0, null!));
    }

    [TestMethod]
    public async Task ListKeys_PagesInOrder()
    {
        var (running, _) = await ComposeAsync();
        await using var _1 = running;
        var stores = running.Stores;
        foreach (var key in new[] { "b", "a", "c" })
        {
            await stores.SeedKeyAsync(0, Encoding.ASCII.GetBytes(key), "v"u8.ToArray(), null, TestContext.CancellationToken);
        }

        var first = stores.ListKeys(0, "*", default, 2).Value!;
        var second = stores.ListKeys(0, "*", "b"u8.ToArray(), 2).Value!;

        Assert.AreEqual(2, first.Keys.Count);
        Assert.IsTrue(first.HasMore);
        Assert.AreEqual(1, second.Keys.Count);
        Assert.IsFalse(second.HasMore);
    }

    [TestMethod]
    public async Task ReadKey_AValueOverTheReadBound_IsTooLarge()
    {
        var (running, factory) = await ComposeAsync();
        await using var _1 = running;
        var size = (int)ComposedStores.MaxReadBytes + 1;
        var request = $"*3\r\n$3\r\nSET\r\n$3\r\nbig\r\n${size}\r\n" + new string('x', size) + "\r\n*1\r\n$4\r\nQUIT\r\n";

        await ExchangeAsync(running, factory, request);
        var read = running.Stores.ReadKey(0, "big"u8.ToArray());

        Assert.AreEqual(ComposedReadOutcome.TooLarge, read.Outcome);
    }

    [TestMethod]
    public async Task ReadKey_AnInstanceWithoutRedis_IsNotFound_AndListsNothing()
    {
        var (running, _) = await ComposeAsync();
        await using var _1 = running;
        var without = new ComposedStores(
            running.Stores.Content, running.Stores.MetadataIndex, running.Stores.Mail, running.Stores.Bus, running.Stores.Accounts);

        Assert.AreEqual(ComposedReadOutcome.NotFound, without.ReadKey(0, "k"u8.ToArray()).Outcome);
        Assert.IsEmpty(without.ListKeys(0, "*").Value!.Keys);
    }

    private async Task<(ComposedServing Serving, FakeListenerFactory Factory)> ComposeAsync()
    {
        var factory = new FakeListenerFactory();
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", ListenUrl], ["redis", "rediss"]).CommandLine!,
            new StartTestSeams(factory).Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return (start.Serving!, factory);
    }

    private async Task ExchangeAsync(ComposedServing serving, FakeListenerFactory factory, string request)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(request));
        await factory.ConnectionsFor(ListenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
    }
}
