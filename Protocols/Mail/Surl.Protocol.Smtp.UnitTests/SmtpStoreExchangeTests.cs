using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The SMTP server hands its exchange to the mail store (ADR-0168, decision 5), so a rule scoped
/// by listener fires for its writes and the store changes it publishes name the exchange.
/// </summary>
[TestClass]
public sealed class SmtpStoreExchangeTests
{
    private const string Request = "HELO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nhi\r\n.\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("smtp://127.0.0.1:18025/", true)]
    [DataRow("smtp://127.0.0.1:18026/", false)]
    public async Task Data_OutOfSpaceRuleScopedToAListener_FiresOnlyForThatListener(string listener, bool isRefused)
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = listener });

        var written = await ServeAsync(events, plan);

        Assert.AreEqual(isRefused, written.Contains(SmtpReplies.MailboxFull, StringComparison.Ordinal), written);
        Assert.AreEqual(isRefused ? 1 : 0, events.Events.OfType<FaultAppliedEvent>().Count());
    }

    [TestMethod]
    public async Task Data_Delivered_PublishesTheStoreChangeNamingTheExchange()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeAsync(events, new ScriptedFaultPlan());

        var change = events.Events.OfType<StoreChangedEvent>().Single();
        Assert.AreEqual(1L, change.ExchangeId);
    }

    private async Task<string> ServeAsync(RecordingSurlEventPublisher events, IFaultPlan plan)
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore([], allowAnonymous: true, clock, events: events, faults: plan);
        var connection = new InMemoryConnection(Ascii(Request));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken) with { Faults = plan, Events = events });

        return System.Text.Encoding.ASCII.GetString(connection.WrittenBytes.ToArray());
    }
}
