using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// DELETE Object and the one answer for every request ADR-0110 decision 1 does not list, from the
/// XML API pages "DELETE Object" and "HTTP status and error codes".
/// </summary>
[TestClass]
public sealed class GcsRequestAnswererTests
{
    private const string NotImplementedMessage = "A header or query you provided requested a function that is not implemented.";

    [TestMethod]
    public async Task ServeAsync_DeleteObject_Is204AndRemovesTheFile()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt");

        Assert.AreEqual($"HTTP/1.1 204 No Content\r\n{GcsServerHarness.CommonFields}Connection: close\r\n\r\n", response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("/photos/a.txt", DisplayName = "no such object")]
    [DataRow("/photos/2026", DisplayName = "a folder")]
    [DataRow("/photos/%2e%2e/x", DisplayName = "a name the store cannot hold")]
    public async Task ServeAsync_DeleteOfNoObject_Is404NoSuchKey(string target)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("DELETE", target);

        Assert.AreEqual(GcsServerHarness.ExpectedError("404 Not Found", "NoSuchKey", "The specified key does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_DeleteWithoutAllowUploads_Is403AccessDenied()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt");

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "AccessDenied", "Access denied."), response);
        Assert.AreEqual(ContentEntryKind.File, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteInAReadOnlyShare_Is403AccessDenied()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        GcsServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var share = new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [share]);

        var response = await harness.SendWithBearerAsync("DELETE", "/shared/a.txt");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    [DataRow($"x-goog-if-generation-match: {GcsServerHarness.NowGeneration}", "204 No Content", DisplayName = "the current generation")]
    [DataRow("x-goog-if-generation-match: 5", "412 Precondition Failed", DisplayName = "another generation")]
    public async Task ServeAsync_DeleteWithAGenerationCondition_DeletesOnlyTheGenerationNamed(string field, string status)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("DELETE", "/photos/a.txt", field);

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
    }

    [TestMethod]
    [DataRow("POST", "/photos/a.txt", "", DisplayName = "a POST")]
    [DataRow("GET", "/photos/a.txt?acl", "", DisplayName = "an object subresource")]
    [DataRow("GET", "/photos/a.txt?X-Goog-Signature=ab", "", DisplayName = "a signed URL")]
    [DataRow("GET", "/photos?versions", "", DisplayName = "a version listing")]
    [DataRow("GET", "/photos?generation-marker=1", "", DisplayName = "a generation marker")]
    [DataRow("PUT", "/photos", "", DisplayName = "bucket creation")]
    [DataRow("DELETE", "/photos", "", DisplayName = "bucket deletion")]
    [DataRow("PUT", "/photos/a.txt?generation=1", "", DisplayName = "a generation on a write")]
    [DataRow("DELETE", "/photos/a.txt?generation=1", "", DisplayName = "a generation on a delete")]
    [DataRow("PUT", "/photos/a.txt", "x-goog-copy-source: /photos/b.txt", DisplayName = "a copy")]
    [DataRow("GET", "/photos/a.txt", "x-goog-encryption-algorithm: AES256", DisplayName = "customer-supplied encryption")]
    [DataRow("GET", "/?project=1", "", DisplayName = "a query on the service")]
    [DataRow("OPTIONS", "/photos/a.txt", "", DisplayName = "OPTIONS")]
    public async Task ServeAsync_UnlistedRequest_Is400NotImplemented(string method, string target, string field)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        string[] fields = field.Length == 0 ? ["Content-Length: 0"] : [field, "Content-Length: 0"];

        var response = await harness.SendWithBearerAsync(method, target, fields);

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "NotImplemented", NotImplementedMessage), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }
}
