using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly ContentExposureOptions UploadsOn = new() { AllowUploads = true };

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Changes_EveryKindOfChange_PublishesOneEventEachWithItsPathAndSizeInOrder(bool inMemory)
    {
        var events = new RecordingSurlEventPublisher();
        ContentStore store = EventStore(inMemory, events, faults: null);

        Assert.AreEqual(ContentChangeResult.Done, store.CreateDirectory(store.MapRequestPath("/uploads")));
        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPath("/uploads/a.txt"), Bytes("hello"), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPath("/uploads/a.txt"), Bytes("hi"), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.Written, await store.AppendUploadAsync(store.MapRequestPath("/uploads/a.txt"), Bytes("!"), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntry(store.MapRequestPath("/uploads/a.txt"), store.MapRequestPath("/uploads/b.txt")));
        Assert.AreEqual(ContentUploadResult.Written, await CommitSessionAsync(store, "/uploads/c.txt", "abcd"));
        Assert.AreEqual(ContentChangeResult.Done, store.DeleteFile(store.MapRequestPath("/uploads/b.txt")));
        Assert.AreEqual(ContentChangeResult.Done, store.DeleteFile(store.MapRequestPath("/uploads/c.txt")));
        Assert.AreEqual(ContentChangeResult.Done, store.RemoveEmptyDirectory(store.MapRequestPath("/uploads")));

        SurlEvent[] expected =
        [
            Changed(StoreChange.Created, "/uploads", null),
            Changed(StoreChange.Written, "/uploads/a.txt", 5),
            Changed(StoreChange.Written, "/uploads/a.txt", 2),
            Changed(StoreChange.Written, "/uploads/a.txt", 3),
            Changed(StoreChange.Renamed, "/uploads/b.txt", null) with { PreviousName = "/uploads/a.txt" },
            Changed(StoreChange.Written, "/uploads/c.txt", 4),
            Changed(StoreChange.Deleted, "/uploads/b.txt", null),
            Changed(StoreChange.Deleted, "/uploads/c.txt", null),
            Changed(StoreChange.Deleted, "/uploads", null),
        ];
        CollectionAssert.AreEqual(expected, events.Events.ToArray());
    }

    [TestMethod]
    public async Task WriteUploadAsync_InAShare_PublishesTheNameWithTheSharesFirst()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new ContentStore(Root, new UnitTestInMemoryContentFileSystem().AddDirectory(Root), UploadsOn, [new ContentShare("team", MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System))], [], events);

        await store.WriteUploadAsync(store.MapRequestPathForScheme("/team/x.txt", "ftp"), Bytes("xy"), CancellationToken.None);

        CollectionAssert.AreEqual(new SurlEvent[] { Changed(StoreChange.Written, "/team/x.txt", 2) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task Changes_RefusedOrTooLarge_PublishNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var fileSystem = new UnitTestInMemoryContentFileSystem().AddDirectory(Root);
        var store = new ContentStore(Root, fileSystem, UploadsOn with { MaxUploadBytes = 2 }, [], [], events);

        await store.WriteUploadAsync(store.MapRequestPath("/big.txt"), Bytes("too big"), CancellationToken.None);
        store.DeleteFile(store.MapRequestPath("/missing.txt"));
        store.RemoveEmptyDirectory(store.MapRequestPath("/missing"));

        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Upload_OutOfSpaceRuleForUploads_IsTooLargeAndPublishesTheFaultWhileElsewhereIsWritten(bool appending)
    {
        var events = new RecordingSurlEventPublisher();
        var faults = new UnitTestPathPrefixFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "/uploads" });
        ContentStore store = EventStore(inMemory: true, events, faults);
        store.CreateDirectory(store.MapRequestPath("/uploads"));

        ContentUploadResult refused = await UploadAsync(store, "/uploads/a.txt", appending);
        ContentUploadResult written = await UploadAsync(store, "/other.txt", appending);

        Assert.AreEqual(ContentUploadResult.TooLarge, refused);
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(store.MapRequestPath("/uploads/a.txt")));
        Assert.AreEqual(ContentUploadResult.Written, written);
        SurlEvent[] expected =
        [
            Changed(StoreChange.Created, "/uploads", null),
            new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "/uploads/a.txt"),
            Changed(StoreChange.Written, "/other.txt", 4),
        ];
        CollectionAssert.AreEqual(expected, events.Events.ToArray());
    }

    [TestMethod]
    public async Task WriteUploadAsync_OutOfSpaceRuleWithTimes1_FiresOnce()
    {
        var events = new RecordingSurlEventPublisher();
        var faults = new UnitTestPathPrefixFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "/uploads", Times = 1 });
        ContentStore store = EventStore(inMemory: true, events, faults);
        store.CreateDirectory(store.MapRequestPath("/uploads"));

        ContentUploadResult first = await UploadAsync(store, "/uploads/a.txt", appending: false);
        ContentUploadResult second = await UploadAsync(store, "/uploads/a.txt", appending: false);

        Assert.AreEqual(ContentUploadResult.TooLarge, first);
        Assert.AreEqual(ContentUploadResult.Written, second);
        Assert.AreEqual(1, events.Events.OfType<FaultAppliedEvent>().Count());
    }

    [TestMethod]
    public async Task CommitAsync_OutOfSpaceRuleForUploads_IsTooLargeDeletesTheUploadAndKeepsTheTarget()
    {
        var events = new RecordingSurlEventPublisher();
        var faults = new UnitTestPathPrefixFaultPlan(new FaultRule("f2", FaultKind.OutOfSpace) { Path = "/uploads" });
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var store = new ContentStore(MemoryRoot, fileSystem, UploadsOn, [], [], events, faults);
        store.CreateDirectory(store.MapRequestPath("/uploads"));

        ContentUploadResult result = await CommitSessionAsync(store, "/uploads/c.txt", "abcd");

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(Path.Join(MemoryRoot, "uploads")));
        Assert.AreEqual(new FaultAppliedEvent("f2", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "/uploads/c.txt"), events.Events[^1]);
    }

    [TestMethod]
    public async Task WriteUploadAsync_RuleOfAnotherKindAtTheStoreWrite_IsNotAppliedAndAsksWithTheCallersSchemeAndAccount()
    {
        var events = new RecordingSurlEventPublisher();
        var faults = new ScriptedFaultPlan(new FaultRule("f3", FaultKind.Delay));
        ContentStore store = EventStore(inMemory: true, events, faults);

        ContentUploadResult result = await store.WriteUploadAsync(
            store.MapRequestPathForCaller("/a.txt", ContentCaller.ForAccount("alice", "ftp")), Bytes("abc"), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual(new SurlEvent[] { Changed(StoreChange.Written, "/a.txt", 3) }, events.Events.ToArray());
        Assert.AreEqual(new FaultPoint(FaultStage.StoreWrite) { Path = "/a.txt", Scheme = "ftp", Account = "alice" }, faults.Points.Single());
    }

    private static ContentStore EventStore(bool inMemory, ISurlEventPublisher events, IFaultPlan? faults) =>
        inMemory
            ? new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), UploadsOn, [], [], events, faults)
            : new ContentStore(Root, new UnitTestInMemoryContentFileSystem().AddDirectory(Root), UploadsOn, [], [], events, faults);

    private static StoreChangedEvent Changed(StoreChange change, string name, long? byteCount) =>
        new(StoreKind.Content, change, name, byteCount, null);

    private static Task<ContentUploadResult> UploadAsync(ContentStore store, string requestPath, bool appending) =>
        appending
            ? store.AppendUploadAsync(store.MapRequestPath(requestPath), Bytes("data"), CancellationToken.None)
            : store.WriteUploadAsync(store.MapRequestPath(requestPath), Bytes("data"), CancellationToken.None);

    private static async Task<ContentUploadResult> CommitSessionAsync(ContentStore store, string requestPath, string text)
    {
        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath(requestPath), CreateOrTruncate, CancellationToken.None);
        await using ContentUploadSession session = outcome.Session!;
        await session.WriteAtAsync(0, Encoding.UTF8.GetBytes(text), CancellationToken.None);
        return await session.CommitAsync(CancellationToken.None);
    }
}
