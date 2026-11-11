using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsJsonServer;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// <c>storage.objects.copy</c> and <c>storage.objects.rewrite</c> (ADR-0148, decision 7), written from the "Objects: copy" and
/// "Objects: rewrite" pages: a new generation of the destination with the source's bytes and stored fields, in one call.
/// </summary>
[TestClass]
public sealed class GcsJsonObjectCopyTests
{
    private const string Copy = "/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/b.txt";

    private const string Rewrite = "/storage/v1/b/photos/o/a.txt/rewriteTo/b/photos/o/b.txt";

    private static readonly string InvalidError = JsonError("400 Bad Request", "Invalid request.", "invalid");

    private static readonly string InvalidParameterError = JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter");

    private static readonly string ForbiddenError = JsonError("403 Forbidden", "Access denied.", "forbidden");

    private static readonly string ConditionError = JsonError("412 Precondition Failed", "Precondition Failed", "conditionNotMet");

    private static readonly string TooLargeError = JsonError("413 Payload Too Large", "Payload too large.", "uploadTooLarge");

    private static readonly string ConflictError = JsonError("409 Conflict", "Conflict.", "conflict");

    [TestMethod]
    public async Task ServeAsync_Copy_StoresANewObjectWithTheSourcesBytesAndFieldsAndMetagenerationOne()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", "/storage/v1/b/photos/o/a.txt", "{\"cacheControl\":\"no-cache\",\"metadata\":{\"k\":\"v\"}}");

