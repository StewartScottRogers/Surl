using System.Text;
using Surl.Protocol.Abstractions;
using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The repository's digest check cached in the object metadata index against each file's stamp
/// (ADR-0115, decision 2.1; BL-865): a file is hashed once until it changes.
/// </summary>
[TestClass]
public sealed class OciRepositoryTests
{
    private const string Layer = "layer bytes";

    private const string Manifest = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a","size":2},"layers":[]}""";

    [TestMethod]
    public async Task ServeAsync_BlobPulledTwice_IsHashedOnceAndAgainOnceReplacedThroughTheStore()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", Layer);
        var file = BlobFile(digest);

        var first = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");
        var second = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.IsTrue(first.EndsWith($"\r\n\r\n{Layer}", StringComparison.Ordinal));
        Assert.AreEqual(first, second);
        Assert.AreEqual(3, harness.ReadsOf(file), "One read to hash the file, then one to serve each pull.");

        harness.TimeProvider.UtcNow = H.Now.AddSeconds(1);
        await harness.ReplaceThroughStoreAsync(file, Encoding.UTF8.GetBytes(Layer));
        var third = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.IsTrue(third.EndsWith($"\r\n\r\n{Layer}", StringComparison.Ordinal));
        Assert.AreEqual(5, harness.ReadsOf(file), "The replaced file is hashed again before it is served.");
    }

    [TestMethod]
    public async Task ServeAsync_BlobTamperedAfterItWasVerified_Returns404BlobUnknownAndNotes()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", Layer);
        await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        await harness.ReplaceThroughStoreAsync(BlobFile(digest), "tampered"u8.ToArray());
        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UNKNOWN", "blob unknown to registry"), response);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("does not hash to its digest", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ManifestPulledTwiceThenTampered_IsServedTwiceThenReturns404ManifestUnknownAndNotes()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", Manifest);
        harness.WriteIndex("app", H.Descriptor(H.ManifestMediaType, Manifest, "latest"));

        var first = await harness.SendAsync("GET", "/v2/app/manifests/latest");
        var second = await harness.SendAsync("GET", "/v2/app/manifests/latest");
        await harness.ReplaceThroughStoreAsync(BlobFile(digest), "tampered"u8.ToArray());
        var third = await harness.SendAsync("GET", "/v2/app/manifests/latest");

        Assert.IsTrue(first.EndsWith($"\r\n\r\n{Manifest}", StringComparison.Ordinal));
        Assert.AreEqual(first, second);
        Assert.AreEqual(H.ExpectedError("404 Not Found", "MANIFEST_UNKNOWN", "manifest unknown"), third);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("does not hash to its digest", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_NoMetadataIndex_HashesTheBlobOnEveryPull()
    {
        var harness = H.WithoutMetadataIndex();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", Layer);

        await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");
        var second = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.IsTrue(second.EndsWith($"\r\n\r\n{Layer}", StringComparison.Ordinal));
        Assert.AreEqual(4, harness.ReadsOf(BlobFile(digest)));
    }

    [TestMethod]
    public async Task ServeAsync_BlobInAShare_IsNotCachedSoATamperedFileOfTheSameStampIsRefused()
    {
        var harness = new H(new Surl.Content.ContentExposureOptions(), H.Share("team", share => share));
        harness.CreateRepository("shares/team/app");
        var digest = harness.WriteBlob("shares/team/app", Layer);
        var served = await harness.SendAsync("GET", $"/v2/team/app/blobs/{digest}");

        harness.WriteFile($"shares/team/app/blobs/sha256/{digest[7..]}", "tampered!!!");
        var refused = await harness.SendAsync("GET", $"/v2/team/app/blobs/{digest}");

        Assert.IsTrue(served.EndsWith($"\r\n\r\n{Layer}", StringComparison.Ordinal));
        Assert.AreEqual(H.ExpectedError("404 Not Found", "BLOB_UNKNOWN", "blob unknown to registry"), refused);
    }

    [TestMethod]
    public void Constructor_WithANullMetadataIndex_Throws()
    {
        var harness = new H();

        Assert.ThrowsExactly<ArgumentNullException>(() => new OciRegistryProtocolServer(harness.Store, null!, harness.Policy, harness.TimeProvider));
    }

    [TestMethod]
    public async Task Constructor_WithAMetadataIndex_CachesVerifiedDigestsInIt()
    {
        var harness = new H();
        harness.CreateRepository("app");
        var digest = harness.WriteBlob("app", Layer);
        var server = new OciRegistryProtocolServer(harness.Store, harness.MetadataIndex!, harness.Policy, harness.TimeProvider);
        var tokenConnection = new InMemoryConnection([Encoding.Latin1.GetBytes($"GET /token?scope=repository:app:pull HTTP/1.1\r\nHost: registry.test\r\n{UnitTestAuthenticationPolicy.BasicAuthorization}\r\nConnection: close\r\n\r\n")]);
        await server.ServeAsync(tokenConnection, harness.Context());
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes($"GET /v2/app/blobs/{digest} HTTP/1.1\r\nHost: registry.test\r\nAuthorization: Bearer {CountingIdentifierRandom.TokenOf(1)}\r\nConnection: close\r\n\r\n")]);

        await server.ServeAsync(connection, harness.Context());

        Assert.IsTrue(await harness.MetadataIndex!.IsDigestVerifiedAsync(BlobFile(digest), digest, CancellationToken.None));
    }

    private static string BlobFile(string digest) => $"app/blobs/sha256/{digest[7..]}";
}
