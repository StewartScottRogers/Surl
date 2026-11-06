using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// A <c>PUT</c> under <c>--allow-uploads</c> is stored and answered <c>201</c> when it creates the
/// file and <c>204</c> when it replaces one (ADR-0132, decision 2), as the bytes recorded from
/// pinned upstream curl 8.21.0 show (<c>Fixtures/put-created-201</c>, <c>put-replaced-204</c>).
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    private static readonly string UploadRoot = InMemoryContentFileSystem.RootPath;

    private static readonly byte[] RecordedUpload = Encoding.ASCII.GetBytes(new string('a', 16));

    [TestMethod]
    public async Task ServeAsync_RecordedPutOfANewFile_StoresItAndSendsTheRecorded201()
    {
        var (fileSystem, store) = UploadStore();

        var (connection, log) = await ServeWithStoreAsync(store, RecordedFixture.ReadRequestBytes("put-created-201"));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("put-created-201", "response.bin"), connection.WrittenBytes);
        CollectionAssert.AreEqual(RecordedUpload, ReadStored(fileSystem, "up.bin"));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual($"PUT /up.bin: 201, 16 bytes stored at {Path.Join(UploadRoot, "up.bin")}", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedPutOverAFile_ReplacesItAndSendsTheRecorded204()
    {
        var (fileSystem, store) = UploadStore();
        WriteStored(fileSystem, "up.bin", "old bytes");

        var (connection, _) = await ServeWithStoreAsync(store, RecordedFixture.ReadRequestBytes("put-replaced-204"));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("put-replaced-204", "response.bin"), connection.WrittenBytes);
        CollectionAssert.AreEqual(RecordedUpload, ReadStored(fileSystem, "up.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_PostUnderAllowUploads_Is405AllowingPut()
    {
        var (_, store) = UploadStore();

        var (connection, _) = await ServeWithStoreAsync(store, RecordedFixture.ReadRequestBytes("post-refused-405"));

        StringAssert.Contains(Latin1(connection.WrittenBytes), "Allow: GET, HEAD, PUT\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_PutWithNoBody_StoresAnEmptyFile()
    {
        var (fileSystem, store) = UploadStore();
        var body = new UnitTestCopyingRequestBody([], isAnnounced: false);

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", "/empty.bin", body), TestContext.CancellationToken);

        Assert.AreEqual(201, answer.Status.Code);
        Assert.AreEqual(0, body.CopyCount);
        Assert.IsEmpty(ReadStored(fileSystem, "empty.bin"));
    }

    [TestMethod]
    public async Task AnswerAsync_PutWithABodyItCannotRead_IsRefusedWith400()
    {
        var (_, store) = UploadStore();

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody([], isReadable: false)), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.BadRequest, answer.Status);
        Assert.IsTrue(answer.IsRefusal);
    }

    [TestMethod]
    [DataRow("/folder", DisplayName = "A directory")]
    [DataRow("/missing/up.bin", DisplayName = "No such directory")]
    public async Task AnswerAsync_PutWhereNoFileCanGo_Is409LeavingTheBodyUnread(string target)
    {
        var (fileSystem, store) = UploadStore();
        fileSystem.CreateDirectory(Path.Join(UploadRoot, "folder"));
        var body = new UnitTestCopyingRequestBody(RecordedUpload);

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", target, body), TestContext.CancellationToken);

        Assert.AreEqual(409, answer.Status.Code);
        Assert.IsFalse(answer.IsRefusal);
        Assert.IsTrue(answer.LeavesRequestBodyUnread);
        Assert.AreEqual(0, body.CopyCount);
    }

    [TestMethod]
    [DataRow("/.hidden", DisplayName = "A hidden file")]
    [DataRow("/../up.bin", DisplayName = "A refused path")]
    public async Task AnswerAsync_PutTheStoreDoesNotPermit_Is403(string target)
    {
        var (_, store) = UploadStore();

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", target, new UnitTestCopyingRequestBody(RecordedUpload)), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Forbidden, answer.Status);
        Assert.IsFalse(answer.IsRefusal);
    }

    [TestMethod]
    public async Task AnswerAsync_PutPastTheUploadLimit_IsRefusedWith413AndStoresNothing()
    {
        var (fileSystem, store) = UploadStore(maxUploadBytes: 10);

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(RecordedUpload)), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.ContentTooLarge, answer.Status);
        Assert.IsTrue(answer.IsRefusal);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(UploadRoot, "up.bin")));
    }

    [TestMethod]
    public async Task AnswerAsync_PutWhoseBodyEndsEarly_IsRefusedWith400AndStoresNothing()
    {
        var (fileSystem, store) = UploadStore();
        var body = new UnitTestCopyingRequestBody(RecordedUpload, HttpRequestBodyDiscardOutcome.Incomplete);

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", "/up.bin", body), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.BadRequest, answer.Status);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(UploadRoot, "up.bin")));
    }

    [TestMethod]
    public async Task AnswerAsync_PutWhoseTargetBecomesADirectoryWhileItArrives_Is403()
    {
        var (fileSystem, store) = UploadStore();
        var body = new UnitTestCopyingRequestBody(RecordedUpload, beforeCopy: () => fileSystem.CreateDirectory(Path.Join(UploadRoot, "up.bin")));

        var answer = await UploadAnswerer(store).AnswerAsync(Request("PUT", "/up.bin", body), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Forbidden, answer.Status);
        Assert.IsFalse(answer.LeavesRequestBodyUnread);
    }

    [TestMethod]
    public async Task AnswerAsync_PutWhoseLoginBindsTheBody_StoresItOnceTheBodyCheckAccepts()
    {
        var (fileSystem, store) = UploadStore();
        var bodyCheck = new UnitTestRequestBodyCheck(_ =>
            new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, ["Next"], "ann", new CheckedLogin("AWS4-HMAC-SHA256", "ann", true)));
        var log = new RecordingExchangeLog();

        var answer = await UploadAnswerer(store, BodyBoundLogin(bodyCheck), log).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(RecordedUpload)), TestContext.CancellationToken);

        Assert.AreEqual(201, answer.Status.Code);
        CollectionAssert.AreEqual(SHA256.HashData(RecordedUpload), bodyCheck.JudgedBodySha256s.Single());
        CollectionAssert.AreEqual(RecordedUpload, ReadStored(fileSystem, "up.bin"));
        Assert.AreEqual(new KeyValuePair<string, string>("WWW-Authenticate", "Next"), answer.Fields[^1]);
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 ann", log.Notes[0]);
    }

    [TestMethod]
    public async Task AnswerAsync_PutWhoseBodyIsNotTheOneSigned_IsRefusedAndStoresNothing()
    {
        var (fileSystem, store) = UploadStore();
        var bodyCheck = new UnitTestRequestBodyCheck(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, [BasicChallenge], null));

        var answer = await UploadAnswerer(store, BodyBoundLogin(bodyCheck)).AnswerAsync(Request("PUT", "/up.bin", new UnitTestCopyingRequestBody(RecordedUpload)), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Unauthorized, answer.Status);
        Assert.IsTrue(answer.IsRefusal);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(UploadRoot, "up.bin")));
    }

    [TestMethod]
    public async Task AnswerAsync_PutWhoseLoginBindsTheBodyWhereNoFileCanGo_ReadsAndJudgesTheBodyThenIs409()
    {
        var (_, store) = UploadStore();
        var bodyCheck = new UnitTestRequestBodyCheck(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "ann"));
        var body = new UnitTestCopyingRequestBody(RecordedUpload);

        var answer = await UploadAnswerer(store, BodyBoundLogin(bodyCheck)).AnswerAsync(Request("PUT", "/missing/up.bin", body), TestContext.CancellationToken);

        Assert.AreEqual(409, answer.Status.Code);
        Assert.IsFalse(answer.LeavesRequestBodyUnread);
        Assert.AreEqual(1, body.CopyCount);
        Assert.HasCount(1, bodyCheck.JudgedBodySha256s);
    }

    private static UnitTestAuthenticationPolicy BodyBoundLogin(UnitTestRequestBodyCheck bodyCheck) =>
        new(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "ann", BodyCheck: bodyCheck));

    private static (InMemoryContentFileSystem FileSystem, ContentStore Store) UploadStore(long maxUploadBytes = ContentExposureOptions.DefaultMaxUploadBytes)
    {
        var fileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(Now));

        return (fileSystem, new ContentStore(UploadRoot, fileSystem, new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = maxUploadBytes }));
    }

    private HttpRequestAnswerer UploadAnswerer(ContentStore store, IAuthenticationPolicy? policy = null, RecordingExchangeLog? log = null) =>
        new(Context(log ?? new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken), store, (policy ?? new AnonymousAuthenticationPolicy()).StartHttpConnection(null));

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeWithStoreAsync(ContentStore store, byte[] request)
    {
        var connection = new InMemoryConnection(RecordedFixture.Whole(request));
        var log = new RecordingExchangeLog();

        await new HttpProtocolServer(store, new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken));

        return (connection, log);
    }

    private static byte[] ReadStored(InMemoryContentFileSystem fileSystem, string name)
    {
        using var stream = fileSystem.OpenFileForAsyncRead(Path.Join(UploadRoot, name));
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    private static void WriteStored(InMemoryContentFileSystem fileSystem, string name, string content)
    {
        using var stream = fileSystem.CreateFileForAsyncWrite(Path.Join(UploadRoot, name));
        stream.Write(Encoding.ASCII.GetBytes(content));
    }
}
