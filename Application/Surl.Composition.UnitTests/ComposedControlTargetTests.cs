using System.Text;
using Surl.Content;
using Surl.Control;
using Surl.Core;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The control endpoint's target over a composed instance (ADR-0124 decision 7, BL-816): each request reaches the
/// instance's fault plan or stores, and each refusal is the <see cref="ControlRefusalKind"/> the ADR's error table
/// names. No network: the instances are composed and never served.
/// </summary>
[TestClass]
public sealed class ComposedControlTargetTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddFault_AnItem_AddsTheRuleUnderTheNextIdAndListFaultsWritesItBack()
    {
        await using var serving = await ComposeAsync("--fault", "refuse-login;account=alice");
        var target = TargetOf(serving);

        var faultId = target.AddFault("error;scheme=smtp;reply=550 no%3B really;times=2");

        Assert.AreEqual("f2", faultId);
        CollectionAssert.AreEqual(
            new[]
            {
                new ControlFault("f1", "refuse-login;account=alice", null),
                new ControlFault("f2", "error;scheme=smtp;reply=550 no%3B really;times=2", 2),
            },
            target.ListFaults().ToArray());
    }

    [TestMethod]
    public async Task AddFault_AnItemTheReaderRefuses_IsABadRequestWithTheReadersText()
    {
        await using var serving = await ComposeAsync();

        var refusal = Assert.ThrowsExactly<ControlRefusalException>(() => TargetOf(serving).AddFault("nosuch"));

        Assert.AreEqual(ControlRefusalKind.BadRequest, refusal.Kind);
        StringAssert.Contains(refusal.Message, "option --fault: unknown kind nosuch");
        Assert.IsEmpty(serving.Faults.Rules);
    }

    [TestMethod]
    public async Task AddFault_APlanAlreadyFull_IsAConflict()
    {
        await using var serving = await ComposeAsync();
        for (var count = 0; count < FaultPlan.MaxRules; count++)
        {
            serving.Faults.Add(new FaultRule(string.Empty, FaultKind.RefuseLogin));
        }

        var refusal = Assert.ThrowsExactly<ControlRefusalException>(() => TargetOf(serving).AddFault("drop"));

        Assert.AreEqual(ControlRefusalKind.Conflict, refusal.Kind);
    }

    [TestMethod]
    public async Task RemoveFault_AKnownAndAnUnknownId_RemovesTheRuleAndReportsWhichWasThere()
    {
        await using var serving = await ComposeAsync("--fault", "drop");
        var target = TargetOf(serving);

        Assert.IsTrue(target.RemoveFault("f1"));
        Assert.IsFalse(target.RemoveFault("f1"));
        Assert.IsEmpty(target.ListFaults());
    }

    [TestMethod]
    public async Task SeedMailAsync_ADeliverableRecipient_DeliversTheMessage()
    {
        await using var serving = await ComposeAsync("--allow-anonymous");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await TargetOf(serving).SeedMailAsync("b@y", "Subject: s\r\n\r\nbody\r\n"u8.ToArray(), TestContext.CancellationToken);

        Assert.IsTrue(subscription.TryRead(out var seeded));
        Assert.AreEqual(StoreChange.Seeded, ((StoreChangedEvent)seeded!).Change);
    }

    [TestMethod]
    public async Task SeedMailAsync_AnInvalidAddressNoSuchAccountOrATooLargeMessage_IsRefusedByKind()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret", "--max-filesize", "8");
        var target = TargetOf(serving);

        var invalid = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.SeedMailAsync("<<>>", "x"u8.ToArray(), TestContext.CancellationToken));
        var unknown = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.SeedMailAsync("bob@example.com", "x"u8.ToArray(), TestContext.CancellationToken));
        var tooLarge = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.SeedMailAsync("alice@example.com", "Subject: far too long\r\n\r\n"u8.ToArray(), TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.BadRequest, invalid.Kind);
        Assert.AreEqual(ControlRefusalKind.NotFound, unknown.Kind);
        Assert.AreEqual("no mailbox receives mail for bob@example.com", unknown.Message);
        Assert.AreEqual(ControlRefusalKind.Conflict, tooLarge.Kind);
        Assert.AreEqual("the mail store did not deliver to alice@example.com: MessageTooLarge", tooLarge.Message);
    }

    [TestMethod]
    public async Task SeedMailAsync_ARecipientOutsideTheMailDomains_IsNotFound()
    {
        await using var serving = await ComposeAsync("--mail-domain", "example.com", "-u", "alice:secret");

        var refusal = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => TargetOf(serving).SeedMailAsync("bob@elsewhere.org", "x"u8.ToArray(), TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.NotFound, refusal.Kind);
        Assert.AreEqual("no mailbox receives mail for bob@elsewhere.org", refusal.Message);
    }

    [TestMethod]
    public async Task ReadEventsAsync_AnAccountAddedThenCancelled_YieldsItsStoreChangeThenEnds()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        await using var events = target.ReadEventsAsync(cancellation.Token).GetAsyncEnumerator(TestContext.CancellationToken);
        var moved = events.MoveNextAsync();

        await target.AddAccountAsync("bob", "builder", TestContext.CancellationToken);
        var yielded = await moved;
        var current = events.Current;
        var movedAfterCancel = events.MoveNextAsync();
        await cancellation.CancelAsync();

        Assert.IsTrue(yielded);
        Assert.AreEqual(StoreKind.Accounts, ((StoreChangedEvent)current).Store);
        Assert.IsFalse(await movedAfterCancel);
    }

    [TestMethod]
    public async Task ReadEventsAsync_Cancelled_EndsTheReadWithoutThrowing()
    {
        await using var serving = await ComposeAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        await using var events = TargetOf(serving).ReadEventsAsync(cancellation.Token).GetAsyncEnumerator(TestContext.CancellationToken);
        var moved = events.MoveNextAsync();

        await cancellation.CancelAsync();

        Assert.IsFalse(await moved);
    }

    [TestMethod]
    public async Task WriteFileAsyncAndDeleteFileAsync_AFile_WriteItThenDeleteIt()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);

        await target.WriteFileAsync("/a/b.txt", "bytes"u8.ToArray(), TestContext.CancellationToken);
        var kindAfterWrite = serving.Stores.Content.GetEntryKind(serving.Stores.Content.MapRequestPath("/a/b.txt"));
        var deleted = await target.DeleteFileAsync("/a/b.txt", TestContext.CancellationToken);
        var deletedAgain = await target.DeleteFileAsync("/a/b.txt", TestContext.CancellationToken);

        Assert.AreEqual(ContentEntryKind.File, kindAfterWrite);
        Assert.IsTrue(deleted);
        Assert.IsFalse(deletedAgain);
    }

    [TestMethod]
    public async Task WriteFileAsync_ANotPermittedPathATooLargeFileOrABadPath_IsRefusedByKind()
    {
        await using var serving = await ComposeAsync("--max-filesize", "2");
        var target = TargetOf(serving);

        var notPermitted = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.WriteFileAsync("/.surl/x", "x"u8.ToArray(), TestContext.CancellationToken));
        var tooLarge = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.WriteFileAsync("/big.txt", "too big"u8.ToArray(), TestContext.CancellationToken));
        var badPath = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.WriteFileAsync("/../x", "x"u8.ToArray(), TestContext.CancellationToken));
        var badDeletePath = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.DeleteFileAsync("/../x", TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.BadRequest, notPermitted.Kind);
        Assert.AreEqual(ControlRefusalKind.Conflict, tooLarge.Kind);
        Assert.AreEqual(ControlRefusalKind.BadRequest, badPath.Kind);
        Assert.AreEqual("/../x is not a path a request may name", badDeletePath.Message);
    }

    [TestMethod]
    public async Task PublishMessageAsync_ATopic_PublishesItRetained()
    {
        await using var serving = await ComposeAsync();
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await TargetOf(serving).PublishMessageAsync(new ControlMessage("t/1", null, "hi"u8.ToArray(), true), TestContext.CancellationToken);

        using var reader = serving.Stores.Bus.Connect();
        var retained = reader.Subscribe("#", _ => { }, includeRetained: true).RetainedMessages;
        Assert.AreEqual("t/1", retained.Single().Topic);
    }

    [TestMethod]
    public async Task PublishMessageAsync_AnUnknownQueueOrAnInvalidTopic_IsRefusedByKind()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);

        var unknownQueue = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.PublishMessageAsync(new ControlMessage(null, "nosuch", "x"u8.ToArray(), false), TestContext.CancellationToken));
        var invalidTopic = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.PublishMessageAsync(new ControlMessage("a/#", null, "x"u8.ToArray(), false), TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.NotFound, unknownQueue.Kind);
        Assert.AreEqual("the message bus refused the message: UnknownQueue", unknownQueue.Message);
        Assert.AreEqual(ControlRefusalKind.BadRequest, invalidTopic.Kind);
    }

    [TestMethod]
    [DataRow(MessageBusOutcome.UnknownQueue, ControlRefusalKind.NotFound)]
    [DataRow(MessageBusOutcome.InvalidTopic, ControlRefusalKind.BadRequest)]
    [DataRow(MessageBusOutcome.InvalidQueueName, ControlRefusalKind.BadRequest)]
    [DataRow(MessageBusOutcome.MessageTooLarge, ControlRefusalKind.BadRequest)]
    [DataRow(MessageBusOutcome.QueueFull, ControlRefusalKind.Conflict)]
    [DataRow(MessageBusOutcome.StoreFull, ControlRefusalKind.Conflict)]
    [DataRow(MessageBusOutcome.InvalidFilter, ControlRefusalKind.Conflict)]
    [DataRow(MessageBusOutcome.TooManyTopics, ControlRefusalKind.Conflict)]
    public void RefusalKindOf_EachOutcome_IsTheKindItsStatusNames(MessageBusOutcome outcome, ControlRefusalKind expected)
    {
        Assert.AreEqual(expected, ComposedControlTarget.RefusalKindOf(outcome));
    }

    [TestMethod]
    public async Task AddAccountAsyncAndRemoveAccountAsync_AnAccount_AddReplaceAndRemoveIt()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var target = TargetOf(serving);

        await target.AddAccountAsync("bob", "builder", TestContext.CancellationToken);
        await target.AddAccountAsync("alice", "changed", TestContext.CancellationToken);

        // Replacing alice removes her and adds her again, after bob.
        CollectionAssert.AreEqual(new[] { "bob", "alice" }, serving.Stores.Accounts.UserNames.ToArray());
        Assert.IsTrue(await target.RemoveAccountAsync("bob", TestContext.CancellationToken));
        Assert.IsFalse(await target.RemoveAccountAsync("bob", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ResetSnapshotAndRestore_InMemory_RestoreTheFileTheSnapshotHeld()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.WriteFileAsync("/kept.txt", "kept"u8.ToArray(), TestContext.CancellationToken);

        var snapshot = await target.SnapshotAsync(TestContext.CancellationToken);
        await target.ResetAsync(TestContext.CancellationToken);
        var kindAfterReset = FileKind(serving, "/kept.txt");
        await target.RestoreAsync(snapshot, TestContext.CancellationToken);

        Assert.AreEqual(ContentEntryKind.None, kindAfterReset);
        Assert.AreEqual(ContentEntryKind.File, FileKind(serving, "/kept.txt"));
    }

    [TestMethod]
    public async Task ResetAndSnapshotAsync_StoresOnDisk_AreConflicts()
    {
        await using var serving = await ComposeAsync("--directory", "served");
        var target = TargetOf(serving);

        var reset = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ResetAsync(TestContext.CancellationToken));
        var snapshot = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.SnapshotAsync(TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.Conflict, reset.Kind);
        Assert.AreEqual(ControlRefusalKind.Conflict, snapshot.Kind);
    }

    private static ComposedControlTarget TargetOf(ComposedServing serving) => new(serving.Events, serving.Faults, serving.Stores);

    private static ContentEntryKind FileKind(ComposedServing serving, string path) =>
        serving.Stores.Content.GetEntryKind(serving.Stores.Content.MapRequestPath(path));

    private async Task<ComposedServing> ComposeAsync(params string[] options)
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "http://127.0.0.1:0/"]), new StartTestSeams().Seams, TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
    }
}
