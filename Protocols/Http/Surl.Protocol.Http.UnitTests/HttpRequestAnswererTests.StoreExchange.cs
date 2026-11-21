using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The answerer hands its exchange to the content store (ADR-0168 decision 5), so a scoped
/// out-of-space rule matches a <c>PUT</c> and its store change names the exchange.
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    [TestMethod]
    public async Task AnswerAsync_PutWithOutOfSpaceRuleForThisListener_RefusesTheWriteAndPublishesTheFault()
    {
        var events = new RecordingSurlEventPublisher();
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "http://127.0.0.1:18050/" });
        var (fileSystem, store) = UploadStoreObserved(events, faults);

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(Encoding.ASCII.GetBytes("abc"))), TestContext.CancellationToken);

        Assert.AreNotEqual(201, answer.Status.Code);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(UploadRoot, "up.bin")));
        Assert.AreEqual(1L, events.Events.OfType<FaultAppliedEvent>().Single().ExchangeId);
    }

    [TestMethod]
    public async Task AnswerAsync_PutRefusedByOutOfSpaceRule_Answers413AndNamesTheRuleInTheNote()
    {
        var log = new RecordingExchangeLog();
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "http://127.0.0.1:18050/" });
        var (_, store) = UploadStoreObserved(new RecordingSurlEventPublisher(), faults);

        var answer = await UploadAnswerer(store, log: log).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(Encoding.ASCII.GetBytes("abc"))), TestContext.CancellationToken);

        Assert.AreEqual(413, answer.Status.Code);
        Assert.Contains("PUT /up.bin: the upload was refused by the fault rule f1 (out-of-space); answered 413 and closed.", log.Notes);
    }

    [TestMethod]
    public async Task AnswerAsync_PutPastTheUploadLimit_Answers413AndNamesTheLimitInTheNote()
    {
        var log = new RecordingExchangeLog();
        var (_, store) = UploadStore(maxUploadBytes: 2);

        var answer = await UploadAnswerer(store, log: log).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(Encoding.ASCII.GetBytes("abc"))), TestContext.CancellationToken);

        Assert.AreEqual(413, answer.Status.Code);
        Assert.Contains("PUT /up.bin: the body went past the upload limit of 2 bytes; answered 413 and closed.", log.Notes);
    }

    [TestMethod]
    public async Task AnswerAsync_PutWithOutOfSpaceRuleForAnotherListener_StoresAndPublishesTheChangeOnTheExchange()
    {
        var events = new RecordingSurlEventPublisher();
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "http://127.0.0.1:1/" });
        var (_, store) = UploadStoreObserved(events, faults);

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(Encoding.ASCII.GetBytes("abc"))), TestContext.CancellationToken);

        Assert.AreEqual(201, answer.Status.Code);
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>());
        Assert.AreEqual(1L, events.Events.OfType<StoreChangedEvent>().Single().ExchangeId);
    }

    private static (InMemoryContentFileSystem FileSystem, ContentStore Store) UploadStoreObserved(ISurlEventPublisher events, IFaultPlan faults)
    {
        var fileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(Now));

        return (fileSystem, new ContentStore(UploadRoot, fileSystem, new ContentExposureOptions { AllowUploads = true }, [], [], events, faults));
    }
}
