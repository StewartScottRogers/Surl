using Surl.Content;
using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Creating a repository on its first push, and the names and shares that refuse one
/// (ADR-0115, decisions 2.2 and 2.3).
/// </summary>
[TestClass]
public sealed class OciRepositoryCreationTests
{
    private const string Denied = "requested access to the resource is denied";

    [TestMethod]
    public async Task OpenOrCreateAsync_PushToAReadOnlyShare_Returns403Denied()
    {
        var harness = new H(new ContentExposureOptions { AllowUploads = true }, H.Share("ro", share => new ContentShare(share.Name, share.Path, share.FileSystem) { IsReadOnly = true }));

        var response = await harness.SendAsync("POST", "/v2/ro/app/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", Denied), response);
    }

    [TestMethod]
    public async Task OpenOrCreateAsync_PushToAShareTheCallerMayOnlyRead_Returns403Denied()
    {
        var harness = new H(
            new ContentExposureOptions { AllowUploads = true },
            H.Share("team", share => new ContentShare(share.Name, share.Path, share.FileSystem) { Grants = new Dictionary<string, ContentShareAccess> { [ContentShare.EveryoneMember] = ContentShareAccess.Read } }));

        var response = await harness.SendAsync("POST", "/v2/team/app/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", Denied), response);
    }

    [TestMethod]
    public async Task OpenOrCreateAsync_PushToAShareTheCallerMayNotSee_Returns404NameUnknown()
    {
        var harness = new H(
            new ContentExposureOptions { AllowUploads = true },
            H.Share("private", share => new ContentShare(share.Name, share.Path, share.FileSystem) { Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.ReadWrite } }));

        var response = await harness.SendAsync("POST", "/v2/private/app/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("404 Not Found", "NAME_UNKNOWN", "repository name not known to registry"), response);
    }

    [TestMethod]
    public async Task OpenOrCreateAsync_PushToAWritableShare_CreatesTheRepositoryInIt()
    {
        var harness = new H(new ContentExposureOptions { AllowUploads = true }, H.Share("team", share => share));

        var response = await harness.SendAsync("POST", "/v2/team/app/blobs/uploads/", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
        Assert.IsNotNull(harness.ReadFile("shares/team/app/oci-layout"));
    }

    [TestMethod]
    [DataRow("file/app", "file")]
    [DataRow("lib/blobs/app", "lib/oci-layout")]
    [DataRow("busy", "busy/notes.txt")]
    [DataRow("con/app", null)]
    public async Task OpenOrCreateAsync_NameInTheWay_Returns400NameInvalid(string name, string? existingFile)
    {
        var harness = H.AllowingUploads();
        if (existingFile is not null)
        {
            harness.WriteFile(existingFile, "x");
        }

        var response = await harness.SendAsync("POST", $"/v2/{name}/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "NAME_INVALID", "invalid repository name"), response);
    }

    [TestMethod]
    public async Task OpenOrCreateAsync_EmptyFolderInAReadOnlyShare_Returns403Denied()
    {
        var harness = new H(new ContentExposureOptions { AllowUploads = true }, H.Share("ro", share => new ContentShare(share.Name, share.Path, share.FileSystem) { IsReadOnly = true }));
        harness.FileSystem.CreateDirectory(Path.Join(InMemoryContentFileSystem.RootPath, "shares", "ro", "app"));

        var response = await harness.SendAsync("POST", "/v2/ro/app/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", Denied), response);
    }

    [TestMethod]
    public async Task OpenOrCreateAsync_EmptyFolderAndARepositoryBeside_AreUsed()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("library");
        harness.FileSystem.CreateDirectory(Path.Join(InMemoryContentFileSystem.RootPath, "library", "alpine"));

        var response = await harness.SendAsync("POST", "/v2/library/alpine/blobs/uploads/", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
        Assert.IsNotNull(harness.ReadFile("library/alpine/oci-layout"));
    }
}
