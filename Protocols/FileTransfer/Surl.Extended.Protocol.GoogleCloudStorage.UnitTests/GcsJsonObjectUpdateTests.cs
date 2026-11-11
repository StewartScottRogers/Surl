using Surl.Content;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsJsonServer;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// <c>storage.objects.patch</c> and <c>storage.objects.update</c> (ADR-0148, decision 7), written from the "Objects: patch" and
/// "Objects: update" pages: a patch merges and a <c>null</c> removes, an update replaces; both raise the metageneration
/// and leave the bytes and the generation alone.
/// </summary>
[TestClass]
public sealed class GcsJsonObjectUpdateTests
{
    private const string Object = "/storage/v1/b/photos/o/a.txt";

    private static readonly string InvalidError = JsonError("400 Bad Request", "Invalid request.", "invalid");

    private static readonly string ForbiddenError = JsonError("403 Forbidden", "Access denied.", "forbidden");

    [TestMethod]
    public async Task ServeAsync_Patch_MergesTheBodyAndRaisesTheMetagenerationOnly()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"contentType\":\"text/csv\",\"cacheControl\":\"no-cache\",\"metadata\":{\"k\":\"v\"},\"acl\":[]}");

        Assert.AreEqual(Json(HelloObject(extraMembers: ",\"cacheControl\":\"no-cache\",\"metadata\":{\"k\":\"v\"}", metageneration: "2", contentType: "text/csv")), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_Patch_IsReadBackOverTheXmlApiAndRaisesTheMetagenerationEachTime()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"contentLanguage\":\"en\"}");
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"contentDisposition\":\"inline\"}");

        var xml = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.Contains(xml, "x-goog-metageneration: 3\r\n");
        StringAssert.Contains(xml, "Content-Language: en\r\n");
        StringAssert.Contains(xml, "Content-Disposition: inline\r\n");
        StringAssert.EndsWith(xml, "hello world");
    }

    [TestMethod]
    public async Task ServeAsync_PatchWithNulls_RemovesWhatTheyNameAndKeepsTheRest()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"cacheControl\":\"no-cache\",\"contentType\":\"text/csv\",\"metadata\":{\"k\":\"v\",\"j\":\"w\"}}");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"cacheControl\":null,\"contentType\":null,\"metadata\":{\"k\":null}}");

        Assert.AreEqual(Json(HelloObject(extraMembers: ",\"metadata\":{\"j\":\"w\"}", metageneration: "3")), response);
    }

    [TestMethod]
    public async Task ServeAsync_PatchWithNullMetadata_RemovesAllOfIt()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"metadata\":{\"k\":\"v\"}}");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"metadata\":null}");

        Assert.AreEqual(Json(HelloObject(metageneration: "3")), response);
    }

    [TestMethod]
    public async Task ServeAsync_PatchWithoutMetadata_KeepsWhatIsStored()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"metadata\":{\"k\":\"v\"}}");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{}");

        Assert.AreEqual(Json(HelloObject(extraMembers: ",\"metadata\":{\"k\":\"v\"}", metageneration: "3")), response);
    }

    [TestMethod]
    public async Task ServeAsync_Update_ReplacesAllWritableMetadata()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"cacheControl\":\"no-cache\",\"metadata\":{\"k\":\"v\"}}");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", Object, "{\"contentType\":\"text/csv\",\"contentLanguage\":\"en\",\"metadata\":{\"j\":\"w\",\"x\":null}}");

        Assert.AreEqual(Json(HelloObject(extraMembers: ",\"contentLanguage\":\"en\",\"metadata\":{\"j\":\"w\"}", metageneration: "3", contentType: "text/csv")), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_UpdateWithNoMetadata_ClearsIt()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"metadata\":{\"k\":\"v\"}}");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", Object, "{\"contentType\":\"text/plain\"}");

        Assert.AreEqual(Json(HelloObject(metageneration: "3")), response);
    }

    [TestMethod]
    public async Task ServeAsync_UpdateWithNullMetadata_ClearsIt()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{\"metadata\":{\"k\":\"v\"}}");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", Object, "{\"metadata\":null}");

        Assert.AreEqual(Json(HelloObject(metageneration: "3")), response);
    }

    [TestMethod]
    public async Task ServeAsync_PatchOfAnObjectInsertedOverJson_KeepsItsBytesAndGeneration()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("POST", "/upload/storage/v1/b/photos/o?uploadType=media&name=b.txt", "hello world", "Content-Type: text/plain");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/storage/v1/b/photos/o/b.txt", "{\"cacheControl\":\"no-cache\"}");

        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt", ",\"cacheControl\":\"no-cache\"", "2")), response);
    }

    [TestMethod]
    public async Task ServeAsync_PatchWithoutAllowUploads_Is403Forbidden()
    {
        var harness = HarnessWithHello(new ContentExposureOptions { ListDirectories = true });

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{}");

        Assert.AreEqual(ForbiddenError, response);
    }

    [TestMethod]
    [DataRow("PATCH")]
    [DataRow("PUT")]
    public async Task ServeAsync_ChangeOfNoObject_Is404NotFound(string method)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync(method, "/storage/v1/b/photos/o/missing.txt", "{}");

        Assert.AreEqual(NotFound, response);
    }

    [TestMethod]
    [DataRow($"ifGenerationMatch={GcsServerHarness.NowGeneration}&ifMetagenerationMatch=1", "200 OK", DisplayName = "conditions that hold")]
    [DataRow("ifMetagenerationMatch=2", "412 Precondition Failed", DisplayName = "another metageneration")]
    [DataRow("ifGenerationMatch=5", "412 Precondition Failed", DisplayName = "another generation")]
    [DataRow("alt=media", "400 Bad Request", DisplayName = "alt media")]
    [DataRow("ifGenerationMatch=abc", "400 Bad Request", DisplayName = "a condition that is no number")]
    public async Task ServeAsync_PatchWithAQuery_ChangesOnlyWhenItHolds(string query, string status)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", $"{Object}?{query}", "{\"cacheControl\":\"no-cache\"}");

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
        var xml = await harness.SendWithBearerAsync("GET", "/photos/a.txt");
        Assert.AreEqual(status == "200 OK", xml.Contains("Cache-Control: no-cache", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("not json", DisplayName = "text")]
    [DataRow("[]", DisplayName = "an array")]
    [DataRow("{\"contentType\":5}", DisplayName = "a type that is no string")]
    [DataRow("{\"metadata\":{\"k\":true}}", DisplayName = "a metadata value that is no string")]
    public async Task ServeAsync_ChangeWithABodyThatIsNotTheJsonItTakes_Is400Invalid(string body)
    {
        var harness = HarnessWithHello();

        var patch = await harness.SendWithBearerAndBodyAsync("PATCH", Object, body);
        var update = await harness.SendWithBearerAndBodyAsync("PUT", Object, body);

        Assert.AreEqual(InvalidError, patch);
        Assert.AreEqual(InvalidError, update);
    }

    [TestMethod]
    public async Task ServeAsync_ChangeWithNoBody_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("PATCH", Object);

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ChangeWithABodyPast1MiB_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, $"{{\"cacheControl\":\"{new string('c', 1024 * 1024)}\"}}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ChangeWithMetadataPast8KiB_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, $"{{\"metadata\":{{\"k\":\"{new string('v', 8200)}\"}}}}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ChangeWhoseFieldsTheIndexRefusesAsTooLarge_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, $"{{\"contentType\":\"text/csv\",\"metadata\":{{\"k\":\"{new string('v', 8190)}\"}}}}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ChangeWhenTheMetadataIndexIsFull_Is403QuotaExceeded()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, maxIndexEntries: 1);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        await harness.SendWithBearerAndBodyAsync("POST", "/upload/storage/v1/b/photos/o?uploadType=media&name=b.txt", "hello world");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{}");

        Assert.AreEqual(JsonError("403 Forbidden", "The object metadata index is full.", "quotaExceeded"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ChangeOfAFileInAShare_Is403BecauseTheIndexKeepsNoEntryForIt()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        GcsServerHarness.WriteFile(shareFileSystem, "s.txt", "hello world");
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/storage/v1/b/shared/o/s.txt", "{}");

        Assert.AreEqual(ForbiddenError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ChosenErrorOnPatch_IsAskedWithThePatchMethodId()
    {
        var harness = HarnessWithHello();
        harness.Faults = new Surl.Protocol.Abstractions.ScriptedFaultPlan(new Surl.Protocol.Abstractions.FaultRule("f1", Surl.Protocol.Abstractions.FaultKind.Error) { Scheme = "gs", Reply = "503", Command = "storage.objects.patch" });

        var patch = await harness.SendWithBearerAndBodyAsync("PATCH", Object, "{}");
        var update = await harness.SendWithBearerAndBodyAsync("PUT", Object, "{}");

        StringAssert.StartsWith(patch, "HTTP/1.1 503 Service Unavailable\r\n");
        StringAssert.StartsWith(update, "HTTP/1.1 200 OK\r\n");
    }
}
