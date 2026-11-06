namespace Surl.Conformance;

/// <summary>
/// Reads the imported data beside the test assembly, as <see cref="FileSystemUpstreamTestCaseFileReaderTests"/>
/// does, so the default run covers the reader on every platform without writing a file.
/// </summary>
[TestClass]
public sealed class FileSystemUpstreamTestCaseRunFilesTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases");

    [TestMethod]
    public void ReadAllBytesOrNull_ExistingFile_IsItsBytes()
    {
        var path = Path.Combine(Root, "COPYING");

        var bytes = new FileSystemUpstreamTestCaseRunFiles().ReadAllBytesOrNull(path);

        CollectionAssert.AreEqual(File.ReadAllBytes(path), bytes);
    }

    [TestMethod]
    public void ReadAllBytesOrNull_MissingFile_IsNull() =>
        Assert.IsNull(new FileSystemUpstreamTestCaseRunFiles().ReadAllBytesOrNull(Path.Combine(Root, "no-such-file")));

    [TestMethod]
    public void Exists_FileDirectoryAndMissingEntry_AreAsTheFileSystemHasThem()
    {
        var files = new FileSystemUpstreamTestCaseRunFiles();

        Assert.IsTrue(files.Exists(Path.Combine(Root, "COPYING")));
        Assert.IsTrue(files.Exists(Path.Combine(Root, "data")));
        Assert.IsFalse(files.Exists(Path.Combine(Root, "no-such-file")));
    }
}