        var response = await harness.SendWithBearerAsync("POST", Copy);

        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt", ",\"cacheControl\":\"no-cache\",\"metadata\":{\"k\":\"v\"}")), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
        Assert.AreEqual("hello world", harness.ReadFile("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyToAnotherBucket_WorksAndMakesTheFolders()
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("other");

        var response = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/other/o/d%2Fe%2Ff.txt");

        StringAssert.Contains(response, "\"name\":\"d/e/f.txt\",\"bucket\":\"other\"");
        Assert.AreEqual("hello world", harness.ReadFile("other/d/e/f.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyWithABody_TakesTheMetadataTheBodyNamesAndTheSourcesTypeWhenItNamesNone()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Copy, "{\"cacheControl\":\"no-store\",\"metadata\":{\"j\":\"w\"}}");

        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt", ",\"cacheControl\":\"no-store\",\"metadata\":{\"j\":\"w\"}")), response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyWithABodyNamingTheType_TakesIt()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Copy, "{\"contentType\":\"text/csv\"}");

        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt", contentType: "text/csv")), response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyOntoItself_IsANewGenerationOfTheSameBytes()
    {
        var harness = HarnessWithHello();
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddSeconds(5);

        var response = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/a.txt");

        StringAssert.Contains(response, "\"generation\":\"1793880005000000\"");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyOfAnObjectInAShare_Works()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        GcsServerHarness.WriteFile(shareFileSystem, "s.txt", "shared bytes");
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem)]);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("POST", "/storage/v1/b/shared/o/s.txt/copyTo/b/photos/o/t.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("shared bytes", harness.ReadFile("photos/t.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_Rewrite_AnswersTheFinishedRewriteResponseAndIgnoresTheTokenAndTheBound()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", $"{Rewrite}?rewriteToken=abc&maxBytesRewrittenPerCall=1");

        var resource = HelloObject("b.txt", "b.txt");
        Assert.AreEqual(Json($"{{\"kind\":\"storage#rewriteResponse\",\"totalBytesRewritten\":\"11\",\"objectSize\":\"11\",\"done\":true,\"resource\":{resource}}}"), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyWithoutAllowUploads_Is403Forbidden()
    {
        var harness = HarnessWithHello(new ContentExposureOptions { ListDirectories = true });

        var response = await harness.SendWithBearerAsync("POST", Copy);

        Assert.AreEqual(ForbiddenError, response);
    }

    [TestMethod]
    [DataRow("/storage/v1/b/photos/o/a.txt/copyTo/b/nowhere/o/b.txt", DisplayName = "no destination bucket")]
    [DataRow("/storage/v1/b/photos/o/missing.txt/copyTo/b/photos/o/b.txt", DisplayName = "no source")]
    [DataRow("/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/b.txt?sourceGeneration=5", DisplayName = "another source generation")]
    [DataRow("/storage/v1/b/photos/o/..%2Fa.txt/copyTo/b/photos/o/b.txt", DisplayName = "a source name the store cannot hold")]
    public async Task ServeAsync_CopyOfWhatIsNotThere_Is404NotFound(string target)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", target);

        Assert.AreEqual(NotFound, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyAtTheSourceGenerationNamed_Works()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", $"{Copy}?sourceGeneration={GcsServerHarness.NowGeneration}&ifSourceGenerationMatch={GcsServerHarness.NowGeneration}&ifSourceMetagenerationMatch=1");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("ifSourceGenerationMatch=5", DisplayName = "another source generation")]
    [DataRow("ifSourceMetagenerationMatch=2", DisplayName = "another source metageneration")]
    [DataRow($"ifSourceGenerationNotMatch={GcsServerHarness.NowGeneration}", DisplayName = "the source generation it must not be")]
    [DataRow("ifGenerationMatch=0", DisplayName = "no destination wanted, but it is there")]
    [DataRow("ifMetagenerationMatch=2", DisplayName = "another destination metageneration")]
    public async Task ServeAsync_CopyWhoseConditionFails_Is412AndChangesNothing(string query)
    {
        var harness = HarnessWithHello();
        harness.WriteFile("photos/b.txt", "old");

        var response = await harness.SendWithBearerAsync("POST", $"{Copy}?{query}");

        Assert.AreEqual(ConditionError, response);
        Assert.AreEqual("old", harness.ReadFile("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyWithIfGenerationMatchZeroAndNoDestination_Works()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", $"{Copy}?ifGenerationMatch=0");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("sourceGeneration=abc")]
    [DataRow("ifSourceGenerationMatch=abc")]
    [DataRow("ifGenerationMatch=abc")]
    public async Task ServeAsync_CopyWithAValueThatIsNoNumber_Is400InvalidParameter(string query)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", $"{Copy}?{query}");

        Assert.AreEqual(InvalidParameterError, response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyWithAParameterItDoesNotTake_Is501NotImplemented()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", $"{Copy}?versions=1");

        Assert.AreEqual(NotImplemented, response);
    }

    [TestMethod]
    [DataRow("not json", DisplayName = "text")]
    [DataRow("[]", DisplayName = "an array")]
    [DataRow("{\"contentType\":5}", DisplayName = "a type that is no string")]
    public async Task ServeAsync_CopyWithABodyThatIsNotTheJsonItTakes_Is400Invalid(string body)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Copy, body);

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyWithABodyPast1MiB_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Copy, $"{{\"cacheControl\":\"{new string('c', 1024 * 1024)}\"}}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyWithMetadataPast8KiB_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Copy, $"{{\"metadata\":{{\"k\":\"{new string('v', 8200)}\"}}}}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyWhoseFieldsTheIndexRefusesAsTooLarge_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Copy, $"{{\"contentType\":\"text/csv\",\"metadata\":{{\"k\":\"{new string('v', 8190)}\"}}}}");

        Assert.AreEqual(InvalidError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyWhenTheMetadataIndexIsFull_Is403QuotaExceeded()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, maxIndexEntries: 1);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        await harness.SendWithBearerAndBodyAsync("POST", "/upload/storage/v1/b/photos/o?uploadType=media&name=c.txt", "hello world");

        var response = await harness.SendWithBearerAsync("POST", Copy);

        Assert.AreEqual(JsonError("403 Forbidden", "The object metadata index is full.", "quotaExceeded"), response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyPastMaxFilesize_Is413UploadTooLargeAndStoresNothing()
    {
        var harness = HarnessWithHello(GcsServerHarness.Writable with { MaxUploadBytes = 4 });

        var response = await harness.SendWithBearerAsync("POST", Copy);

        Assert.AreEqual(TooLargeError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_CopyToANameEndingInASlash_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/dir%2F");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyToANamePast1024Bytes_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", $"/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/{new string('n', 1025)}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyToANameThePathMapperRefuses_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/..%2Fx");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_CopyBeneathAFileOrOntoAFolder_Is409Conflict()
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("photos/dir");

        var beneathAFile = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/a.txt%2Fx.txt");
        var ontoAFolder = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/photos/o/dir");

        Assert.AreEqual(ConflictError, beneathAFile);
        Assert.AreEqual(ConflictError, ontoAFolder);
    }

    [TestMethod]
    public async Task ServeAsync_CopyIntoAReadOnlyShare_Is403Forbidden()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true }]);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var intoAFolder = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/shared/o/dir%2Fs.txt");
        var intoTheShare = await harness.SendWithBearerAsync("POST", "/storage/v1/b/photos/o/a.txt/copyTo/b/shared/o/s.txt");

        Assert.AreEqual(ForbiddenError, intoAFolder);
        Assert.AreEqual(ForbiddenError, intoTheShare);
    }

    [TestMethod]
    public async Task ServeAsync_CopyWhenTheStoreRunsOutOfSpaceAtTheCommit_Is403ForbiddenAndStoresNothing()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, storeFaults: new ScriptedFaultPlan(new FaultRule("full", FaultKind.OutOfSpace)));
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("POST", Copy);

        Assert.AreEqual(ForbiddenError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ChosenErrorOnRewrite_IsAskedWithTheRewriteMethodIdAndNotTheCopys()
    {
        var harness = HarnessWithHello();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Reply = "503", Command = "storage.objects.rewrite" });

        var copy = await harness.SendWithBearerAsync("POST", Copy);
        var rewrite = await harness.SendWithBearerAsync("POST", Rewrite);

        StringAssert.StartsWith(copy, "HTTP/1.1 200 OK\r\n");
        StringAssert.StartsWith(rewrite, "HTTP/1.1 503 Service Unavailable\r\n");
    }
}
