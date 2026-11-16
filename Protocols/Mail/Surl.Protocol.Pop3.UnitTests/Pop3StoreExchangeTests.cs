using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The POP3 server hands its exchange to the mail store (ADR-0168, decision 5), so the store
/// changes it publishes name the exchange. POP3's only store write is the removal at <c>QUIT</c>,
/// which frees space, so an out-of-space rule never refuses it, whatever its scope.
/// </summary>
[TestClass]
public sealed class Pop3StoreExchangeTests
{
    private const string DeleteAndQuit = Login + "DELE 1\r\nQUIT\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_OutOfSpaceRuleScopedToThisListener_DoesNotRefuseTheRemovalAndTheStoreChangeNamesTheExchange()
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = "pop3://127.0.0.1:18110/" });
        var store = Seed(new MailboxStore(["u"], allowAnonymous: false, clock, events: events, faults: plan), "u@x", [Message]);

        await ServeAsync(store, DeleteAndQuit, clock, TestContext.CancellationToken);

        Assert.AreEqual(0, events.Events.OfType<FaultAppliedEvent>().Count());
        Assert.IsTrue(events.Events.OfType<StoreChangedEvent>().Any(change => change.ExchangeId == 1L));
    }
}
