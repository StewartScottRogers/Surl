using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.OciRegistry;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The OCI registry's open upload sessions and tokens under whole-instance reset, snapshot and restore
/// (ADR-0167 decisions 2, 3 and 5; BL-1232), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class OciStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ResetAndRestore_PublishOneChangeEachNamedOciForUploadsAndSessionsAndSnapshotsHoldNothing()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(CommandLineParser.Parse(["--allow-anonymous", "--oci-plaintext", "oci://127.0.0.1:0"], ["oci"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var serving = start.Serving!;
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        var uploads = await serving.Stores.TakeSnapshotAsync(StoreKind.Uploads, TestContext.CancellationToken);
        var sessions = await serving.Stores.TakeSnapshotAsync(StoreKind.Sessions, TestContext.CancellationToken);
        await serving.Stores.ResetAsync(StoreKind.Uploads, TestContext.CancellationToken);
        await serving.Stores.ResetAsync(StoreKind.Sessions, TestContext.CancellationToken);
        await serving.Stores.RestoreAsync(uploads, TestContext.CancellationToken);
        await serving.Stores.RestoreAsync(sessions, TestContext.CancellationToken);

        List<(StoreKind, StoreChange, string?)> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            var change = (StoreChangedEvent)surlEvent!;
            if (change.Name == "oci")
            {
                changes.Add((change.Store, change.Change, change.Name));
            }
        }

        Assert.AreEqual(0, uploads.ByteCount);
        Assert.AreEqual(0, sessions.ByteCount);
        CollectionAssert.AreEqual(
            new[]
            {
                (StoreKind.Uploads, StoreChange.Reset, (string?)"oci"),
                (StoreKind.Sessions, StoreChange.Reset, "oci"),
                (StoreKind.Uploads, StoreChange.Restored, "oci"),
                (StoreKind.Sessions, StoreChange.Restored, "oci"),
            },
            changes);
    }

    [TestMethod]
    public void For_NoServers_IsNull()
    {
        var content = SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System);
        var events = new SurlEventStream(TimeProvider.System);

        Assert.IsNull(OciUploadStoreRegistration.For([], content, events));
        Assert.IsNull(OciTokenStoreRegistration.For([], events));
    }

    [TestMethod]
    public async Task For_ADiskBackedContentStore_RefusesResetAndSnapshotOfUploads()
    {
        var directory = Path.Join(Path.GetTempPath(), "surl-oci-uploads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var content = SurlServerComposition.ComposeContentStore(new SurlCommandLine { DataDirectory = directory }, TimeProvider.System);
            var services = SurlServerComposition.ComposeUnservedProtocolServices(content, TimeProvider.System);
            var server = new OciRegistryProtocolServer(content, new ObjectMetadataIndex(new InMemoryContentFileSystem(TimeProvider.System), InMemoryContentFileSystem.RootPath, false), services.AuthenticationPolicy, TimeProvider.System);
            var registration = OciUploadStoreRegistration.For([server], content, new SurlEventStream(TimeProvider.System))!;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.ResetAsync(TestContext.CancellationToken));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.TakeSnapshotAsync(long.MaxValue, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
