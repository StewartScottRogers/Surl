using System.Net;
using System.Text;
using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Gopher.GopherTestExchange;

namespace Surl.Protocol.Gopher;

/// <summary>
/// The Gopher server hands its exchange to the content store (ADR-0168, decision 5). Gopher only
/// reads, so no write rule can fire for it and no store change is published: a read under an
/// out-of-space rule scoped to its listener is served untouched.
/// </summary>
[TestClass]
public sealed class GopherStoreExchangeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Selector_OutOfSpaceRuleScopedToTheListener_ServesTheFileAndAppliesNoFaultOrStoreChange()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = "gopher://127.0.0.1:18634/" });
        var fileSystem = FileSystemWithDotFiles();
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions(), [], [], events, plan);
        var connection = new InMemoryConnection(RecordedFixture.Whole(Ascii("/notes.txt\r\n")));
        var context = new ExchangeContext(
            1,
            new ListenUrl("gopher", "127.0.0.1", 18634).WithBoundPort(18634),
            new IPEndPoint(IPAddress.Loopback, 18634),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            TimeProvider.System,
            TestContext.CancellationToken);

        await new GopherProtocolServer(store).ServeAsync(connection, context);

        Assert.IsFalse(events.Events.OfType<FaultAppliedEvent>().Any());
        Assert.IsFalse(events.Events.OfType<StoreChangedEvent>().Any());
        Assert.IsGreaterThan(0, connection.WrittenBytes.Length);
    }
}
