using System.Text;
using Surl.Control;
using Surl.Core;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The control endpoint's read-back members over a composed instance (ADR-0164 decisions 1 to 4, BL-1203): each
/// returns what <see cref="ComposedStores"/> seeded, an absent item is a <c>NotFound</c> refusal, and no read
/// publishes an event.
/// </summary>
[TestClass]
public sealed class ComposedControlTargetReadTests
{
    private const string Mail = "Subject: one\r\n\r\nfirst\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadMessageAsync_ASeededMessage_ReturnsItsBytesUidSizeAndFlags()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(Mail), TestContext.CancellationToken);

        var boxes = await target.ListMailboxesAsync("b", TestContext.CancellationToken);
        var page = await target.ListMessagesAsync("b", "INBOX", 0, 10, TestContext.CancellationToken);
        var message = await target.ReadMessageAsync("b", "INBOX", page.Messages[0].Uid, TestContext.CancellationToken);

        Assert.AreEqual("INBOX", boxes.Single().Name);
        Assert.AreEqual(1, boxes[0].UnseenCount);
        Assert.AreEqual(Mail.Length, page.Messages[0].Size);
        Assert.IsFalse(page.HasMore);
        Assert.IsEmpty(message.Summary.Flags);
        Assert.AreEqual(Mail, Encoding.ASCII.GetString(message.Bytes.Span));
    }

    [TestMethod]
    public async Task ReadMessageAsync_ASeenMessage_NamesItsFlagsAsImapWritesThem()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(Mail), TestContext.CancellationToken);
        var uid = (await target.ListMessagesAsync("b", "INBOX", 0, 10, TestContext.CancellationToken)).Messages[0].Uid;
        var view = serving.Stores.Mail.ViewFor("b");
        _ = serving.Stores.Mail.ChangeFlags(view, "INBOX", uid, Surl.MailStore.MailFlagChange.Replace, Surl.MailStore.MailFlags.Seen | Surl.MailStore.MailFlags.Flagged, out _);

        var message = await target.ReadMessageAsync("b", "INBOX", uid, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "\\Seen", "\\Flagged" }, message.Summary.Flags.ToArray());
    }

    [TestMethod]
    public async Task ReadFileAsync_ASeededFile_ReturnsItsBytesAndTheFolderListsIt()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.WriteFileAsync("/reports/a.csv", "a,b\n"u8.ToArray(), TestContext.CancellationToken);

        var file = await target.ReadFileAsync("/reports/a.csv", TestContext.CancellationToken);
        var folder = await target.ListFolderAsync("/reports", null, 10, TestContext.CancellationToken);

        Assert.AreEqual("a,b\n", Encoding.UTF8.GetString(file.Bytes.Span));
        Assert.AreEqual(4, file.ByteCount);
        Assert.AreEqual("text/csv", file.ContentType);
        Assert.AreEqual(new ControlFolderEntry("a.csv", false, 4), folder.Entries.Single());
    }

    [TestMethod]
    public async Task BrowseQueueAsync_ASeededMessage_ReturnsItAndLeavesItQueued()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        using var connection = serving.Stores.Bus.Connect();
        _ = connection.DeclareQueue("orders", new BusQueueFlags(Durable: true));
        await target.PublishMessageAsync(new ControlMessage(null, "orders", "one"u8.ToArray(), false), TestContext.CancellationToken);

        var first = await target.BrowseQueueAsync("orders", -1, 10, TestContext.CancellationToken);
        var second = await target.BrowseQueueAsync("orders", -1, 10, TestContext.CancellationToken);

        Assert.AreEqual("one", Encoding.UTF8.GetString(first.Messages.Single().Payload.Span));
        Assert.AreEqual(0, first.Messages[0].Position);
        Assert.AreEqual(1, first.ReadyCount);
        Assert.AreEqual(1, second.ReadyCount);
    }

    [TestMethod]
    public async Task ReadRetainedMessageAsync_ARetainedTopic_ReturnsTheMessage()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.PublishMessageAsync(new ControlMessage("t/1", null, "hi"u8.ToArray(), true), TestContext.CancellationToken);

        var retained = await target.ReadRetainedMessageAsync("t/1", TestContext.CancellationToken);

        Assert.AreEqual("t/1", retained.Topic);
        Assert.AreEqual("hi", Encoding.UTF8.GetString(retained.Payload.Span));
    }

    [TestMethod]
    public async Task Reads_AbsentItems_AreNotFoundRefusals()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);

        var kinds = new List<ControlRefusalKind>
        {
            (await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ListMessagesAsync("b", "Nope", 0, 10, TestContext.CancellationToken))).Kind,
            (await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadMessageAsync("b", "INBOX", 1, TestContext.CancellationToken))).Kind,
            (await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadFileAsync("/none.txt", TestContext.CancellationToken))).Kind,
            (await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ListFolderAsync("/none", null, 10, TestContext.CancellationToken))).Kind,
            (await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.BrowseQueueAsync("none", -1, 10, TestContext.CancellationToken))).Kind,
            (await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadRetainedMessageAsync("none", TestContext.CancellationToken))).Kind,
            (await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadLogAsync("none", 0, 10, TestContext.CancellationToken))).Kind,
        };

        Assert.IsTrue(kinds.All(kind => kind == ControlRefusalKind.NotFound));
    }

    [TestMethod]
    public async Task Reads_ABadPathOrLimit_AreBadRequests()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);

        var path = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadFileAsync("/../x", TestContext.CancellationToken));
        var limit = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ListFolderAsync("/", null, 0, TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.BadRequest, path.Kind);
        Assert.AreEqual(ControlRefusalKind.BadRequest, limit.Kind);
    }

    [TestMethod]
    public async Task ReadFileAsync_AFilePastTheReadBound_IsATooLargeRefusal()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.WriteFileAsync("/big.bin", new byte[(int)ComposedStores.MaxReadBytes + 1], TestContext.CancellationToken);

        var refusal = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadFileAsync("/big.bin", TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.TooLarge, refusal.Kind);
    }

    [TestMethod]
    public async Task Reads_EveryMember_PublishNoEventOnTheStream()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(Mail), TestContext.CancellationToken);
        await target.WriteFileAsync("/a.txt", "x"u8.ToArray(), TestContext.CancellationToken);
        using var connection = serving.Stores.Bus.Connect();
        _ = connection.DeclareQueue("orders", new BusQueueFlags(Durable: true));
        await target.PublishMessageAsync(new ControlMessage(null, "orders", "one"u8.ToArray(), false), TestContext.CancellationToken);
        await target.PublishMessageAsync(new ControlMessage("t/1", null, "hi"u8.ToArray(), true), TestContext.CancellationToken);
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        _ = await target.ListMailboxesAsync("b", TestContext.CancellationToken);
        _ = await target.ListMessagesAsync("b", "INBOX", 0, 10, TestContext.CancellationToken);
        _ = await target.ReadMessageAsync("b", "INBOX", 1, TestContext.CancellationToken);
        _ = await target.ReadFileAsync("/a.txt", TestContext.CancellationToken);
        _ = await target.ListFolderAsync("/", null, 10, TestContext.CancellationToken);
        _ = await target.BrowseQueueAsync("orders", -1, 10, TestContext.CancellationToken);
        _ = await target.ReadRetainedMessageAsync("t/1", TestContext.CancellationToken);

        Assert.IsFalse(subscription.TryRead(out _));
    }

    private static ComposedControlTarget TargetOf(ComposedServing serving) => new(serving.Events, serving.Faults, serving.Stores);

    private async Task<ComposedServing> ComposeAsync()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse(["--allow-anonymous", "http://127.0.0.1:0/"]), new StartTestSeams().Seams, TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
    }
}
