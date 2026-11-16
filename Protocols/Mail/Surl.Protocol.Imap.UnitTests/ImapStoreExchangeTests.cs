using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// The IMAP server hands its exchange to the mail store (ADR-0168, decision 5), so a rule scoped by
/// listener fires for its writes and the store changes it publishes name the exchange.
/// </summary>
[TestClass]
public sealed class ImapStoreExchangeTests
{
    private const string Append = "a LOGIN u p\r\nb APPEND INBOX {5}\r\nhello\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("imap://127.0.0.1:18143/", true)]
    [DataRow("imap://127.0.0.1:18144/", false)]
    public async Task ServeAsync_OutOfSpaceRuleScopedToAListener_FiresOnlyForThatListener(string listener, bool isRefused)
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = listener });
        var store = new MailboxStore([], allowAnonymous: true, clock, events: events, faults: plan);

        await ServeAsync(Server(store), Append, clock, TestContext.CancellationToken);

        Assert.AreEqual(isRefused ? 1 : 0, events.Events.OfType<FaultAppliedEvent>().Count());
    }

    [TestMethod]
    public async Task ServeAsync_Append_PublishesTheStoreChangeNamingTheExchange()
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore([], allowAnonymous: true, clock, events: events);

        await ServeAsync(Server(store), Append, clock, TestContext.CancellationToken);

        Assert.IsTrue(events.Events.OfType<StoreChangedEvent>().Any(change => change.ExchangeId == 1L));
    }
}
