using System.Text;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.MailStore;
using Surl.MessageBus;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Each composed instance's stores, seeded, reset, snapshotted and restored through
/// <see cref="ComposedServing.Stores"/> (ADR-0124, decision 3; BL-805), over
/// <see cref="FakeListenerFactory"/> and <see cref="StartTestSeams"/>, with no network.
/// </summary>
[TestClass]
public sealed class ComposedStoresTests
{
    private const string SeededMessage = "Subject: seeded\r\n\r\nseeded through the stores\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ASeededMessageAndFile_AreFetchedOverImapAndReadOverHttp()
    {
        var httpConnection = new FakeConnection(Encoding.ASCII.GetBytes("GET /seeded.txt HTTP/1.1\r\nHost: x\r\nConnection: close\r\n\r\n"));
        var imapConnection = new FakeConnection(Encoding.ASCII.GetBytes("a1 SELECT INBOX\r\na2 FETCH 1 BODY[TEXT]\r\na3 LOGOUT\r\n"));
        await using var serving = await ComposeHttpAndImapAsync(httpConnection, imapConnection);

        var seededMail = await serving.Stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(SeededMessage), TestContext.CancellationToken);
        var seededFile = await SeedTextFileAsync(serving.Stores, "/seeded.txt", "served over http");
        await ServeUntilEndedAsync(serving, imapConnection);

        Assert.AreEqual(new MailSeedResult(MailRecipientLookup.Deliverable, MailStoreOutcome.Succeeded), seededMail);
        Assert.AreEqual(ContentUploadResult.Written, seededFile);
        StringAssert.Contains(Encoding.ASCII.GetString(imapConnection.WrittenBytes), "seeded through the stores\r\n");
        var httpWritten = Encoding.ASCII.GetString(httpConnection.WrittenBytes);
        StringAssert.StartsWith(httpWritten, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(httpWritten, "served over http");
    }

