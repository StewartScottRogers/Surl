using Surl.Content;

namespace Surl.Api;

[TestClass]
public sealed class InMemoryFilesTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("")]
    [DataRow("/a.txt")]
    [DataRow("a//b.txt")]
    [DataRow("a/")]
    [DataRow("../a.txt")]
    [DataRow("a/./b.txt")]
    [DataRow(@"a\b.txt")]
    [DataRow("C:/a.txt")]
    public void ThrowIfMisused_APathThatIsNotPlainAndRelative_ThrowsArgument(string path)
    {
        var files = new Dictionary<string, byte[]> { [path] = [1] };

        Assert.ThrowsExactly<ArgumentException>(() => InMemoryFiles.ThrowIfMisused(files));
    }

    [TestMethod]
    public void ThrowIfMisused_NullBytes_ThrowsArgument()
    {
        var files = new Dictionary<string, byte[]> { ["a.txt"] = null! };

        Assert.ThrowsExactly<ArgumentException>(() => InMemoryFiles.ThrowIfMisused(files));
    }

    [TestMethod]
    public void ThrowIfMisused_AFileWithAFileInsideIt_ThrowsArgument()
    {
        var files = new Dictionary<string, byte[]> { ["docs"] = [1], ["docs/a.txt"] = [2] };

        Assert.ThrowsExactly<ArgumentException>(() => InMemoryFiles.ThrowIfMisused(files));
    }

    [TestMethod]
    public void ThrowIfMisused_SiblingsAndNestedFiles_AreAccepted()
    {
        var files = new Dictionary<string, byte[]> { ["docs.txt"] = [1], ["docs/a.txt"] = [2], ["docs/b/c.txt"] = [] };

        InMemoryFiles.ThrowIfMisused(files);

        Assert.HasCount(3, files);
    }

    [TestMethod]
    public async Task ComposeAsync_NestedFiles_WritesEachUnderTheRootWithItsBytes()
    {
        var files = new Dictionary<string, byte[]> { ["readme.txt"] = "hi"u8.ToArray(), ["docs/b/c.txt"] = "deep"u8.ToArray() };

        var fileSystem = await InMemoryFiles.ComposeAsync(files, TimeProvider.System, TestContext.CancellationToken);

        Assert.AreEqual("hi", await ReadAsync(fileSystem, Path.Combine(InMemoryContentFileSystem.RootPath, "readme.txt")));
        Assert.AreEqual("deep", await ReadAsync(fileSystem, Path.Combine(InMemoryContentFileSystem.RootPath, "docs", "b", "c.txt")));
    }

    private async Task<string> ReadAsync(InMemoryContentFileSystem fileSystem, string path)
    {
        await using var stream = fileSystem.OpenFileForAsyncRead(path);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(TestContext.CancellationToken);
    }
}
