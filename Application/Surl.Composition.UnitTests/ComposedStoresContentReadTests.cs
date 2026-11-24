using System.Text;
using Surl.Content;
using Surl.Core;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A test reads files and folder listings back through <see cref="ComposedStores"/> without a client
/// (ADR-0164, BL-1197).
/// </summary>
[TestClass]
public sealed class ComposedStoresContentReadTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadFileAsync_ASeededFile_GivesExactBytesSizeAndContentType_AndListFolderListsIt()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        await SeedAsync(stores, "/reports/a.txt", "hello");
        await SeedAsync(stores, "/reports/b.bin", "x");
        _ = stores.SeedFolder("/reports/sub");

        var file = (await stores.ReadFileAsync("/reports/a.txt", TestContext.CancellationToken)).Value!;
        var listing = stores.ListFolder("/reports", null, 100, TestContext.CancellationToken).Value!;

        Assert.AreEqual("hello", Encoding.ASCII.GetString(file.Bytes.Span));
        Assert.AreEqual(5, file.ByteCount);
        Assert.AreEqual("text/plain", file.ContentType.Split(';')[0]);
        CollectionAssert.AreEqual(new[] { "a.txt", "b.bin", "sub" }, listing.Entries.Select(entry => entry.Name).ToArray());
        Assert.AreEqual(5, listing.Entries[0].ByteCount);
        Assert.IsFalse(listing.Entries[0].IsFolder);
        Assert.IsTrue(listing.Entries[2].IsFolder);
        Assert.IsFalse(listing.HasMore);
    }

    [TestMethod]
    public async Task ListFolder_ALimitAndACursor_PageInOrdinalOrder()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        await SeedAsync(stores, "/d/a", "1");
        await SeedAsync(stores, "/d/b", "2");

        var first = stores.ListFolder("/d", null, 1, TestContext.CancellationToken).Value!;
        var second = stores.ListFolder("/d", "a", 1, TestContext.CancellationToken).Value!;

        Assert.AreEqual("a", first.Entries.Single().Name);
        Assert.IsTrue(first.HasMore);
        Assert.AreEqual("b", second.Entries.Single().Name);
        Assert.IsFalse(second.HasMore);
    }

    [TestMethod]
    public async Task Reads_MissingHiddenAndBadPaths_AreNotFoundOrRefused()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        await SeedAsync(stores, "/f.txt", "x");

        Assert.AreEqual(ComposedReadOutcome.NotFound, (await stores.ReadFileAsync("/nope.txt", TestContext.CancellationToken)).Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, (await stores.ReadFileAsync("/.surl/x", TestContext.CancellationToken)).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, (await stores.ReadFileAsync("/a/../f.txt", TestContext.CancellationToken)).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, (await stores.ReadFileAsync("//f.txt", TestContext.CancellationToken)).Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListFolder("/nope", null, 10, TestContext.CancellationToken).Outcome);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListFolder("/f.txt", null, 10, TestContext.CancellationToken).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ListFolder("/a/../b", null, 10, TestContext.CancellationToken).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ListFolder("/", null, 0, TestContext.CancellationToken).Outcome);
        Assert.AreEqual(ComposedReadOutcome.Refused, stores.ListFolder("/", null, 1001, TestContext.CancellationToken).Outcome);
        Assert.IsFalse(stores.ListFolder("/", null, 10, TestContext.CancellationToken).Value!.Entries.Any(entry => entry.Name == ".surl"));
    }

    [TestMethod]
    public async Task ReadFileAsync_AFileOverTheBound_IsRefusedNotTruncated()
    {
        await using var serving = await ComposeAsync("--max-filesize", "20000000");
        var stores = serving.Stores;
        await using var big = new MemoryStream(new byte[ComposedStores.MaxReadBytes + 1]);
        Assert.AreEqual(ContentUploadResult.Written, await stores.SeedFileAsync("/big.bin", big, TestContext.CancellationToken));

        var result = await stores.ReadFileAsync("/big.bin", TestContext.CancellationToken);

        Assert.AreEqual(ComposedReadOutcome.TooLarge, result.Outcome);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public async Task Reads_PublishNoEvent()
    {
        await using var serving = await ComposeAsync();
        var stores = serving.Stores;
        await SeedAsync(stores, "/f.txt", "x");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        _ = await stores.ReadFileAsync("/f.txt", TestContext.CancellationToken);
        _ = stores.ListFolder("/", null, 10, TestContext.CancellationToken);

        Assert.IsFalse(subscription.TryRead(out _));
    }

    [TestMethod]
    public async Task Reads_ANullPath_Throws()
    {
        await using var serving = await ComposeAsync();
        await Assert.ThrowsAsync<ArgumentNullException>(() => serving.Stores.ReadFileAsync(null!, TestContext.CancellationToken));
        Assert.ThrowsExactly<ArgumentNullException>(() => serving.Stores.ListFolder(null!, null, 10, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadFileAsyncAndListFolder_AShareClosedByItsGrants_AreReadAsTheTestsOwnView()
    {
        await using var serving = await ComposeAsync("--user", "alice:pw", "--share", "team=team", "--share-access", "team=alice:rw");
        var stores = serving.Stores;
        await SeedAsync(stores, "/team/a.txt", "inside");

        var file = await stores.ReadFileAsync("/team/a.txt", TestContext.CancellationToken);
        var folder = stores.ListFolder("/team", null, 100, TestContext.CancellationToken);
        var root = stores.ListFolder("/", null, 100, TestContext.CancellationToken);

        Assert.AreEqual("inside", Encoding.ASCII.GetString(file.Value!.Bytes.Span));
        CollectionAssert.AreEqual(new[] { "a.txt" }, folder.Value!.Entries.Select(entry => entry.Name).ToArray());
        Assert.IsTrue(root.Value!.Entries.Any(entry => entry.Name == "team" && entry.IsFolder));
    }

    private async Task SeedAsync(ComposedStores stores, string path, string text)
    {
        await using var contents = new MemoryStream(Encoding.ASCII.GetBytes(text));
        Assert.AreEqual(ContentUploadResult.Written, await stores.SeedFileAsync(path, contents, TestContext.CancellationToken));
    }

    private static InMemoryContentFileSystem TeamShareFiles()
    {
        var files = new InMemoryContentFileSystem(TimeProvider.System);
        files.CreateDirectory(Path.GetFullPath("team"));
        return files;
    }

    private async Task<ComposedServing> ComposeAsync(params string[] options)
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "--allow-anonymous", "http://127.0.0.1:0/"]),
            new StartTestSeams().Seams with { ContentWithoutDataDirectory = new InMemoryContentFileSystem(TimeProvider.System), CanOpenShareDirectory = _ => true, ShareFileSystem = TeamShareFiles() },
            TestContext.CancellationToken);
        return start.Serving!;
    }
}
