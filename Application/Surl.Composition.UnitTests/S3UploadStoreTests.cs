using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.S3;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The S3 server's open multipart uploads under whole-instance reset, snapshot and restore
/// (ADR-0167 decisions 2, 3 and 5; BL-1229), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class S3UploadStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_ResetAndRestore_PublishOneUploadsChangeEachNamedS3AndSnapshotHoldsNothing()
    {
        await using var serving = await ComposeAsync("--allow-anonymous", "--s3-plaintext", "s3://127.0.0.1:0");
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
            new[] { (StoreKind.Uploads, StoreChange.Reset, "s3"), (StoreKind.Uploads, StoreChange.Restored, "s3") },
            changes.Select(change => (change.Store, change.Change, change.Name)).ToArray());
    }

    [TestMethod]
    public async Task Stores_WithAnS3Server_RegistersUploadsAfterTheDirectory()
    {
        await using var serving = await ComposeAsync("--allow-anonymous", "--s3-plaintext", "s3://127.0.0.1:0");

        Assert.AreEqual(StoreKind.Uploads, serving.Stores.RegisteredStoreOrder[^1]);
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutAnS3ListenUrl_RegistersNoUploadsStore()
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

        Assert.IsNull(S3UploadStoreRegistration.For([], content, new SurlEventStream(TimeProvider.System)));
    }

    [TestMethod]
    public async Task For_ADiskBackedContentStore_RefusesResetAndSnapshot()
    {
        var directory = Path.Join(Path.GetTempPath(), "surl-s3-uploads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var content = SurlServerComposition.ComposeContentStore(new SurlCommandLine { DataDirectory = directory }, TimeProvider.System);
            var services = SurlServerComposition.ComposeUnservedProtocolServices(content, TimeProvider.System);
            var server = new S3ProtocolServer(content, new ObjectMetadataIndex(new InMemoryContentFileSystem(TimeProvider.System), InMemoryContentFileSystem.RootPath, false), services.AuthenticationPolicy, TimeProvider.System);
            var registration = S3UploadStoreRegistration.For([server], content, new SurlEventStream(TimeProvider.System))!;

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
        var start = await SurlServerComposition.ComposeAsync(CommandLineParser.Parse(args, ["s3"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return start.Serving!;
    }
}
