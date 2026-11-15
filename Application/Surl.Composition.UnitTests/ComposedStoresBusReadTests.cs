using Surl.Core;
using Surl.MessageBus;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A test reads queue messages, retained messages and topic logs back through <see cref="ComposedStores"/>
/// without a broker client (ADR-0164, BL-1198).
/// </summary>
[TestClass]
public sealed class ComposedStoresBusReadTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BrowseQueue_TwoSeededMessages_ListInOrder_AndTheQueueStillHoldsBoth()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        using var connection = stores.Bus.Connect();
        _ = connection.DeclareQueue("orders", new BusQueueFlags(Durable: true));
        Assert.AreEqual(MessageBusOutcome.Succeeded, await stores.SeedEnqueueAsync("orders", new BusMessage("t", "one"u8.ToArray()), TestContext.CancellationToken));
        Assert.AreEqual(MessageBusOutcome.Succeeded, await stores.SeedEnqueueAsync("orders", new BusMessage("t", "two"u8.ToArray()), TestContext.CancellationToken));

        var first = stores.BrowseQueue("orders").Value!;
        var second = stores.BrowseQueue("orders").Value!;
        var paged = stores.BrowseQueue("orders", 0, 1).Value!;

        Assert.AreEqual(2, first.Messages.Count);
        Assert.AreEqual("one", System.Text.Encoding.ASCII.GetString(first.Messages[0].Message.Payload.Span));
        Assert.AreEqual("two", System.Text.Encoding.ASCII.GetString(first.Messages[1].Message.Payload.Span));
        Assert.AreEqual(2, first.ReadyCount);
        Assert.AreEqual(0, first.InFlightCount);
        Assert.IsFalse(first.HasMore);
        Assert.AreEqual(2, second.Messages.Count);
        Assert.AreEqual(1, paged.Messages.Single().Position);
        Assert.IsFalse(paged.HasMore);
        Assert.IsTrue(stores.BrowseQueue("orders", -1, 1).Value!.HasMore);
    }

    [TestMethod]
    public async Task ReadRetainedMessage_ARetainedPublish_ReadsBack_AndStaysThere()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        _ = await stores.SeedPublishAsync(new BusMessage("a/b", "kept"u8.ToArray()) { Retain = true }, TestContext.CancellationToken);

        var read = stores.ReadRetainedMessage("a/b");
        var again = stores.ReadRetainedMessage("a/b");

        Assert.IsTrue(read.IsFound);
        Assert.AreEqual("kept", System.Text.Encoding.ASCII.GetString(read.Value!.Payload.Span));
        Assert.IsTrue(again.IsFound);
    }

    [TestMethod]
    public async Task ReadLog_FromOffsetZero_ReturnsRecordsAndPages()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        foreach (var text in new[] { "x", "y", "z" })
        {
            _ = await stores.SeedPublishAsync(new BusMessage("log/t", System.Text.Encoding.ASCII.GetBytes(text)), TestContext.CancellationToken);
        }

        var all = stores.ReadLog("log/t").Value!;
        var page = stores.ReadLog("log/t", 1, 1).Value!;

        Assert.AreEqual(3, all.Records.Count);
        Assert.AreEqual(0, all.Records[0].Offset);
        Assert.AreEqual(3, all.NextOffset);
        Assert.AreEqual(3, all.HighWatermark);
        Assert.IsFalse(all.HasMore);
        Assert.AreEqual(1, page.Records.Single().Offset);
        Assert.AreEqual(2, page.NextOffset);
        Assert.IsTrue(page.HasMore);
    }

    [TestMethod]
    public async Task Reads_UnknownQueueAndTopic_AreNotFound_AndBadArgumentsRefused()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        _ = await stores.SeedPublishAsync(new BusMessage("log/t", "x"u8.ToArray()), TestContext.CancellationToken);

        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.BrowseQueue("nope").Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ReadRetainedMessage("log/t").Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ReadLog("nope/t").Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.BrowseQueue("nope", -1, 0).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.BrowseQueue("nope", -2, 5).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.BrowseQueue("nope", -1, 1001).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ReadLog("log/t", 0, 0).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ReadLog("log/t", 99).Outcome);
    }

    [TestMethod]
    public async Task Reads_PublishNoEvent_AndNullArgumentsThrow()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        _ = await stores.SeedPublishAsync(new BusMessage("log/t", "x"u8.ToArray()) { Retain = true }, TestContext.CancellationToken);
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        _ = stores.BrowseQueue("nope");
        _ = stores.ReadRetainedMessage("log/t");
        _ = stores.ReadLog("log/t");

        Assert.IsFalse(subscription.TryRead(out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => stores.BrowseQueue(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => stores.ReadRetainedMessage(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => stores.ReadLog(null!));
    }

    private async Task<ComposedServing> ComposeAsync()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse(["--allow-anonymous", "http://127.0.0.1:0/"]),
            new StartTestSeams().Seams,
            TestContext.CancellationToken);
        return start.Serving!;
    }
}
