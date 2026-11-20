using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The conditions of a <c>PUT</c> under <c>--allow-uploads</c> and the <c>ETag</c> of the file it
/// stores (ADR-0184, decision 7): a failed condition is <c>412</c> and the file stands.
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    private static readonly string OldHttpDate = Now.AddDays(-1).ToString("r", CultureInfo.InvariantCulture);

    private static readonly string NewHttpDate = Now.AddDays(1).ToString("r", CultureInfo.InvariantCulture);

    private static string StoredETag(IContentFileSystem fileSystem, string name)
    {
        var path = Path.Join(UploadRoot, name);

        return string.Create(CultureInfo.InvariantCulture, $"\"{fileSystem.GetLastWriteTimeUtc(path).UtcTicks:x}-{fileSystem.GetFileLength(path):x}\"");
    }

    private async Task<HttpAnswer> PutWithAsync(ContentStore store, string name, params (string Name, string Value)[] fields)
    {
        var request = new HttpRequest("PUT", "/" + name, "example.test", [.. fields.Select(field => Field(field.Name, field.Value))], new UnitTestCopyingRequestBody(RecordedUpload));

        return await UploadAnswerer(store).AnswerAsync(request, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task AnswerAsync_PutOfANewFile_Carries201WithTheETagOfWhatWasStored()
    {
        var (fileSystem, store) = UploadStore();

        var answer = await PutWithAsync(store, "up.bin");

        Assert.AreEqual(201, answer.Status.Code);
        Assert.AreEqual(StoredETag(fileSystem, "up.bin"), ETagOf(answer));
    }

    [TestMethod]
    public async Task AnswerAsync_PutOverAFile_Carries204WithTheETagOfTheNewFile()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");
        var oldETag = StoredETag(fileSystem, "up.bin");

        var answer = await PutWithAsync(store, "up.bin");

        Assert.AreEqual(204, answer.Status.Code);
        Assert.AreEqual(StoredETag(fileSystem, "up.bin"), ETagOf(answer));
        Assert.AreNotEqual(oldETag, ETagOf(answer));
    }

    [TestMethod]
    public async Task AnswerAsync_PutWhoseStoredStatusCannotBeRead_StillAnswersWithoutAnETag()
    {
        var fileSystem = new UnitTestStatusFailingAfterWriteContentFileSystem(new InMemoryContentFileSystem(new FixedTimeProvider(Now)));
        var store = new ContentStore(UploadRoot, fileSystem, new ContentExposureOptions { AllowUploads = true });

        var answer = await PutWithAsync(store, "up.bin");

        Assert.AreEqual(201, answer.Status.Code);
        Assert.IsFalse(answer.Fields.Any(field => field.Key == "ETag"));
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfMatchOnAMissingFile_Is412AndStoresNothing()
    {
        var (fileSystem, store) = UploadStore();

        var answer = await PutWithAsync(store, "up.bin", ("If-Match", "*"));

        Assert.AreEqual(412, answer.Status.Code);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(UploadRoot, "up.bin")));
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfMatchOfAnotherTag_Is412AndTheFileIsUnchanged()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");

        var answer = await PutWithAsync(store, "up.bin", ("If-Match", "\"other\""));

        Assert.AreEqual(412, answer.Status.Code);
        CollectionAssert.AreEqual("old"u8.ToArray(), ReadStored(fileSystem, "up.bin"));
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfMatchOfTheFilesTag_IsStored()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");

        var answer = await PutWithAsync(store, "up.bin", ("If-Match", StoredETag(fileSystem, "up.bin")));

        Assert.AreEqual(204, answer.Status.Code);
        CollectionAssert.AreEqual(RecordedUpload, ReadStored(fileSystem, "up.bin"));
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfNoneMatchStarOnAnExistingFile_Is412()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");

        var answer = await PutWithAsync(store, "up.bin", ("If-None-Match", "*"));

        Assert.AreEqual(412, answer.Status.Code);
        CollectionAssert.AreEqual("old"u8.ToArray(), ReadStored(fileSystem, "up.bin"));
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfNoneMatchStarOnAMissingFile_IsStored()
    {
        var (_, store) = UploadStore();

        var answer = await PutWithAsync(store, "up.bin", ("If-None-Match", "*"));

        Assert.AreEqual(201, answer.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfNoneMatchListingTheFilesTagWeakOrStrong_Is412()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");
        var tag = StoredETag(fileSystem, "up.bin");

        var weak = await PutWithAsync(store, "up.bin", ("If-None-Match", $"\"other\", W/{tag}"));
        var strong = await PutWithAsync(store, "up.bin", ("If-None-Match", tag));

        Assert.AreEqual(412, weak.Status.Code);
        Assert.AreEqual(412, strong.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfNoneMatchListingOnlyOtherTags_IsStored()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");

        var answer = await PutWithAsync(store, "up.bin", ("If-None-Match", "\"other\""));

        Assert.AreEqual(204, answer.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfUnmodifiedSince_Is412OnANewerFileAndStoredOtherwise()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");

        var newer = await PutWithAsync(store, "up.bin", ("If-Unmodified-Since", OldHttpDate));
        var older = await PutWithAsync(store, "up.bin", ("If-Unmodified-Since", NewHttpDate));

        Assert.AreEqual(412, newer.Status.Code);
        Assert.AreEqual(204, older.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfModifiedSinceAndIfUnmodifiedSinceOnAMissingFile_AreIgnored()
    {
        var (_, store) = UploadStore();

        var answer = await PutWithAsync(store, "up.bin", ("If-Modified-Since", NewHttpDate), ("If-Unmodified-Since", OldHttpDate));

        Assert.AreEqual(201, answer.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_PutIfModifiedSinceOnAnExistingFile_IsIgnored()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old");

        var answer = await PutWithAsync(store, "up.bin", ("If-Modified-Since", NewHttpDate));

        Assert.AreEqual(204, answer.Status.Code);
    }
}
