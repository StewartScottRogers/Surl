using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.AzureStorage;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The Azure Storage server's staged blocks under whole-instance reset, snapshot and restore
/// (ADR-0167 decisions 2, 3 and 5; BL-1230), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class AzureStagedUploadStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ResetAndRestore_PublishOneUploadsChangeEachNamedAzureAndSnapshotHoldsNothing()
    {
        await using var serving = await ComposeAsync("--allow-anonymous", "--az-plaintext", "az://127.0.0.1:0");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        var snapshot = await serving.Stores.TakeSnapshotAsync(StoreKind.Uploads, TestContext.CancellationToken);
        await serving.Stores.ResetAsync(StoreKind.Uploads, TestContext.CancellationToken);
        await serving.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<StoreChangedEvent> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            changes.Add((StoreChangedEvent)surlEvent!);
        }

        Assert.AreEqual(0, snapshot.ByteCount);
        CollectionAssert.AreEqual(
            new[] { (StoreKind.Uploads, StoreChange.Reset, "s3"), (StoreKind.Uploads, StoreChange.Reset, "azure"), (StoreKind.Uploads, StoreChange.Reset, "gcs"), (StoreKind.Uploads, StoreChange.Reset, "oci"), (StoreKind.Uploads, StoreChange.Restored, "s3"), (StoreKind.Uploads, StoreChange.Restored, "azure"), (StoreKind.Uploads, StoreChange.Restored, "gcs"), (StoreKind.Uploads, StoreChange.Restored, "oci") },
            changes.Select(change => (change.Store, change.Change, change.Name)).ToArray());
    }

    [TestMethod]
    public async Task Stores_WithAnS3AndAnAzureServer_RegistersOneUploadsStoreThatControlsBoth()
    {
        await using var serving = await ComposeAsync("--allow-anonymous", "--s3-plaintext", "--az-plaintext", "s3://127.0.0.1:0", "az://127.0.0.1:0");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        var snapshot = await serving.Stores.TakeSnapshotAsync(StoreKind.Uploads, TestContext.CancellationToken);
        await serving.Stores.ResetAsync(StoreKind.Uploads, TestContext.CancellationToken);
        await serving.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<string?> names = [];
        while (subscription.TryRead(out var surlEvent))
        {
            names.Add(((StoreChangedEvent)surlEvent!).Name);
        }

        Assert.AreEqual(1, serving.Stores.RegisteredStoreOrder.Count(store => store == StoreKind.Uploads));
        Assert.AreEqual(0, snapshot.ByteCount);
        CollectionAssert.AreEqual(new[] { "s3", "azure", "gcs", "oci", "s3", "azure", "gcs", "oci" }, names);
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutAnAzureListenUrl_RegistersNoUploadsStore()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-anonymous", "http://127.0.0.1:0/"), new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.DoesNotContain(StoreKind.Uploads, serving.Stores.RegisteredStoreOrder);
    }

    [TestMethod]
    public void For_NoServers_IsNull()
    {
        var content = SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System);

        Assert.IsNull(AzureStagedUploadStoreRegistration.For([], content, new SurlEventStream(TimeProvider.System)));
    }

    [TestMethod]
    public void Combine_NoRegistrations_IsNull()
    {
        Assert.IsNull(SameKindStoreRegistrations.Combine(StoreKind.Uploads, null, null));
    }

    [TestMethod]
    public void Combine_OneRegistration_IsThatRegistration()
    {
        var only = new ComposedStoreRegistration(StoreKind.Uploads, _ => Task.CompletedTask, (_, _) => Task.FromResult(new TakenSnapshot(StoreKind.Uploads, 0, _ => Task.CompletedTask)));

        Assert.AreSame(only, SameKindStoreRegistrations.Combine(StoreKind.Uploads, null, only));
    }

    [TestMethod]
    public async Task For_ADiskBackedContentStore_RefusesResetAndSnapshot()
    {
        var directory = Path.Join(Path.GetTempPath(), "surl-azure-uploads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var content = SurlServerComposition.ComposeContentStore(new SurlCommandLine { DataDirectory = directory }, TimeProvider.System);
            var services = SurlServerComposition.ComposeUnservedProtocolServices(content, TimeProvider.System);
            var server = new AzureStorageProtocolServer(content, new ObjectMetadataIndex(new InMemoryContentFileSystem(TimeProvider.System), InMemoryContentFileSystem.RootPath, false), services.AuthenticationPolicy, TimeProvider.System);
            var registration = AzureStagedUploadStoreRegistration.For([server], content, new SurlEventStream(TimeProvider.System))!;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.ResetAsync(TestContext.CancellationToken));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => registration.TakeSnapshotAsync(long.MaxValue, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private async Task<ComposedServing> ComposeAsync(params string[] args)
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(CommandLineParser.Parse(args, ["s3", "az"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return start.Serving!;
    }
}
