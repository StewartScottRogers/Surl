using Surl.Content;
using Surl.HttpServing;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// <see cref="HttpByteRangesBody"/> builds the <c>multipart/byteranges</c> body, and
/// <see cref="HttpAnswerBody.ContentFileParts"/> copies it (RFC 9110, section 14.6; ADR-0133,
/// Amendment 1).
/// </summary>
[TestClass]
public sealed class HttpByteRangesBodyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Of_TwoRanges_CopiesTheRecordedMultipartBody()
    {
        var (store, mapping) = StoreAndMapping(StandardFileSystem());
        var ranges = new[] { ContentByteRange.Select(17, 0, 1), ContentByteRange.Select(17, 3, 4) };

        var body = HttpByteRangesBody.Of(store, mapping, "text/plain", ranges);

        using var destination = new MemoryStream();
        var copied = await body.CopyToAsync(destination, TestContext.CancellationToken);
        var expected = RecordedFixture.ReadBytes("range-multiple-206", "stdout.bin");
        CollectionAssert.AreEqual(expected, destination.ToArray());
        Assert.AreEqual(4, copied);
        Assert.AreEqual(4, body.FileByteCount);
        Assert.AreEqual(expected.Length, body.Length);
        Assert.HasCount(2, body.Parts);
        Assert.AreEqual(3, body.Parts[1].Range.FirstByte);
    }

    [TestMethod]
    public async Task CopyToAsync_FileShorterThanItsRanges_StopsAtTheFirstShortRangeWithoutTheEpilogue()
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"), Ascii(FileBody), FileTime, reportedLength: FileBody.Length + 5);
        var (store, mapping) = StoreAndMapping(fileSystem);
        var ranges = new[] { ContentByteRange.Select(FileBody.Length + 5, 0, 1), ContentByteRange.Select(FileBody.Length + 5, 18, 21) };

        var body = HttpByteRangesBody.Of(store, mapping, "text/plain", ranges);

        using var destination = new MemoryStream();
        var copied = await body.CopyToAsync(destination, TestContext.CancellationToken);
        Assert.IsLessThan(body.FileByteCount, copied);
        Assert.DoesNotContain("--SURL_BYTERANGES--", System.Text.Encoding.Latin1.GetString(destination.ToArray()));
    }

    [TestMethod]
    public void ContentFileParts_Of_OneFileRange_HasNoBytesAroundTheRange()
    {
        var (store, mapping) = StoreAndMapping(StandardFileSystem());
        var range = ContentByteRange.Select(17, 2, 6);

        var parts = HttpAnswerBody.ContentFileParts.Of(new HttpAnswerBody.ContentFile(store, mapping, range));

        Assert.AreEqual(5, parts.Length);
        Assert.AreEqual(5, parts.FileByteCount);
        Assert.IsTrue(parts.Parts.Single().Preamble.IsEmpty);
        Assert.IsTrue(parts.Epilogue.IsEmpty);
    }

    private static (ContentStore Store, ContentPathMapping Mapping) StoreAndMapping(IContentFileSystem fileSystem)
    {
        var store = new ContentStore(Root, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot);

        return (store, store.MapRequestPath("/file.txt"));
    }
}
