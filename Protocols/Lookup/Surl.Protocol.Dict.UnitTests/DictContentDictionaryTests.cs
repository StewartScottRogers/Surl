using Surl.Content;

namespace Surl.Protocol.Dict;

[TestClass]
public sealed class DictContentDictionaryTests
{
    private static readonly string Root = Path.Join(Path.GetTempPath(), "surl-dict-dictionary-tests");
    private static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void FindDefinition_ListedHeadwordWhoseRequestPathTheContentStoreDoesNotMap_ReturnsNull()
    {
        var fileSystem = new UnitTestFileMovedOutsideRootOnceListedContentFileSystem(
            new UnitTestInMemoryContentFileSystem()
                .AddDirectory(Root)
                .AddFile(Path.Join(Root, "hello"), "A greeting.\n"u8.ToArray(), FileTime),
            Path.Join(Root, "hello"),
            Path.Join(Path.GetTempPath(), "surl-dict-elsewhere", "hello"));
        var dictionary = new DictContentDictionary(new ContentStore(Root, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot));

        Assert.IsNull(dictionary.FindDefinition("hello", CancellationToken.None));
    }

    [TestMethod]
    public void FindDefinition_ListedMappedHeadwordWhoseFileIsGoneBeforeItsStatusIsRead_ReturnsNull()
    {
        var fileSystem = new UnitTestFileGoneOnceListedContentFileSystem(
            new UnitTestInMemoryContentFileSystem()
                .AddDirectory(Root)
                .AddFile(Path.Join(Root, "hello"), "A greeting.\n"u8.ToArray(), FileTime),
            Path.Join(Root, "hello"));
        var dictionary = new DictContentDictionary(new ContentStore(Root, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot));

        Assert.IsNull(dictionary.FindDefinition("hello", CancellationToken.None));
    }
}
