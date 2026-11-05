using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The tag list, written from the OCI Distribution Specification v1.1.0's "Listing Tags"
/// (ADR-0115, decision 5.2).
/// </summary>
[TestClass]
public sealed class OciTagsListTests
{
    private const string Manifest = "{\"schemaVersion\":2}";

    [TestMethod]
    public async Task AnswerAsync_NoParameters_ListsEveryTagInOrdinalOrder()
    {
        var harness = Repository("v2", "latest", "v1", "latest", "bad tag");

        var response = await harness.SendAsync("GET", "/v2/app/tags/list");

        const string Body = "{\"name\":\"app\",\"tags\":[\"latest\",\"v1\",\"v2\"]}";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: application/json\r\nContent-Length: {Body.Length}\r\nConnection: close\r\n\r\n{Body}",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_CountAndLast_ReturnsThatPageWithALinkToTheNext()
    {
        var harness = Repository("a", "b", "c", "d");

        var response = await harness.SendAsync("GET", "/v2/app/tags/list?n=2&last=a");

        const string Body = "{\"name\":\"app\",\"tags\":[\"b\",\"c\"]}";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: application/json\r\nLink: </v2/app/tags/list?n=2&last=c>; rel=\"next\"\r\n"
                + $"Content-Length: {Body.Length}\r\nConnection: close\r\n\r\n{Body}",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_LastPage_HasNoLink()
    {
        var harness = Repository("a", "b", "c");

        var response = await harness.SendAsync("GET", "/v2/app/tags/list?n=2&last=a");

        Assert.DoesNotContain("Link:", response);
        StringAssert.EndsWith(response, "{\"name\":\"app\",\"tags\":[\"b\",\"c\"]}");
    }

    [TestMethod]
    public async Task AnswerAsync_CountZero_IsAnEmptyListWithNoLink()
    {
        var harness = Repository("a");

        var response = await harness.SendAsync("GET", "/v2/app/tags/list?n=0");

        Assert.DoesNotContain("Link:", response);
        StringAssert.EndsWith(response, "{\"name\":\"app\",\"tags\":[]}");
    }

    [TestMethod]
    [DataRow("n=x")]
    [DataRow("n=-1")]
    [DataRow("n=10001")]
    public async Task AnswerAsync_CountNotFrom0To10000_Returns400Unsupported(string query)
    {
        var harness = Repository("a");

        var response = await harness.SendAsync("GET", $"/v2/app/tags/list?{query}");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "UNSUPPORTED", "the operation is unsupported"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_RepositoryWithoutIndex_ListsNoTags()
    {
        var harness = new H();
        harness.CreateRepository("app");

        var response = await harness.SendAsync("GET", "/v2/app/tags/list");

        StringAssert.EndsWith(response, "{\"name\":\"app\",\"tags\":[]}");
    }

    private static H Repository(params string[] tags)
    {
        var harness = new H();
        harness.CreateRepository("app");
        harness.WriteBlob("app", Manifest);
        harness.WriteIndex("app", [.. tags.Select(tag => H.Descriptor(H.ManifestMediaType, Manifest, tag)), H.Descriptor(H.ManifestMediaType, Manifest)]);

        return harness;
    }
}
