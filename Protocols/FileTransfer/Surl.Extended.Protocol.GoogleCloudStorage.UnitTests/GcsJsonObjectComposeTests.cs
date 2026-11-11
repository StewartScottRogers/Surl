using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsJsonServer;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// <c>storage.objects.compose</c> (ADR-0148, decision 7), written from the "Objects: compose" page: one to 32 sources, in
/// order, become one new generation of the destination.
/// </summary>
[TestClass]
public sealed class GcsJsonObjectComposeTests
{
    private const string Compose = "/storage/v1/b/photos/o/c.txt/compose";

    private static readonly string InvalidError = JsonError("400 Bad Request", "Invalid request.", "invalid");

    private static readonly string RequiredError = JsonError("400 Bad Request", "Required parameter missing.", "required");

    private static readonly string ForbiddenError = JsonError("403 Forbidden", "Access denied.", "forbidden");

    private static readonly string ConditionError = JsonError("412 Precondition Failed", "Precondition Failed", "conditionNotMet");

    private static GcsServerHarness HarnessWithParts()
    {
        var harness = HarnessWithHello();
        harness.WriteFile("photos/b.txt", "!!");

        return harness;
    }

    private static string Sources(params string[] names) =>
        $"{{\"kind\":\"storage#composeRequest\",\"sourceObjects\":[{string.Join(',', names.Select(name => $"{{\"name\":\"{name}\"}}"))}]}}";

    [TestMethod]
    public async Task ServeAsync_Compose_ConcatenatesTheSourcesInOrderIntoOneNewGeneration()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, Sources("a.txt", "b.txt", "a.txt"));

        StringAssert.Contains(response, "\"name\":\"c.txt\",\"bucket\":\"photos\"");
        StringAssert.Contains(response, "\"size\":\"24\"");
        StringAssert.Contains(response, "\"contentType\":\"application/octet-stream\"");
        Assert.AreEqual("hello world!!hello world", harness.ReadFile("photos/c.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithADestination_StoresItsMetadata()
    {
        var harness = HarnessWithParts();
        var body = "{\"sourceObjects\":[{\"name\":\"a.txt\"}],\"destination\":{\"contentType\":\"text/plain\",\"metadata\":{\"k\":\"v\"}}}";

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, body);

        StringAssert.Contains(response, "\"contentType\":\"text/plain\"");
        StringAssert.Contains(response, "\"metadata\":{\"k\":\"v\"}");
    }

    [TestMethod]
    public async Task ServeAsync_ComposeOntoASource_AppendsToIt()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", "/storage/v1/b/photos/o/a.txt/compose", Sources("a.txt", "b.txt"));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world!!", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithThe32SourcesItTakes_Works()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, Sources([.. Enumerable.Repeat("b.txt", 32)]));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(64, harness.ReadFile("photos/c.txt").Length);
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWith33Sources_Is400Invalid()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, Sources([.. Enumerable.Repeat("b.txt", 33)]));

        Assert.AreEqual(InvalidError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/c.txt"));
    }

    [TestMethod]
    [DataRow("{}", DisplayName = "no sources")]
    [DataRow("{\"sourceObjects\":null}", DisplayName = "null sources")]
    [DataRow("{\"sourceObjects\":[]}", DisplayName = "an empty list")]
    public async Task ServeAsync_ComposeWithoutSources_Is400Required(string body)
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, body);

        Assert.AreEqual(RequiredError, response);
    }

    [TestMethod]
    [DataRow("not json", DisplayName = "text")]
    [DataRow("[]", DisplayName = "an array")]
    [DataRow("{\"sourceObjects\":5}", DisplayName = "sources that are no list")]
    [DataRow("{\"sourceObjects\":[5]}", DisplayName = "a source that is no object")]
    [DataRow("{\"sourceObjects\":[{}]}", DisplayName = "a source with no name")]
    [DataRow("{\"sourceObjects\":[{\"name\":\"\"}]}", DisplayName = "a source with an empty name")]
    [DataRow("{\"sourceObjects\":[{\"name\":5}]}", DisplayName = "a source whose name is no string")]
    [DataRow("{\"sourceObjects\":[{\"name\":\"a.txt\",\"generation\":\"x\"}]}", DisplayName = "a generation that is no number")]
    [DataRow("{\"sourceObjects\":[{\"name\":\"a.txt\",\"generation\":true}]}", DisplayName = "a generation that is a boolean")]
    [DataRow("{\"sourceObjects\":[{\"name\":\"a.txt\",\"objectPreconditions\":{\"ifGenerationMatch\":\"x\"}}]}", DisplayName = "a precondition that is no number")]
    [DataRow("{\"sourceObjects\":[{\"name\":\"a.txt\"}],\"destination\":5}", DisplayName = "a destination that is no object")]
    [DataRow("{\"sourceObjects\":[{\"name\":\"a.txt\"}],\"destination\":{\"contentType\":5}}", DisplayName = "a destination field that is no string")]
    public async Task ServeAsync_ComposeWithABodyThatIsNotTheJsonItTakes_Is400Invalid(string body)
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, body);

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithNoBody_Is400Invalid()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAsync("POST", Compose);

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithABodyPast1MiB_Is400Invalid()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, $"{{\"padding\":\"{new string('p', 1024 * 1024)}\"}}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithAMissingSource_Is404NotFoundAndStoresNothing()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, Sources("a.txt", "missing.txt"));

        Assert.AreEqual(NotFound, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/c.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithASourceNameTheStoreCannotHold_Is404NotFound()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, Sources("../a.txt"));

        Assert.AreEqual(NotFound, response);
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithTheSourceGenerationsAndPreconditionsNamed_Works()
    {
        var harness = HarnessWithParts();
        var source = $"{{\"name\":\"a.txt\",\"generation\":\"{GcsServerHarness.NowGeneration}\",\"objectPreconditions\":{{\"ifGenerationMatch\":{GcsServerHarness.NowGeneration}}}}}";

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, $"{{\"sourceObjects\":[{source}]}}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithASourceAtAnotherGeneration_Is404NotFound()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, "{\"sourceObjects\":[{\"name\":\"a.txt\",\"generation\":5}]}");

        Assert.AreEqual(NotFound, response);
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithAFailingSourcePrecondition_Is412()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, "{\"sourceObjects\":[{\"name\":\"a.txt\",\"objectPreconditions\":{\"ifGenerationMatch\":5}}]}");

        Assert.AreEqual(ConditionError, response);
    }

    [TestMethod]
    [DataRow("ifGenerationMatch=0", "old", DisplayName = "no destination wanted, but it is there")]
    [DataRow("ifGenerationMatch=0", null, DisplayName = "no destination wanted and none there")]
    [DataRow("ifMetagenerationMatch=2", "old", DisplayName = "another destination metageneration")]
    public async Task ServeAsync_ComposeWithAConditionOnTheDestination_ComposesOnlyWhenItHolds(string query, string? existing)
    {
        var harness = HarnessWithParts();
        if (existing is not null)
        {
            harness.WriteFile("photos/c.txt", existing);
        }

        var response = await harness.SendWithBearerAndBodyAsync("POST", $"{Compose}?{query}", Sources("a.txt"));

        Assert.AreEqual(existing is null, response.StartsWith("HTTP/1.1 200 OK\r\n", StringComparison.Ordinal));
        Assert.AreEqual(existing is null ? "hello world" : existing, harness.ReadFile("photos/c.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithAConditionThatIsNoNumber_Is400InvalidParameter()
    {
        var harness = HarnessWithParts();

        var response = await harness.SendWithBearerAndBodyAsync("POST", $"{Compose}?ifGenerationMatch=x", Sources("a.txt"));

        Assert.AreEqual(JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ComposeWithoutAllowUploads_Is403Forbidden()
    {
        var harness = HarnessWithParts();
        var readOnly = HarnessWithHello(new ContentExposureOptions { ListDirectories = true });

        var response = await readOnly.SendWithBearerAndBodyAsync("POST", Compose, Sources("a.txt"));

        Assert.AreEqual(ForbiddenError, response);
        Assert.AreEqual("!!", harness.ReadFile("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ComposePastMaxFilesize_Is413UploadTooLarge()
    {
        var harness = HarnessWithHello(GcsServerHarness.Writable with { MaxUploadBytes = 15 });

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, Sources("a.txt", "a.txt"));

        Assert.AreEqual(JsonError("413 Payload Too Large", "Payload too large.", "uploadTooLarge"), response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/c.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ComposeIntoAReadOnlyShare_Is403Forbidden()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        GcsServerHarness.WriteFile(shareFileSystem, "s.txt", "shared");
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true }]);

        var response = await harness.SendWithBearerAndBodyAsync("POST", "/storage/v1/b/shared/o/t.txt/compose", Sources("s.txt"));

        Assert.AreEqual(ForbiddenError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ChosenErrorOnCompose_IsAskedWithTheComposeMethodId()
    {
        var harness = HarnessWithParts();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Reply = "503", Command = "storage.objects.compose" });

        var response = await harness.SendWithBearerAndBodyAsync("POST", Compose, Sources("a.txt"));

        StringAssert.StartsWith(response, "HTTP/1.1 503 Service Unavailable\r\n");
    }
}
