using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.GoogleCloudStorage;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The Google Cloud Storage server's open uploads under whole-instance reset, snapshot and restore
/// (ADR-0167 decisions 2, 3 and 5; BL-1231), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class GcsStagedUploadStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ResetAndRestore_PublishOneUploadsChangeEachNamedGcsAndSnapshotHoldsNothing()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(CommandLineParser.Parse(["--allow-anonymous", "--gs-plaintext", "gs://127.0.0.1:0"], ["gs"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var serving = start.Serving!;
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        var snapshot = await serving.Stores.TakeSnapshotAsync(StoreKind.Uploads, TestContext.CancellationToken);
        await serving.Stores.ResetAsync(StoreKind.Uploads, TestContext.CancellationToken);
        await serving.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<(StoreChange, string?)> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            var change = (StoreChangedEvent)surlEvent!;
            if (change.Name == "gcs")
            {
                changes.Add((change.Change, change.Name));
            }
        }

        Assert.AreEqual(0, snapshot.ByteCount);
        CollectionAssert.AreEqual(new[] { (StoreChange.Reset, (string?)"gcs"), (StoreChange.Restored, "gcs") }, changes);
    }

    [TestMethod]
    public void For_NoServers_IsNull()
    {
        var content = SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System);

        Assert.IsNull(GcsStagedUploadStoreRegistration.For([], content, new SurlEventStream(TimeProvider.System)));
    }

    [TestMethod]
    public async Task For_ADiskBackedContentStore_RefusesResetAndSnapshot()
    {
        var directory = Path.Join(Path.GetTempPath(), "surl-gcs-uploads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var content = SurlServerComposition.ComposeContentStore(new SurlCommandLine { DataDirectory = directory }, TimeProvider.System);
            var services = SurlServerComposition.ComposeUnservedProtocolServices(content, TimeProvider.System);
            var server = new GoogleCloudStorageProtocolServer(content, new ObjectMetadataIndex(new InMemoryContentFileSystem(TimeProvider.System), InMemoryContentFileSystem.RootPath, false), services.AuthenticationPolicy, TimeProvider.System);
            var registration = GcsStagedUploadStoreRegistration.For([server], content, new SurlEventStream(TimeProvider.System))!;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.ResetAsync(TestContext.CancellationToken));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.TakeSnapshotAsync(long.MaxValue, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
