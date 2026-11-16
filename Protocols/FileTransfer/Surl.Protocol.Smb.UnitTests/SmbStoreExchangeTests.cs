using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// The exchange the SMB server hands the content store (ADR-0168 decision 5): an out-of-space
/// rule scoped by listener fires for its write, and its store change names the exchange.
/// </summary>
[TestClass]
public sealed class SmbStoreExchangeTests
{
    private static readonly byte[] Upload = "upload me\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    private static async Task<(RecordingSurlEventPublisher Events, InMemoryContentFileSystem FileSystem)> UploadAsync(FaultRule? rule, CancellationToken cancellationToken)
    {
        var events = new RecordingSurlEventPublisher();
        var fileSystem = StandardFileSystem();
        var store = new ContentStore(
            InMemoryContentFileSystem.RootPath,
            fileSystem,
            new ContentExposureOptions { AllowUploads = true },
            [],
            [],
            events,
            rule is null ? null : new ScriptedFaultPlan(rule));

        await ServeConnectedAsync(store, cancellationToken, null, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), CloseHex(1, 1));
        return (events, fileSystem);
    }

    [TestMethod]
    public async Task Upload_UnderAnOutOfSpaceRuleForThisListenUrl_RunsOutOfSpaceAndStoresNothing()
    {
        var rule = new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "smb://127.0.0.1:445/" };

        var (events, fileSystem) = await UploadAsync(rule, TestContext.CancellationToken);

        Assert.AreEqual(1, events.Events.OfType<FaultAppliedEvent>().Count());
        Assert.IsNull(StoredBytes(fileSystem, "share", "up.txt"));
    }

    [TestMethod]
    public async Task Upload_UnderAnOutOfSpaceRuleForAnotherListenUrl_IsStored()
    {
        var rule = new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "smb://127.0.0.1:9999/" };

        var (events, fileSystem) = await UploadAsync(rule, TestContext.CancellationToken);

        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>().ToArray());
        CollectionAssert.AreEqual(Upload, StoredBytes(fileSystem, "share", "up.txt"));
    }

    [TestMethod]
    public async Task Upload_PublishesTheStoreChangeNamingTheExchange()
    {
        var (events, _) = await UploadAsync(null, TestContext.CancellationToken);

        var change = events.Events.OfType<StoreChangedEvent>().Single(e => e.Change == StoreChange.Written);
        Assert.AreEqual(1L, change.ExchangeId);
    }
}
