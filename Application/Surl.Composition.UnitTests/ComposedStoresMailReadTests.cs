using System.Text;
using Surl.Content;
using Surl.Core;
using Surl.MailStore;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A test reads delivered mail back through <see cref="ComposedStores"/> without an IMAP client
/// (ADR-0164, BL-1196).
/// </summary>
[TestClass]
public sealed class ComposedStoresMailReadTests
{
    private const string First = "Subject: one\r\n\r\nfirst\r\n";
    private const string Second = "Subject: two\r\n\r\nsecond body\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadMessage_TwoSeededMessages_GiveTheirUidsSizesAndExactBytesInDeliveryOrder()
    {
        await using var serving = await ComposeAsync("--allow-anonymous");
        var stores = serving.Stores;
        await stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(First), TestContext.CancellationToken);
        await stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(Second), TestContext.CancellationToken);

        var boxes = stores.ListMailboxes("b").Value!;
        var page = stores.ListMessages("b", "inbox").Value!;
        var one = stores.ReadMessage("b", "INBOX", page.Messages[0].Uid).Value!;
        var two = stores.ReadMessage("b", "INBOX", page.Messages[1].Uid).Value!;

        Assert.AreEqual("INBOX", boxes.Single().Name);
        Assert.AreEqual(2, boxes[0].MessageCount);
        Assert.AreEqual(2, boxes[0].UnseenCount);
        Assert.IsFalse(page.HasMore);
        Assert.IsTrue(page.Messages[0].Uid < page.Messages[1].Uid);
        Assert.AreEqual(First.Length, page.Messages[0].Size);
        Assert.AreEqual(First, Encoding.ASCII.GetString(one.Bytes.Span));
        Assert.AreEqual(Second, Encoding.ASCII.GetString(two.Bytes.Span));
        Assert.AreEqual(Second.Length, two.Summary.Size);
    }

    [TestMethod]
    public async Task ListMessages_ALimitAndACursor_PageInUidOrder()
    {
        await using var serving = await ComposeAsync("--allow-anonymous");
        var stores = serving.Stores;
        await stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(First), TestContext.CancellationToken);
        await stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(Second), TestContext.CancellationToken);

        var firstPage = stores.ListMessages("b", "INBOX", 0, 1).Value!;
        var secondPage = stores.ListMessages("b", "INBOX", firstPage.Messages[0].Uid, 1).Value!;

        Assert.IsTrue(firstPage.HasMore);
        Assert.AreEqual(1, secondPage.Messages.Count);
        Assert.IsTrue(secondPage.Messages[0].Uid > firstPage.Messages[0].Uid);
        Assert.IsFalse(secondPage.HasMore);
    }

    [TestMethod]
    public async Task ListMessages_ALimitOutsideOneToAThousand_IsRefused()
    {
        await using var serving = await ComposeAsync("--allow-anonymous");

        Assert.AreEqual(ComposedReadOutcome.Refused, serving.Stores.ListMessages("b", "INBOX", 0, 0).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, serving.Stores.ListMessages("b", "INBOX", 0, 1001).Outcome);
    }

    [TestMethod]
    public async Task Reads_AnUnknownMailboxOrMessage_AreNotFound()
    {
        await using var serving = await ComposeAsync("--allow-anonymous");
        var stores = serving.Stores;
        await stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(First), TestContext.CancellationToken);

        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListMessages("b", "Nope").Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ReadMessage("b", "Nope", 1).Outcome);
        var missing = stores.ReadMessage("b", "INBOX", 999);
        Assert.AreEqual(ComposedReadOutcome.NotFound, missing.Outcome);
        Assert.IsFalse(missing.IsFound);
        Assert.IsNull(missing.Value);
        Assert.IsNotNull(missing.Reason);
    }

    [TestMethod]
    public async Task Reads_AnUnknownAccount_AreNotFound()
    {
        await using var serving = await ComposeAsync("--allow-plaintext-auth", "-u", "alice:secret");
        var stores = serving.Stores;

        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListMailboxes("nobody").Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListMessages("nobody", "INBOX").Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ReadMessage("nobody", "INBOX", 1).Outcome);
    }

    [TestMethod]
    public async Task ReadMessage_AMessageLargerThanTheBound_IsRefusedNotTruncated()
    {
        await using var serving = await ComposeAsync("--allow-anonymous");
        var stores = serving.Stores;
        var big = new byte[(int)ComposedStores.MaxReadBytes + 1];
        Array.Fill(big, (byte)'x');
        await stores.SeedMailAsync("b@y", big, TestContext.CancellationToken);
        var uid = stores.ListMessages("b", "INBOX").Value!.Messages.Single().Uid;

        var result = stores.ReadMessage("b", "INBOX", uid);

        Assert.AreEqual(ComposedReadOutcome.TooLarge, result.Outcome);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public async Task Reads_LeaveFlagsUnchangedAndPublishNoStoreChange()
    {
        await using var serving = await ComposeAsync("--allow-anonymous");
        var stores = serving.Stores;
        await stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(First), TestContext.CancellationToken);
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());
        var before = stores.ListMessages("b", "INBOX").Value!.Messages.Single();

        _ = stores.ListMailboxes("b");
        _ = stores.ReadMessage("b", "INBOX", before.Uid);

        var after = stores.ListMessages("b", "INBOX").Value!.Messages.Single();
        Assert.AreEqual(MailFlags.None, after.Flags);
        Assert.AreEqual(before, after);
        Assert.AreEqual(1, stores.ListMailboxes("b").Value![0].UnseenCount);
        Assert.IsFalse(subscription.TryRead(out _));
    }

    private async Task<ComposedServing> ComposeAsync(params string[] options)
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "http://127.0.0.1:0/"]),
            new StartTestSeams().Seams with { ContentWithoutDataDirectory = new InMemoryContentFileSystem(TimeProvider.System) },
            TestContext.CancellationToken);
        return start.Serving!;
    }
}
