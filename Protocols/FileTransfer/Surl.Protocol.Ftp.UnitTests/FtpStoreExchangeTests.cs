using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;
using static Surl.Protocol.Ftp.FtpUploadTests;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The FTP server hands its exchange to the content store (ADR-0168, decision 5), so a rule scoped
/// by listener fires for its writes and the store changes it publishes name the exchange.
/// </summary>
[TestClass]
public sealed class FtpStoreExchangeTests
{
    private const string Listen = "ftp://127.0.0.1:2121/";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(Listen, true)]
    [DataRow("ftp://127.0.0.1:2122/", false)]
    public async Task Stor_OutOfSpaceRuleScopedToAListener_FiresOnlyForThatListener(string listener, bool isRefused)
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = listener });

        var written = await ServeUploadAsync(events, plan);

        Assert.AreEqual(isRefused, written.Contains("552 Upload exceeds the size limit", StringComparison.Ordinal), written);
        Assert.AreEqual(isRefused ? 1 : 0, events.Events.OfType<FaultAppliedEvent>().Count());
    }

    [TestMethod]
    public async Task Stor_Written_PublishesTheStoreChangeNamingTheExchange()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeUploadAsync(events, new FaultPlan());

        var change = events.Events.OfType<StoreChangedEvent>().Single(e => e.Change == StoreChange.Written);
        Assert.AreEqual(1L, change.ExchangeId);
    }

    private async Task<string> ServeUploadAsync(RecordingSurlEventPublisher events, FaultPlan plan)
    {
        var fileSystem = StandardFileSystem();
        var store = new ContentStore(
            InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true }, [], [], events, plan);
        var data = new InMemoryDataConnections().ScriptPassiveListener(
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 50100), new InMemoryConnection(Ascii("body\n")));
        var connection = new InMemoryConnection(Ascii(AnonymousLogin + "EPSV\r\nSTOR b.txt\r\n"));
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: data) with { Faults = plan, Events = events };

        await Server(contentStore: store).ServeAsync(connection, context);

        return Text(connection.WrittenBytes);
    }
}
