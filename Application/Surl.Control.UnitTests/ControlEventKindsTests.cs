using Surl.Protocol.Abstractions;

namespace Surl.Control;

[TestClass]
public sealed class ControlEventKindsTests
{
    [TestMethod]
    public void ExchangeIdOf_EachKind_IsItsExchangeOrNull()
    {
        SurlEvent[] events =
        [
            new ExchangeStartedEvent(1, "http://127.0.0.1:80", "http", ControlHarness.LoopbackPeer),
            new ExchangeBytesEvent(2, ExchangeDirection.Sent, Array.Empty<byte>()),
            new ExchangeEndedEvent(3, 0, 0),
            new LoginJudgedEvent(4, "smtp", "alice", "PLAIN", IsAccepted: true, IsRefusedByFault: false),
            new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "INBOX", 1, 5),
            new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Accept, 6, "smtp://127.0.0.1:25"),
            new HookFailedEvent("h1", HookFailure.Threw, "x"),
            new ConsumerGroupChangedEvent(7, "kafka", "orders", ConsumerGroupChange.MemberJoined, "c-1", 1, "alice"),
            new ConsumerGroupChangedEvent(null, "kafka", "orders", ConsumerGroupChange.MemberRemoved, "c-1", 1, null),
        ];

        var exchangeIds = events.Select(ControlEventKinds.ExchangeIdOf).ToArray();

        CollectionAssert.AreEqual(new long?[] { 1, 2, 3, 4, 5, 6, null, 7, null }, exchangeIds);
    }

    [TestMethod]
    public void AccountOf_LoginAccountChangeAndOtherStoreChange_IsTheAccountOrNull()
    {
        var login = new LoginJudgedEvent(null, "smtp", "alice", "PLAIN", IsAccepted: true, IsRefusedByFault: false);
        var accountAdded = new StoreChangedEvent(StoreKind.Accounts, StoreChange.AccountAdded, "bob", null, null);
        var fileWritten = new StoreChangedEvent(StoreKind.Content, StoreChange.Written, "/bob", 1, null);

        Assert.AreEqual("alice", ControlEventKinds.AccountOf(login));
        Assert.AreEqual("bob", ControlEventKinds.AccountOf(accountAdded));
        Assert.IsNull(ControlEventKinds.AccountOf(fileWritten));
    }

    [TestMethod]
    public void Known_ConsumerGroupChangedEvent_IsConsumerGroupChanged()
    {
        var changed = new ConsumerGroupChangedEvent(7, "kafka", "orders", ConsumerGroupChange.MemberJoined, "c-1", 1, "alice");

        var kind = ControlEventKinds.NameOf(changed);

        Assert.AreEqual("consumer_group_changed", kind);
        Assert.Contains("consumer_group_changed", ControlEventKinds.Known);
    }

    [TestMethod]
    public void AccountOf_ConsumerGroupChange_IsItsAccountOrNull()
    {
        var joined = new ConsumerGroupChangedEvent(7, "kafka", "orders", ConsumerGroupChange.MemberJoined, "c-1", 1, "alice");
        var removed = new ConsumerGroupChangedEvent(null, "kafka", "orders", ConsumerGroupChange.MemberRemoved, "c-1", 1, null);

        Assert.AreEqual("alice", ControlEventKinds.AccountOf(joined));
        Assert.IsNull(ControlEventKinds.AccountOf(removed));
    }
}
