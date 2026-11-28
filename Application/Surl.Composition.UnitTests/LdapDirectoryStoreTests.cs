using System.Text;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;

namespace Surl.Composition;

/// <summary>
/// The LDAP directory under whole-instance seed, reset, snapshot and restore (ADR-0158 decisions 3 to 6;
/// BL-1163), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class LdapDirectoryStoreTests
{
    private const string ListenUrl = "ldap://127.0.0.1:0";
    private const string SeedLdif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";

    // A base search of dc=example,dc=com, filter (objectClass=*), then an unbind, with no bind first.
    private static readonly byte[] SearchThenUnbind = Convert.FromHexString(
        "30840000003E020102638400000035041164633D6578616D706C652C64633D636F6D0A01000A0100020100"
        + "020100010100870B6F626A656374436C617373308400000000"
        + "30840000000502010342" + "00");

    private static readonly byte[] ExampleDn = Encoding.ASCII.GetBytes("dc=example,dc=com");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_SeedEntriesAsync_IsFoundByAnLdapSearchOfTheSameInstance()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        var before = await SearchAsync(running, factory);

        await running.Stores.SeedEntriesAsync(SeedLdif, TestContext.CancellationToken);
        var after = await SearchAsync(running, factory);

        Assert.IsFalse(Contains(before, ExampleDn));
        Assert.IsTrue(Contains(after, ExampleDn));
    }

    [TestMethod]
    public async Task Stores_ResetAsync_EmptiesTheDirectoryAndRestoreBringsTheSeededEntryBack()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        await running.Stores.SeedEntriesAsync(SeedLdif, TestContext.CancellationToken);
        var snapshot = await running.Stores.TakeSnapshotAsync(TestContext.CancellationToken);

        await running.Stores.ResetAsync(TestContext.CancellationToken);
        var afterReset = await SearchAsync(running, factory);
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        var afterRestore = await SearchAsync(running, factory);

        Assert.IsFalse(Contains(afterReset, ExampleDn));
        Assert.IsTrue(Contains(afterRestore, ExampleDn));
        CollectionAssert.AreEqual(
            new[] { StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts, StoreKind.Directory },
            running.Stores.RegisteredStoreOrder.ToArray());
    }

    [TestMethod]
    public async Task Stores_ResetAsync_KeepsAnEntrySeededToBeKeptAndDropsOneSeededWithoutTheFlag()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await running.Stores.SeedEntriesAsync(SeedLdif, keepOnReset: true, TestContext.CancellationToken);
        await running.Stores.SeedEntriesAsync(
            "dn: ou=dropped,dc=example,dc=com\nobjectClass: organizationalUnit\nou: dropped\n", TestContext.CancellationToken);

        await running.Stores.ResetAsync(StoreKind.Directory, TestContext.CancellationToken);

        var ldif = running.Stores.ReadDirectoryLdif().Value!;
        StringAssert.Contains(ldif, "dn: dc=example,dc=com\n");
        Assert.IsFalse(ldif.Contains("ou=dropped", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Stores_SeedResetAndRestore_PublishTheirChangesOnTheInstancesStream()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        await running.Stores.SeedEntriesAsync(SeedLdif, TestContext.CancellationToken);
        var snapshot = await running.Stores.TakeSnapshotAsync(StoreKind.Directory, TestContext.CancellationToken);
        await running.Stores.ResetAsync(StoreKind.Directory, TestContext.CancellationToken);
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<StoreChange> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            changes.Add(((StoreChangedEvent)surlEvent!).Change);
        }

        CollectionAssert.AreEqual(new[] { StoreChange.Seeded, StoreChange.Reset, StoreChange.Restored }, changes);
    }

    [TestMethod]
    public async Task Stores_SeedEntriesAsync_RefusesBadLdifAndACancelledToken()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => running.Stores.SeedEntriesAsync("dn: dc=x\nchangetype: delete\n", TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => running.Stores.SeedEntriesAsync(SeedLdif, cancelled.Token));
    }

    [TestMethod]
    public async Task SeedEntriesAsync_AnInstanceWithoutADirectory_IsRefused()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        var stores = new ComposedStores(
            running.Stores.Content, running.Stores.MetadataIndex, running.Stores.Mail, running.Stores.Bus, running.Stores.Accounts);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => stores.SeedEntriesAsync(SeedLdif, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task For_ADirectoryLoadedFromAFile_ResetsToTheFilesEntriesAndRestoresASnapshot()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var folder = Path.Join(InMemoryContentFileSystem.RootPath, ".surl", "ldap");
        fileSystem.CreateDirectory(folder);
        await using (var stream = fileSystem.CreateFileForAsyncWrite(Path.Join(folder, LdapDirectoryFile.FileName)))
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(SeedLdif), TestContext.CancellationToken);
        }

        var policy = SurlServerComposition.ComposePolicyWithoutAccounts(TimeProvider.System);
        var (server, failure) = await SurlServerComposition.LoadLdapServerAsync(
            new LdapDirectoryFile(fileSystem, folder), policy, false, TimeProvider.System, TestContext.CancellationToken);
        Assert.IsNull(failure);
        var registration = LdapDirectoryStoreRegistration.For(server!);
        server!.SeedDirectoryEntries("dn: ou=extra,dc=example,dc=com\nobjectClass: organizationalUnit\nou: extra\n");
        var seededBytes = (await registration.TakeSnapshotAsync(long.MaxValue, TestContext.CancellationToken)).ByteCount;

        await registration.ResetAsync(TestContext.CancellationToken);
        var resetBytes = (await registration.TakeSnapshotAsync(long.MaxValue, TestContext.CancellationToken)).ByteCount;

        Assert.IsTrue(seededBytes > resetBytes);
        Assert.IsTrue(resetBytes > 0);
    }

    private static bool Contains(byte[] written, byte[] bytes) => written.AsSpan().IndexOf(bytes) >= 0;

    private async Task<(ComposedServing Serving, FakeListenerFactory Factory)> ComposeAsync()
    {
        var factory = new FakeListenerFactory();
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", ListenUrl]).CommandLine!,
            new StartTestSeams(factory).Seams,
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return (start.Serving!, factory);
    }

    // One connection's answer to a search of the example entry; the serving loop runs for just that exchange.
    private async Task<byte[]> SearchAsync(ComposedServing serving, FakeListenerFactory factory)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        var connection = new FakeConnection(SearchThenUnbind);
        await factory.ConnectionsFor(ListenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
        return connection.WrittenBytes;
    }
}
