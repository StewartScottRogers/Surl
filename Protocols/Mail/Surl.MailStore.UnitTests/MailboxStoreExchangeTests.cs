using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// How <see cref="MailboxStore"/> uses the exchange behind a write (ADR-0168 decisions 2 to 4):
/// an out-of-space rule scoped by <c>listener</c> or <c>scheme</c> matches, and every event names
/// the exchange.
/// </summary>
[TestClass]
public sealed class MailboxStoreExchangeTests
{
    private const long ExchangeId = 42;

    private static readonly ExchangeContext Exchange = new(
        ExchangeId,
        new ListenUrl("smtp", "127.0.0.1", 25),
        new IPEndPoint(IPAddress.Loopback, 25),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        TimeProvider.System,
        CancellationToken.None);

    [TestMethod]
    public void Deliver_WithAnExchange_FiresAListenerScopedRuleAndNamesTheExchangeOnTheFault()
    {
        var events = new RecordingSurlEventPublisher();
        var store = Store(events, new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = ReportedListenUrl.Format(Exchange.ListenUrl) }));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Deliver([Recipient(store, "<ann@x>")], Pending(store, "hi"u8), Exchange));

        Assert.AreEqual(
            new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, ExchangeId, "ann/INBOX"),
            events.Events.Single());
    }

    [TestMethod]
    public void Append_WithAnExchange_FiresASchemeScopedRule()
    {
        var events = new RecordingSurlEventPublisher();
        var store = Store(events, new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Scheme = "smtp" }));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Append(store.ViewFor("ann"), "INBOX", "hi"u8, MailFlags.None, null, out _, Exchange));
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Append(store.ViewFor("ann"), "INBOX", Pending(store, "hi"u8), MailFlags.None, null, out _, Exchange));

        Assert.HasCount(2, events.Events.OfType<FaultAppliedEvent>().Where(applied => applied.ExchangeId == ExchangeId));
    }

    [TestMethod]
    public async Task DeliverAsyncAndAppendAsync_WithAnExchange_FireListenerAndSchemeScopedRules()
    {
        var events = new RecordingSurlEventPublisher();
        var store = Store(events, new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Scheme = "smtp", Listener = ReportedListenUrl.Format(Exchange.ListenUrl) }));

        Assert.AreEqual(
            MailStoreOutcome.QuotaExceeded,
            await store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "hi"u8), CancellationToken.None, Exchange));
        var appended = await store.AppendAsync(store.ViewFor("ann"), "INBOX", Pending(store, "hi"u8), MailFlags.None, null, CancellationToken.None, Exchange);

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, appended.Outcome);
        Assert.HasCount(2, events.Events.OfType<FaultAppliedEvent>().Where(applied => applied.ExchangeId == ExchangeId));
    }

    [TestMethod]
    public void Deliver_WithoutAnExchange_DoesNotFireAScopedRule_AndPublishesNoExchangeId()
    {
        var events = new RecordingSurlEventPublisher();
        var store = Store(events, new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Scheme = "smtp" }));

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hi", "<ann@x>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Seed("<ann@x>", "hi"u8).Delivery);

        Assert.IsTrue(events.Events.OfType<StoreChangedEvent>().All(change => change.ExchangeId is null));
        Assert.HasCount(2, events.Events);
    }

    [TestMethod]
    public void EveryPublishingMutator_WithAnExchange_PublishesItsIdOnEveryEvent()
    {
        var events = new RecordingSurlEventPublisher();
        var store = Store(events, new ScriptedFaultPlan());
        var view = store.ViewFor("ann");

        store.Deliver([Recipient(store, "<ann@x>")], Pending(store, "one"u8), Exchange);
        store.CreateMailbox(view, "Work", Exchange);
        store.Append(view, "Work", "four"u8, MailFlags.None, null, out _, Exchange);
        store.Copy(view, "INBOX", [1], "Work", out _, Exchange);
        store.ChangeFlags(view, "Work", 1, MailFlagChange.Add, MailFlags.Deleted, out _, Exchange);
        store.Expunge(view, "Work", out _, Exchange);
        store.ChangeFlags(view, "Work", 2, MailFlagChange.Add, MailFlags.Deleted, out _, Exchange);
        store.Expunge(view, "Work", [2], out _, Exchange);
        store.Move(view, "INBOX", [1], "Work", out _, Exchange);
        store.RenameMailbox(view, "Work", "Play", Exchange);
        store.RenameMailbox(view, "INBOX", "Kept", Exchange);
        store.DeleteMailbox(view, "Play", Exchange);

        var changes = events.Events.OfType<StoreChangedEvent>().ToList();
        Assert.IsGreaterThan(10, changes.Count);
        Assert.IsTrue(changes.All(change => change.ExchangeId == ExchangeId));
        Assert.IsTrue(changes.Any(change => change.ByteCount is null), "a mailbox change");
        Assert.IsTrue(changes.Any(change => change.ByteCount is not null), "a message change");
    }

    [TestMethod]
    public async Task AppendAsync_AndDeliverAsync_WithAnExchange_PublishTheIdOnTheirChanges()
    {
        var events = new RecordingSurlEventPublisher();
        var store = Store(events, new ScriptedFaultPlan());

        await store.DeliverAsync([Recipient(store, "<ann@x>")], Pending(store, "one"u8), CancellationToken.None, Exchange);
        await store.AppendAsync(store.ViewFor("ann"), "INBOX", Pending(store, "two"u8), MailFlags.None, null, CancellationToken.None, Exchange);

        Assert.HasCount(2, events.Events);
        Assert.IsTrue(events.Events.OfType<StoreChangedEvent>().All(change => change.ExchangeId == ExchangeId));
    }

    [TestMethod]
    public void MaildropRemoveMessages_WithAnExchange_PublishesTheIdOnTheDeletion()
    {
        var events = new RecordingSurlEventPublisher();
        var store = Store(events, new ScriptedFaultPlan());
        Deliver(store, "one", "<ann@x>");
        store.LockMaildrop(store.ViewFor("ann"), out var maildrop);

        using (maildrop)
        {
            Assert.AreEqual(1, maildrop!.RemoveMessages([1], Exchange));
        }

        Assert.AreEqual(ExchangeId, ((StoreChangedEvent)events.Events[^1]).ExchangeId);
    }

    private static MailboxStore Store(ISurlEventPublisher events, IFaultPlan faults) =>
        new(["ann"], allowAnonymous: false, new SettableTimeProvider(), events: events, faults: faults);
}
