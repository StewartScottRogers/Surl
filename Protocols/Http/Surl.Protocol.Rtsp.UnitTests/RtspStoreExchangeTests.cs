using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The RTSP server hands its exchange to the content store (ADR-0168, decision 5), so a rule scoped
/// by listener fires for its writes and the store changes it publishes name the exchange.
/// </summary>
[TestClass]
public sealed class RtspStoreExchangeTests
{
    private const string Announce = "ANNOUNCE rtsp://127.0.0.1:18554/rec.bin RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 5\r\nContent-Type: application/sdp\r\n\r\nv=0\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("rtsp://127.0.0.1:18554/", true)]
    [DataRow("rtsp://127.0.0.1:18555/", false)]
    public async Task Announce_OutOfSpaceRuleScopedToAListener_FiresOnlyForThatListener(string listener, bool isRefused)
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = listener });

        var (_, fired) = await ServeAnnounceAsync(events, plan);

        Assert.AreEqual(isRefused ? 1 : 0, fired);
    }

    [TestMethod]
    public async Task Announce_Written_PublishesTheStoreChangeNamingTheExchange()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeAnnounceAsync(events, new FaultPlan());

        var change = events.Events.OfType<StoreChangedEvent>().Single(e => e.Change == StoreChange.Written);
        Assert.AreEqual(1L, change.ExchangeId);
    }

    private async Task<(InMemoryConnection Connection, int Fired)> ServeAnnounceAsync(RecordingSurlEventPublisher events, FaultPlan plan)
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(Root);
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions { AllowUploads = true }, [], [], events, plan);
        var server = new RtspProtocolServer(store, new AnonymousAuthenticationPolicy(), new PatternRandomNumberGenerator());
        var connection = new InMemoryConnection([Ascii(Announce)]);
        var context = Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken) with { Faults = plan, Events = events };

        await server.ServeAsync(connection, context);

        return (connection, events.Events.OfType<FaultAppliedEvent>().Count());
    }
}
