using Surl.Cli;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    private static readonly string IndexPathUnderServed =
        Path.Join(Path.GetFullPath("served"), ".surl", ObjectMetadataIndex.ObjectsFolderName, ObjectMetadataIndex.IndexFileName);

    [TestMethod]
    public void ComposeObjectMetadataIndex_WithADataDirectory_PersistsInItsSurlFolder()
    {
        var index = SurlServerComposition.ComposeObjectMetadataIndex(
            new SurlCommandLine { DataDirectory = "served" }, new InMemoryContentFileSystem(TimeProvider.System));

        Assert.AreEqual(IndexPathUnderServed, index.IndexPath);
    }

    [TestMethod]
    public void ComposeObjectMetadataIndex_WithoutADataDirectory_IsHeldInMemoryOnly()
    {
        var index = SurlServerComposition.ComposeObjectMetadataIndex(new SurlCommandLine(), new InMemoryContentFileSystem(TimeProvider.System));

        Assert.IsNull(index.IndexPath);
    }

    [TestMethod]
    public void ComposeObjectMetadataIndex_NullCommandLine_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => SurlServerComposition.ComposeObjectMetadataIndex(null!, new InMemoryContentFileSystem(TimeProvider.System)));
    }

    [TestMethod]
    public async Task LoadObjectMetadataIndexAsync_NoIndexFileYet_IsTheIndex()
    {
        var index = SurlServerComposition.ComposeObjectMetadataIndex(
            new SurlCommandLine { DataDirectory = "served" }, new InMemoryContentFileSystem(TimeProvider.System));

        var (loaded, failureMessage) = await SurlServerComposition.LoadObjectMetadataIndexAsync(index, TestContext.CancellationToken);

        Assert.AreSame(index, loaded);
        Assert.IsNull(failureMessage);
    }

    [TestMethod]
    public async Task LoadObjectMetadataIndexAsync_MalformedIndexFile_GivesCouldNotReadWithTheFile()
    {
        var fileSystem = FileSystemWithMalformedIndex();
        var index = SurlServerComposition.ComposeObjectMetadataIndex(new SurlCommandLine { DataDirectory = "served" }, fileSystem);

        var (loaded, failureMessage) = await SurlServerComposition.LoadObjectMetadataIndexAsync(index, TestContext.CancellationToken);

        Assert.IsNull(loaded);
        Assert.AreEqual($"(37) Could not read {IndexPathUnderServed}: {ObjectMetadataIndex.MalformedIndexReason}", failureMessage);
    }

    [TestMethod]
    public async Task LoadObjectMetadataIndexAsync_NullIndex_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => SurlServerComposition.LoadObjectMetadataIndexAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ComposeAsync_ADataDirectoryWithAMalformedObjectIndex_FailsWithCouldNotReadFile()
    {
        var seams = new StartTestSeams().Seams with { DataDirectoryFileSystem = FileSystemWithMalformedIndex() };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "http://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"(37) Could not read {IndexPathUnderServed}: {ObjectMetadataIndex.MalformedIndexReason}" },
            start.Failure.Lines.ToArray());
    }

    private static InMemoryContentFileSystem FileSystemWithMalformedIndex()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(Path.GetDirectoryName(IndexPathUnderServed)!);
        using (var file = fileSystem.CreateFileForAsyncWrite(IndexPathUnderServed))
        {
            file.Write("not an index\n"u8);
        }

        return fileSystem;
    }
}
