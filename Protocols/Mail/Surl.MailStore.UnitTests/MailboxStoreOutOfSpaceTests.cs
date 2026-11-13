using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// How <see cref="MailboxStore"/> answers an out-of-space fault rule (ADR-0124 decision 4;
/// ADR-0127).
/// </summary>
[TestClass]
public sealed class MailboxStoreOutOfSpaceTests
{
    private static readonly FaultRule AnnOutOfSpace = new("f1", FaultKind.OutOfSpace) { Account = "ann" };

    [TestMethod]
    public void Deliver_ToAMailboxARuleNames_AnswersQuotaExceededPublishesTheFaultAndStoresNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var store = FaultedStore(events, new ScriptedFaultPlan(AnnOutOfSpace));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, Deliver(store, "hello", "<ann@x>"));

        CollectionAssert.AreEqual(
            new SurlEvent[] { new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "ann/INBOX") },
            events.Events.ToArray());
        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public void Deliver_ToAnotherMailbox_Succeeds()
    {
        var events = new RecordingSurlEventPublisher();
        var store = FaultedStore(events, new ScriptedFaultPlan(AnnOutOfSpace));

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hello", "<bob@x>"));

        Assert.IsInstanceOfType<StoreChangedEvent>(events.Events.Single());
        Assert.HasCount(1, Uids(store, store.ViewFor("bob"), "INBOX"));
    }

    [TestMethod]
    public void Deliver_ToTwoOwnersOneOfThemFaulted_StoresForNeither()
    {
        var store = FaultedStore(new RecordingSurlEventPublisher(), new ScriptedFaultPlan(AnnOutOfSpace));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, Deliver(store, "hello", "<bob@x>", "<ann@x>"));

        Assert.IsEmpty(Uids(store, store.ViewFor("bob"), "INBOX"));
    }

    [TestMethod]
    public void Deliver_AsksTheStoreWriteStageWithTheOwnerAsAccountAndTheMailboxAsPath()
    {
        var plan = new ScriptedFaultPlan();
        var store = FaultedStore(new RecordingSurlEventPublisher(), plan);

        Deliver(store, "hello", "<ann@x>", "<ann@x>", "<bob@x>");

        CollectionAssert.AreEqual(
            new[]
            {
                new FaultPoint(FaultStage.StoreWrite) { Account = "ann", Path = "INBOX" },
                new FaultPoint(FaultStage.StoreWrite) { Account = "bob", Path = "INBOX" },
            },
            plan.Points.ToArray());
    }

    [TestMethod]
    public void Deliver_ARuleWithTimesOne_FiresOnce()
    {
        var events = new RecordingSurlEventPublisher();
        var store = FaultedStore(events, new SpendingFaultPlan(AnnOutOfSpace with { Times = 1 }));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, Deliver(store, "one", "<ann@x>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "two", "<ann@x>"));

        Assert.HasCount(1, events.Events.OfType<FaultAppliedEvent>());
        Assert.HasCount(1, Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public void Append_ToAMailboxARuleNames_AnswersQuotaExceededAndPublishesTheFault()
    {
        var events = new RecordingSurlEventPublisher();
        var rule = new FaultRule("f2", FaultKind.OutOfSpace) { Path = "Work" };
        var store = FaultedStore(events, new ScriptedFaultPlan(rule));
        var view = store.ViewFor("ann");
        store.CreateMailbox(view, "Work");

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Append(view, "Work", "x"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, "INBOX", "x"u8, MailFlags.None, null, out _));

        Assert.AreEqual(new FaultAppliedEvent("f2", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "ann/Work"), events.Events[1]);
        Assert.IsEmpty(Uids(store, view, "Work"));
    }

    [TestMethod]
    public void Append_ToAMissingMailbox_AsksNothingAndAnswersMailboxMissing()
    {
        var plan = new ScriptedFaultPlan(AnnOutOfSpace);
        var store = FaultedStore(new RecordingSurlEventPublisher(), plan);

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Append(store.ViewFor("ann"), "Missing", "x"u8, MailFlags.None, null, out _));

        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public void Append_TooLarge_AnswersMessageTooLargeBeforeAskingThePlan()
    {
        var plan = new ScriptedFaultPlan(AnnOutOfSpace);
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), maxMessageBytes: 1, faults: plan);

        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, store.Append(store.ViewFor("ann"), "INBOX", "xy"u8, MailFlags.None, null, out _));

        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public void Deliver_ARuleOfAnotherKind_IsNotApplied()
    {
        var events = new RecordingSurlEventPublisher();
        var store = FaultedStore(events, new ScriptedFaultPlan(new FaultRule("f3", FaultKind.Error) { Account = "ann" }));

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hello", "<ann@x>"));

        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public void Deliver_APlanWhoseAnswerIsPending_IsServedAsIfNoRuleMatched()
    {
        var answer = new TaskCompletionSource<FaultRule?>();
        var store = FaultedStore(new RecordingSurlEventPublisher(), new AnsweringLaterFaultPlan(answer.Task));

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hello", "<ann@x>"));

        answer.SetException(new InvalidOperationException("the hook threw"));
        Assert.HasCount(1, Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public void Deliver_APlanThatFailsAtOnce_IsServedAsIfNoRuleMatched()
    {
        var store = FaultedStore(
            new RecordingSurlEventPublisher(),
            new AnsweringLaterFaultPlan(Task.FromException<FaultRule?>(new InvalidOperationException("the hook threw"))));

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hello", "<ann@x>"));
    }

    [TestMethod]
    public async Task DeliverAsync_APlanThatAnswersLaterWithOutOfSpace_AnswersQuotaExceededAndPublishesTheFault()
    {
        var answer = new TaskCompletionSource<FaultRule?>();
        var events = new RecordingSurlEventPublisher();
        var store = FaultedStore(events, new AnsweringLaterFaultPlan(answer.Task));

        var pending = store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "hi"u8), CancellationToken.None);
        Assert.IsFalse(pending.IsCompleted);
        answer.SetResult(AnnOutOfSpace);

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, await pending);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "ann/INBOX"), events.Events.Single());
        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public async Task DeliverAsync_APlanThatAnswersLaterWithNoRule_Stores()
    {
        var store = FaultedStore(new RecordingSurlEventPublisher(), new AnsweringLaterFaultPlan(Task.FromResult<FaultRule?>(null)));

        Assert.AreEqual(MailStoreOutcome.Succeeded, await store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "hi"u8), CancellationToken.None));
        Assert.HasCount(1, Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public async Task DeliverAsync_APlanThatThrows_IsServedAsIfNoRuleMatched()
    {
        var store = FaultedStore(
            new RecordingSurlEventPublisher(),
            new AnsweringLaterFaultPlan(Task.FromException<FaultRule?>(new InvalidOperationException("the hook threw"))));

        Assert.AreEqual(MailStoreOutcome.Succeeded, await store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "hi"u8), CancellationToken.None));
    }

    [TestMethod]
    public async Task DeliverAsync_ARuleOfAnotherKind_IsNotApplied()
    {
        var store = FaultedStore(
            new RecordingSurlEventPublisher(),
            new AnsweringLaterFaultPlan(Task.FromResult<FaultRule?>(new FaultRule("f3", FaultKind.Error))));

        Assert.AreEqual(MailStoreOutcome.Succeeded, await store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "hi"u8), CancellationToken.None));
    }

    [TestMethod]
    public async Task DeliverAsync_ACancelledPlan_ThrowsAndStoresNothing()
    {
        var store = FaultedStore(
            new RecordingSurlEventPublisher(),
            new AnsweringLaterFaultPlan(Task.FromCanceled<FaultRule?>(new CancellationToken(canceled: true))));

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "hi"u8), CancellationToken.None));

        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public async Task DeliverAsync_WithNoRecipients_AsksNothing()
    {
        var plan = new ScriptedFaultPlan(AnnOutOfSpace);
        var store = FaultedStore(new RecordingSurlEventPublisher(), plan);

        Assert.AreEqual(MailStoreOutcome.Succeeded, await store.DeliverAsync([], Pending(store, "hi"u8), CancellationToken.None));
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task DeliverAsync_TooLarge_AnswersMessageTooLargeBeforeAskingThePlan()
    {
        var plan = new ScriptedFaultPlan(AnnOutOfSpace);
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), maxMessageBytes: 1, faults: plan);

        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, await store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "xy"u8), CancellationToken.None));
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task DeliverAsync_NullArguments_Throw()
    {
        var store = FaultedStore(new RecordingSurlEventPublisher(), new ScriptedFaultPlan());

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await store.DeliverAsync(null!, Pending(store, "x"u8), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await store.DeliverAsync([], null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task AppendAsync_APlanThatAnswersLaterWithOutOfSpace_AnswersQuotaExceededAndPublishesTheFault()
    {
        var answer = new TaskCompletionSource<FaultRule?>();
        var events = new RecordingSurlEventPublisher();
        var store = FaultedStore(events, new AnsweringLaterFaultPlan(answer.Task));

        var pending = store.AppendAsync(store.ViewFor("ann"), "INBOX", Pending(store, "x"u8), MailFlags.None, null, CancellationToken.None);
        Assert.IsFalse(pending.IsCompleted);
        answer.SetResult(new FaultRule("f2", FaultKind.OutOfSpace));

        var result = await pending;
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, result.Outcome);
        Assert.AreEqual(default, result.Stored);
        Assert.AreEqual(new FaultAppliedEvent("f2", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "ann/INBOX"), events.Events.Single());
        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public async Task AppendAsync_APlanThatAnswersNoRule_StoresAndGivesWhereItWentToo()
    {
        var store = FaultedStore(new RecordingSurlEventPublisher(), new AnsweringLaterFaultPlan(Task.FromResult<FaultRule?>(null)));
        var view = store.ViewFor("ann");

        var result = await store.AppendAsync(view, "INBOX", Pending(store, "x"u8), MailFlags.None, null, CancellationToken.None);

        Assert.AreEqual(MailStoreOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(Uids(store, view, "INBOX").Single(), result.Stored.Uid);
    }

    [TestMethod]
    public async Task AppendAsync_APlanThatThrows_IsServedAsIfNoRuleMatched()
    {
        var store = FaultedStore(
            new RecordingSurlEventPublisher(),
            new AnsweringLaterFaultPlan(Task.FromException<FaultRule?>(new InvalidOperationException("the hook threw"))));

        var result = await store.AppendAsync(store.ViewFor("ann"), "INBOX", Pending(store, "x"u8), MailFlags.None, null, CancellationToken.None);

        Assert.AreEqual(MailStoreOutcome.Succeeded, result.Outcome);
    }

    [TestMethod]
    public async Task AppendAsync_ToAMissingMailbox_AsksNothingAndAnswersMailboxMissing()
    {
        var plan = new ScriptedFaultPlan(AnnOutOfSpace);
        var store = FaultedStore(new RecordingSurlEventPublisher(), plan);

        var result = await store.AppendAsync(store.ViewFor("ann"), "Missing", Pending(store, "x"u8), MailFlags.None, null, CancellationToken.None);

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, result.Outcome);
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task AppendAsync_TooLarge_AnswersMessageTooLargeBeforeAskingThePlan()
    {
        var plan = new ScriptedFaultPlan(AnnOutOfSpace);
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), maxMessageBytes: 1, faults: plan);

        var result = await store.AppendAsync(store.ViewFor("ann"), "INBOX", Pending(store, "xy"u8), MailFlags.None, null, CancellationToken.None);

        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, result.Outcome);
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task AppendAsync_ABadFlagOrNoMessage_Throws()
    {
        var store = FaultedStore(new RecordingSurlEventPublisher(), new ScriptedFaultPlan());
        var view = store.ViewFor("ann");

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await store.AppendAsync(view, "INBOX", null!, MailFlags.None, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await store.AppendAsync(view, "INBOX", Pending(store, "x"u8), unchecked((MailFlags)int.MaxValue), null, CancellationToken.None));
    }

    private static MailboxStore FaultedStore(ISurlEventPublisher events, IFaultPlan faults) =>
        new(["ann", "bob"], allowAnonymous: false, new SettableTimeProvider(), events: events, faults: faults);

    /// <summary>
    /// A fault plan whose every answer is the given task, as a plan waiting on a hook gives.
    /// </summary>
    private sealed class AnsweringLaterFaultPlan(Task<FaultRule?> answer) : IFaultPlan
    {
        public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken) => new(answer);
    }
}
