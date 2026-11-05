using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// How <see cref="MailboxStore"/> answers an out-of-space fault rule (ADR-0124 decision 4;
/// ADR-0126).
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