    [TestMethod]
    public async Task Stores_ResetAsync_EmptiesMailAndContentAndKeepsTheInboxAndTheHostsFiles()
    {
        var hostFiles = new InMemoryContentFileSystem(TimeProvider.System);
        hostFiles.CreateDirectory(Path.Join(InMemoryContentFileSystem.RootPath, "kept"));
        var imapConnection = new FakeConnection(Encoding.ASCII.GetBytes("a1 SELECT INBOX\r\na2 LOGOUT\r\n"));
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["imap"] = Task.FromResult(imapConnection) },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-anonymous", "imap://127.0.0.1:0/", "http://127.0.0.1:0/"),
            seams.Seams with { ContentWithoutDataDirectory = hostFiles },
            TestContext.CancellationToken);
        await using var serving = start.Serving!;
        await serving.Stores.SeedMailAsync("b@y", Encoding.ASCII.GetBytes(SeededMessage), TestContext.CancellationToken);
        await SeedTextFileAsync(serving.Stores, "/seeded.txt", "gone after the reset");

        await serving.Stores.ResetAsync(TestContext.CancellationToken);
        await ServeUntilEndedAsync(serving, imapConnection);

        var imapWritten = Encoding.ASCII.GetString(imapConnection.WrittenBytes);
        StringAssert.Contains(imapWritten, "* 0 EXISTS\r\n");
        StringAssert.Contains(imapWritten, "a1 OK");
        Assert.AreEqual(ContentEntryKind.None, hostFiles.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "seeded.txt")));
        Assert.AreEqual(ContentEntryKind.Directory, hostFiles.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "kept")));
    }

    [TestMethod]
    public async Task Stores_SnapshotChangeRestore_GivesBackEveryStoresState()
    {
        var files = new InMemoryContentFileSystem(TimeProvider.System);
        await using var serving = await ComposeStoresAsync(files, "-u", "alice:secret");
        var stores = serving.Stores;
        await stores.SeedMailAsync("alice@example.com", Encoding.ASCII.GetBytes(SeededMessage), TestContext.CancellationToken);
        await SeedTextFileAsync(stores, "/before.txt", "before");
        await stores.SeedPublishAsync(new BusMessage("t", "retained"u8.ToArray()) { Retain = true }, TestContext.CancellationToken);
        stores.AddAccount(new Account("bob", "builder"));

        var snapshot = await stores.TakeSnapshotAsync(TestContext.CancellationToken);
        await stores.ResetAsync(TestContext.CancellationToken);
        await SeedTextFileAsync(stores, "/after.txt", "after");
        stores.AddAccount(new Account("carol", "c"));
        await stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        CollectionAssert.AreEqual((StoreKind[])[.. ComposedStores.StoreOrder], snapshot.Stores.ToArray());
        Assert.AreEqual(1, stores.Mail.TakeSnapshot().MessageCount);
        Assert.AreEqual(ContentEntryKind.File, EntryKindOf(files, "before.txt"));
        Assert.AreEqual(ContentEntryKind.None, EntryKindOf(files, "after.txt"));
        Assert.AreEqual(1, RetainedMessageCount(stores.Bus));
        CollectionAssert.AreEqual(new[] { "alice", "bob" }, stores.Accounts.TakeSnapshot().UserNames.ToArray());
        Assert.AreEqual(snapshot.ByteCount, ("before".Length + stores.Mail.TakeSnapshot().ByteCount) + stores.Bus.TakeSnapshot().ByteCount);
    }

    [TestMethod]
    public async Task HasAccounts_AfterTheLastAccountIsRemoved_IsFalse()
    {
        await using var serving = await ComposeStoresAsync("-u", "alice:secret");
        var stores = serving.Stores;

        Assert.IsTrue(stores.HasAccounts);
        Assert.IsTrue(stores.RemoveAccount("alice"));

        Assert.IsFalse(stores.HasAccounts);
    }

    [TestMethod]
    public async Task Stores_EachOperation_PublishesItsChangesOnTheInstancesStream()
    {
        await using var serving = await ComposeStoresAsync("-u", "alice:secret");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());
        var stores = serving.Stores;

        await stores.SeedMailAsync("alice@example.com", Encoding.ASCII.GetBytes(SeededMessage), TestContext.CancellationToken);
        await SeedTextFileAsync(stores, "/seeded.txt", "seeded");
        Assert.AreEqual(ContentChangeResult.Done, await stores.DeleteFileAsync("/seeded.txt"));
        Assert.AreEqual(ContentChangeResult.Absent, await stores.DeleteFileAsync("/seeded.txt"));
        await SeedTextFileAsync(stores, "/seeded.txt", "seeded");
        Assert.AreEqual(ContentChangeResult.Done, stores.SeedFolder("/folder"));
        await stores.SeedPublishAsync(new BusMessage("t", "seeded"u8.ToArray()), TestContext.CancellationToken);
        stores.AddAccount(new Account("bob", "builder"));
        Assert.IsTrue(stores.RemoveAccount("bob"));
        var snapshot = await stores.TakeSnapshotAsync(TestContext.CancellationToken);
        await stores.ResetAsync(TestContext.CancellationToken);
        await stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        var changes = Drain(subscription).OfType<StoreChangedEvent>().ToList();

        CollectionAssert.AreEqual(
            new[] { StoreKind.Mail, StoreKind.Content, StoreKind.Content, StoreKind.Content, StoreKind.Bus },
            changes.Where(changed => changed.Change == StoreChange.Seeded).Select(changed => changed.Store).ToArray());
        Assert.IsTrue(changes.Any(changed => changed.Store == StoreKind.Accounts && changed.Change == StoreChange.AccountAdded && changed.Name == "bob"));
        Assert.IsTrue(changes.Any(changed => changed.Store == StoreKind.Accounts && changed.Change == StoreChange.AccountRemoved && changed.Name == "bob"));
        Assert.AreEqual(1, changes.Count(changed => changed.Store == StoreKind.Content && changed.Change == StoreChange.Deleted));
        CollectionAssert.AreEqual(
            (StoreKind[])[.. ComposedStores.StoreOrder],
            changes.Where(changed => changed.Change == StoreChange.Reset).Select(changed => changed.Store).ToArray());
        CollectionAssert.AreEqual(
            (StoreKind[])[.. ComposedStores.StoreOrder],
            changes.Where(changed => changed.Change == StoreChange.Restored).Select(changed => changed.Store).ToArray());
    }

    [TestMethod]
    public async Task Stores_OneStoreAtATime_ResetsSnapshotsAndRestoresThatStoreAlone()
    {
        var files = new InMemoryContentFileSystem(TimeProvider.System);
        await using var serving = await ComposeStoresAsync(files, "-u", "alice:secret");
        var stores = serving.Stores;
        await SeedTextFileAsync(stores, "/kept.txt", "kept");
        await stores.SeedMailAsync("alice@example.com", Encoding.ASCII.GetBytes(SeededMessage), TestContext.CancellationToken);

        var mailSnapshot = await stores.TakeSnapshotAsync(StoreKind.Mail, TestContext.CancellationToken);
        await stores.ResetAsync(StoreKind.Mail, TestContext.CancellationToken);
        var afterReset = stores.Mail.TakeSnapshot().MessageCount;
        await stores.RestoreAsync(mailSnapshot, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { StoreKind.Mail }, mailSnapshot.Stores.ToArray());
        Assert.AreEqual(0, afterReset);
        Assert.AreEqual(1, stores.Mail.TakeSnapshot().MessageCount);
        Assert.AreEqual(ContentEntryKind.File, EntryKindOf(files, "kept.txt"));
    }

    [TestMethod]
    public async Task Stores_WithDirectory_RefuseResetSnapshotAndRestoreOfTheDiskStoresButSeedThemAndControlAccounts()
    {
        var disk = new InMemoryContentFileSystem(TimeProvider.System);
        disk.CreateDirectory(Path.GetFullPath("served"));
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "-u", "alice:secret", "http://127.0.0.1:0/"),
            new StartTestSeams().Seams with { DataDirectoryFileSystem = disk },
            TestContext.CancellationToken);
        await using var serving = start.Serving!;
        var stores = serving.Stores;

        var wholeReset = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => stores.ResetAsync(TestContext.CancellationToken));
        var mailReset = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => stores.ResetAsync(StoreKind.Mail, TestContext.CancellationToken));
        var busReset = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => stores.ResetAsync(StoreKind.Bus, TestContext.CancellationToken));
        var wholeSnapshot = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => stores.TakeSnapshotAsync(TestContext.CancellationToken));
        var seeded = await SeedTextFileAsync(stores, "/seeded.txt", "ordinary writing");
        stores.AddAccount(new Account("bob", "builder"));
        var accountSnapshot = await stores.TakeSnapshotAsync(StoreKind.Accounts, TestContext.CancellationToken);
        await stores.ResetAsync(StoreKind.Accounts, TestContext.CancellationToken);
        var afterReset = stores.Accounts.TakeSnapshot().UserNames.ToArray();
        await stores.RestoreAsync(accountSnapshot, TestContext.CancellationToken);

        Assert.AreEqual(StoreKind.Content, wholeReset.Store);
        Assert.IsInstanceOfType<InvalidOperationException>(wholeReset.InnerException);
        StringAssert.StartsWith(wholeReset.Message, "Content: the store could not be reset. ");
        Assert.AreEqual(StoreKind.Mail, mailReset.Store);
        Assert.AreEqual(StoreKind.Bus, busReset.Store);
        Assert.AreEqual(StoreKind.Content, wholeSnapshot.Store);
        Assert.AreEqual(ContentUploadResult.Written, seeded);
        CollectionAssert.AreEqual(new[] { "alice" }, afterReset);
        CollectionAssert.AreEqual(new[] { "alice", "bob" }, stores.Accounts.TakeSnapshot().UserNames.ToArray());
    }

    [TestMethod]
    public async Task Stores_WithDirectory_ResettingTheDirectoryAndZonesLeavesTheLoadedContentsAndKeptSeedsAndTheFilesUnchanged()
    {
        const string Zone = "$TTL 3600\n@ IN SOA ns.loaded.test. host.loaded.test. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n";
        var disk = new InMemoryContentFileSystem(TimeProvider.System);
        var state = Path.Join(Path.GetFullPath("served"), ".surl");
        var ldifFile = await WriteStateFileAsync(
            disk, Path.Join(state, "ldap"), "directory.ldif", "dn: dc=loaded,dc=test\nobjectClass: domain\ndc: loaded\n");
        var zoneFile = await WriteStateFileAsync(disk, Path.Join(state, "dns"), "loaded.test.zone", Zone);
        var ldifBefore = await ReadAllBytesAsync(disk, ldifFile);
        var zoneBefore = await ReadAllBytesAsync(disk, zoneFile);
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", "--directory", "served", "ldap://127.0.0.1:0/", "dns://127.0.0.1:5300/"], ["dns"]).CommandLine!,
            new StartTestSeams().Seams with { DataDirectoryFileSystem = disk, AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var serving = start.Serving!;
        var stores = serving.Stores;
        await stores.SeedEntriesAsync(
            "dn: ou=kept,dc=loaded,dc=test\nobjectClass: organizationalUnit\nou: kept\n", keepOnReset: true, TestContext.CancellationToken);
        await stores.SeedEntriesAsync(
            "dn: ou=dropped,dc=loaded,dc=test\nobjectClass: organizationalUnit\nou: dropped\n", TestContext.CancellationToken);
        await stores.SeedZoneAsync(
            "kept.test", Zone.Replace("loaded.test", "kept.test", StringComparison.Ordinal), keepOnReset: true, TestContext.CancellationToken);
        await stores.SeedZoneAsync(
            "dropped.test", Zone.Replace("loaded.test", "dropped.test", StringComparison.Ordinal), TestContext.CancellationToken);

        await stores.ResetAsync(StoreKind.Directory, TestContext.CancellationToken);
        await stores.ResetAsync(StoreKind.Zones, TestContext.CancellationToken);

        var ldif = stores.ReadDirectoryLdif().Value!;
        StringAssert.Contains(ldif, "dn: dc=loaded,dc=test\n");
        StringAssert.Contains(ldif, "dn: ou=kept,dc=loaded,dc=test\n");
        Assert.IsFalse(ldif.Contains("ou=dropped", StringComparison.Ordinal));
        CollectionAssert.AreEqual(new[] { "loaded.test.", "kept.test." }, stores.ListZoneApexes().ToArray());
        CollectionAssert.AreEqual(ldifBefore, await ReadAllBytesAsync(disk, ldifFile));
        CollectionAssert.AreEqual(zoneBefore, await ReadAllBytesAsync(disk, zoneFile));
        CollectionAssert.AreEqual(new[] { "directory.ldif" }, disk.EnumerateDirectoryEntryNames(Path.Join(state, "ldap")).ToArray());
        CollectionAssert.AreEqual(new[] { "loaded.test.zone" }, disk.EnumerateDirectoryEntryNames(Path.Join(state, "dns")).ToArray());
    }

    [TestMethod]
    public async Task Stores_RestoreAsync_RefusesAnotherInstancesSnapshotAndNone()
    {
        await using var first = await ComposeStoresAsync();
        await using var second = await ComposeStoresAsync();
        var snapshot = await first.Stores.TakeSnapshotAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => second.Stores.RestoreAsync(snapshot, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => second.Stores.RestoreAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Stores_AStoreNoInstanceHolds_IsOutOfRange()
    {
        await using var serving = await ComposeStoresAsync();

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => serving.Stores.ResetAsync((StoreKind)99, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => serving.Stores.TakeSnapshotAsync((StoreKind)99, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Stores_ASnapshotPastItsBound_IsRefusedNamingTheStoreThatPassedIt()
    {
        await using var serving = await ComposeStoresAsync("-u", "alice:secret");
        var stores = serving.Stores;
        await SeedTextFileAsync(stores, "/ten.txt", "0123456789");
        await stores.SeedMailAsync("alice@example.com", Encoding.ASCII.GetBytes(SeededMessage), TestContext.CancellationToken);
        await stores.SeedPublishAsync(new BusMessage("t", "0123456789"u8.ToArray()) { Retain = true }, TestContext.CancellationToken);
        var mailBytes = stores.Mail.TakeSnapshot().ByteCount;

        var content = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => Bounded(stores, 9).TakeSnapshotAsync(TestContext.CancellationToken));
        var mail = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => Bounded(stores, 10 + mailBytes - 1).TakeSnapshotAsync(TestContext.CancellationToken));
        var bus = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => Bounded(stores, 10 + mailBytes).TakeSnapshotAsync(TestContext.CancellationToken));

        Assert.AreEqual(ComposedStores.DefaultMaxSnapshotBytes, stores.MaxSnapshotBytes);
        Assert.AreEqual(StoreKind.Content, content.Store);
        Assert.AreEqual("Content: The snapshot would hold 10 bytes, more than the 9 it may hold.", content.Message);
        Assert.AreEqual(StoreKind.Mail, mail.Store);
        Assert.AreEqual(StoreKind.Bus, bus.Store);
        Assert.IsInstanceOfType<InvalidOperationException>(bus.InnerException);
    }

    [TestMethod]
    public async Task Stores_SeedEnqueueAsync_PutsTheMessageThroughTheQueuePath()
    {
        await using var serving = await ComposeStoresAsync();

        var outcome = await serving.Stores.SeedEnqueueAsync("absent", new BusMessage("t", "x"u8.ToArray()), TestContext.CancellationToken);

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, outcome);
    }

    [TestMethod]
    public async Task Stores_ABusSeedThatCannotBeSaved_ThrowsTheSavesFailure()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "mqtt://127.0.0.1:0/"),
            new StartTestSeams().Seams with { DataDirectoryFileSystem = new InMemoryContentFileSystem(TimeProvider.System, maxTotalBytes: 1) },
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        await Assert.ThrowsAsync<IOException>(() => serving.Stores.SeedPublishAsync(
            new BusMessage("t", "more than one byte"u8.ToArray()) { Retain = true }, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Stores_AFifthRegisteredStore_IsResetSnapshottedAndRestoredAfterTheFour()
    {
        await using var serving = await ComposeStoresAsync();
        List<string> calls = [];
        var stores = WithFifthStore(serving.Stores, FifthStore(calls, byteCount: 7), ComposedStores.DefaultMaxSnapshotBytes);

        await stores.ResetAsync(TestContext.CancellationToken);
        var snapshot = await stores.TakeSnapshotAsync(TestContext.CancellationToken);
        await stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        var fifthAlone = await stores.TakeSnapshotAsync(FifthKind, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "reset", "snapshot", "restore", "snapshot" }, calls);
        CollectionAssert.AreEqual(ComposedStores.StoreOrder.Append(FifthKind).ToArray(), stores.RegisteredStoreOrder.ToArray());
        CollectionAssert.AreEqual(stores.RegisteredStoreOrder.ToArray(), snapshot.Stores.ToArray());
        Assert.AreEqual(7, snapshot.ByteCount);
        Assert.AreEqual(7, fifthAlone.ByteCount);
    }

    [TestMethod]
    public async Task Stores_AFifthStoreCountsTowardTheBoundAndItsRefusalNamesIt()
    {
        await using var serving = await ComposeStoresAsync();
        List<string> calls = [];
        var bounded = WithFifthStore(serving.Stores, FifthStore(calls, byteCount: 7), 6);
        var refusing = WithFifthStore(serving.Stores, FifthStore(calls, byteCount: 0, refuses: true), ComposedStores.DefaultMaxSnapshotBytes);

        var past = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => bounded.TakeSnapshotAsync(TestContext.CancellationToken));
        var reset = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => refusing.ResetAsync(TestContext.CancellationToken));
        var snapshot = await Assert.ThrowsExactlyAsync<ComposedStoreException>(() => refusing.TakeSnapshotAsync(TestContext.CancellationToken));

        Assert.AreEqual(FifthKind, past.Store);
        Assert.AreEqual(FifthKind, reset.Store);
        Assert.AreEqual(FifthKind, snapshot.Store);
    }

    private static readonly StoreKind FifthKind = (StoreKind)100;

    private static ComposedStoreRegistration FifthStore(List<string> calls, long byteCount, bool refuses = false) =>
        new(
            FifthKind,
            _ => refuses ? throw new InvalidOperationException("no") : Record(calls, "reset"),
            (_, _) =>
            {
                if (refuses)
                {
                    throw new InvalidOperationException("no");
                }

                calls.Add("snapshot");
                return Task.FromResult(new TakenSnapshot(FifthKind, byteCount, _ => Record(calls, "restore")));
            });

    private static Task Record(List<string> calls, string call)
    {
        calls.Add(call);
        return Task.CompletedTask;
    }

    private static ComposedStores WithFifthStore(ComposedStores stores, ComposedStoreRegistration fifth, long maxSnapshotBytes) =>
        new(stores.Content, stores.MetadataIndex, stores.Mail, stores.Bus, stores.Accounts, maxSnapshotBytes, [fifth]);

    private static ComposedStores Bounded(ComposedStores stores, long maxSnapshotBytes) =>
        new(stores.Content, stores.MetadataIndex, stores.Mail, stores.Bus, stores.Accounts, maxSnapshotBytes);

    private static ContentEntryKind EntryKindOf(InMemoryContentFileSystem files, string name) =>
        files.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, name));

    private static async Task<FakeConnection> HandOutAfterAsync(FakeConnection first, FakeConnection then)
    {
        await first.Disposed.Task;
        return then;
    }

    private static List<SurlEvent> Drain(SurlEventSubscription subscription)
    {
        List<SurlEvent> events = [];
        while (subscription.TryRead(out var surlEvent))
        {
            events.Add(surlEvent!);
        }

        return events;
    }

    // Serves until the last connection has ended, then stops.
    private async Task ServeUntilEndedAsync(ComposedServing serving, FakeConnection last)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await last.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        Assert.AreEqual(new SurlServingOutcome(SurlExitCode.Ok, null), await serve);
    }

    private static int RetainedMessageCount(MessageBus.MessageBus bus)
    {
        using var reader = bus.Connect();
        return reader.Subscribe("#", _ => { }, includeRetained: true).RetainedMessages.Count;
    }

    private async Task<string> WriteStateFileAsync(InMemoryContentFileSystem files, string folder, string name, string text)
    {
        files.CreateDirectory(folder);
        var path = Path.Join(folder, name);
        await using var stream = files.CreateFileForAsyncWrite(path);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text), TestContext.CancellationToken);
        return path;
    }

    private async Task<byte[]> ReadAllBytesAsync(InMemoryContentFileSystem files, string path)
    {
        await using var stream = files.OpenFileForAsyncRead(path);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.CancellationToken);
        return buffer.ToArray();
    }

    private async Task<ContentUploadResult> SeedTextFileAsync(ComposedStores stores, string requestPath, string text)
    {
        using var contents = new MemoryStream(Encoding.ASCII.GetBytes(text));
        return await stores.SeedFileAsync(requestPath, contents, TestContext.CancellationToken);
    }

    private Task<ComposedServing> ComposeStoresAsync(params string[] options) =>
        ComposeStoresAsync(new InMemoryContentFileSystem(TimeProvider.System), options);

    // A start serving http over files, the in-memory file system a host supplies.
    private async Task<ComposedServing> ComposeStoresAsync(InMemoryContentFileSystem files, params string[] options)
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "--allow-anonymous", "http://127.0.0.1:0/"]),
            new StartTestSeams().Seams with { ContentWithoutDataDirectory = files },
            TestContext.CancellationToken);
        return start.Serving!;
    }

    private async Task<ComposedServing> ComposeHttpAndImapAsync(FakeConnection httpConnection, FakeConnection imapConnection)
    {
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>>
            {
                ["http"] = Task.FromResult(httpConnection),
                ["imap"] = HandOutAfterAsync(httpConnection, imapConnection),
            },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-anonymous", "http://127.0.0.1:0/", "imap://127.0.0.1:0/"), seams.Seams, TestContext.CancellationToken);
        return start.Serving!;
    }
}
