using System.Security.Cryptography;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>oci</c> client against a live <c>surl oci://</c> (BL-440, ADR-0083 decisions 1 to 3):
/// a blob push and a manifest push under <c>--allow-uploads</c> with a token login, a pull of a manifest by tag and
/// by digest and of a blob by digest, and an unauthenticated push refused. Each expected value comes from the OCI
/// Distribution Specification - a push stores the exact bytes under their digest, a pull returns the stored bytes,
/// and a request with no login is <c>401</c> - never from what the port printed. The client reaches surl over TLS
/// (<c>oci</c> is implicit TLS, here with <c>--self-signed</c>, and the client is given <c>-k</c>). Inconclusive
/// when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientTransfersContentWithSurlOverOciTests
{
    private const string Repository = "lib/app";
    private const string User = "alice";
    private const string Password = "secret";
    private const string ManifestType = "application/vnd.oci.image.manifest.v1+json";

    private static readonly byte[] Layer = "layer bytes for the oci client\n"u8.ToArray();

    private static readonly byte[] Manifest =
        """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}"""u8.ToArray();

    private static readonly byte[] PulledManifest =
        """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json","layers":[]}"""u8.ToArray();

    private readonly List<string> directories = [];
    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteDirectories()
    {
        localFiles.Dispose();
        foreach (var directory in directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task BlobPush_WithATokenLogin_StoresTheBlobUnderItsDigest()
    {
        var data = NewDataDirectory();
        await using var surl = await StartAsync(data);
        var digest = DigestOf(Layer);
        var file = await localFiles.WriteFileAsync("layer.bin", Layer, TestContext.CancellationToken);

        var pushed = await RunPortAsync(surl, "-u", $"{User}:{Password}", "-T", file, $"oci://{Authority(surl)}/{Repository}@{digest}");

        Assert.AreEqual(0, pushed.ExitCode, Diagnostics(pushed, surl));
        CollectionAssert.AreEqual(Layer, await File.ReadAllBytesAsync(BlobPath(data, digest), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ManifestPush_WithATokenLogin_StoresTheManifestAndListsItsTag()
    {
        var data = NewDataDirectory();
        await using var surl = await StartAsync(data);
        var digest = DigestOf(Manifest);
        var file = await localFiles.WriteFileAsync("manifest.json", Manifest, TestContext.CancellationToken);

        var pushed = await RunPortAsync(surl, "-u", $"{User}:{Password}", "-T", file, $"oci://{Authority(surl)}/{Repository}:2.0");

        Assert.AreEqual(0, pushed.ExitCode, Diagnostics(pushed, surl));
        CollectionAssert.AreEqual(Manifest, await File.ReadAllBytesAsync(BlobPath(data, digest), TestContext.CancellationToken));
        var index = await File.ReadAllTextAsync(Path.Combine(data, Repository, "index.json"), TestContext.CancellationToken);
        StringAssert.Contains(index, digest, Diagnostics(pushed, surl));
        StringAssert.Contains(index, "\"2.0\"", Diagnostics(pushed, surl));
    }

    [TestMethod]
    public async Task ManifestPull_ByTag_ReturnsTheStoredManifest()
    {
        var data = NewDataDirectory();
        await SeedManifestAsync(data, tag: "1.0");
        await using var surl = await StartAsync(data);

        var pulled = await RunPortAsync(surl, "-u", $"{User}:{Password}", $"oci://{Authority(surl)}/{Repository}:1.0");

        Assert.AreEqual(0, pulled.ExitCode, Diagnostics(pulled, surl));
        CollectionAssert.AreEqual(PulledManifest, pulled.StandardOutput, Diagnostics(pulled, surl));
    }

    [TestMethod]
    public async Task ManifestPull_ByDigest_ReturnsTheStoredManifest()
    {
        var data = NewDataDirectory();
        await SeedManifestAsync(data, tag: "1.0");
        await using var surl = await StartAsync(data);

        var pulled = await RunPortAsync(
            surl, "-u", $"{User}:{Password}", $"oci://{Authority(surl)}/{Repository}@{DigestOf(PulledManifest)}");

        Assert.AreEqual(0, pulled.ExitCode, Diagnostics(pulled, surl));
        CollectionAssert.AreEqual(PulledManifest, pulled.StandardOutput, Diagnostics(pulled, surl));
    }

    [TestMethod]
    public async Task BlobPull_ByDigest_ReturnsTheStoredBlob()
    {
        var data = NewDataDirectory();
        await SeedManifestAsync(data, tag: "1.0");
        await using var surl = await StartAsync(data);

        var pulled = await RunPortAsync(surl, "-u", $"{User}:{Password}", $"oci://{Authority(surl)}/{Repository}@{DigestOf(Layer)}");

        Assert.AreEqual(0, pulled.ExitCode, Diagnostics(pulled, surl));
        CollectionAssert.AreEqual(Layer, pulled.StandardOutput, Diagnostics(pulled, surl));
    }

    [TestMethod]
    public async Task BlobPush_WithoutCredentials_IsRefusedAndStoresNothing()
    {
        var data = NewDataDirectory();
        await using var surl = await StartAsync(data);
        var digest = DigestOf(Layer);
        var file = await localFiles.WriteFileAsync("layer.bin", Layer, TestContext.CancellationToken);

        var pushed = await RunPortAsync(surl, "-T", file, $"oci://{Authority(surl)}/{Repository}@{digest}");

        Assert.AreNotEqual(0, pushed.ExitCode, Diagnostics(pushed, surl));
        Assert.IsFalse(File.Exists(BlobPath(data, digest)), Diagnostics(pushed, surl));
    }

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";

    private static string DigestOf(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string BlobPath(string data, string digest) =>
        Path.Combine(data, Repository, "blobs", "sha256", digest["sha256:".Length..]);

    private static string Authority(SurlOnLoopback surl) => $"127.0.0.1:{surl.BaseUrlOf("oci").Port}";

    private string NewDataDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-oci-store-");
        directories.Add(directory.FullName);
        return directory.FullName;
    }

    /// <summary>Writes an OCI Image Layout holding <see cref="Layer"/> and a manifest tagged <paramref name="tag"/>.</summary>
    private async Task SeedManifestAsync(string data, string tag)
    {
        var root = Path.Combine(data, Repository);
        var blobs = Directory.CreateDirectory(Path.Combine(root, "blobs", "sha256")).FullName;
        await File.WriteAllTextAsync(Path.Combine(root, "oci-layout"), """{"imageLayoutVersion":"1.0.0"}""", TestContext.CancellationToken);
        await File.WriteAllBytesAsync(BlobPath(data, DigestOf(Layer)), Layer, TestContext.CancellationToken);
        await File.WriteAllBytesAsync(BlobPath(data, DigestOf(PulledManifest)), PulledManifest, TestContext.CancellationToken);
        Assert.IsTrue(Directory.Exists(blobs));

        var index = $$$"""
            {"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"{{{ManifestType}}}","digest":"{{{DigestOf(PulledManifest)}}}","size":{{{PulledManifest.Length}}},"annotations":{"org.opencontainers.image.ref.name":"{{{tag}}}"}}]}
            """;
        await File.WriteAllTextAsync(Path.Combine(root, "index.json"), index, new UTF8Encoding(false), TestContext.CancellationToken);
    }

    private Task<SurlOnLoopback> StartAsync(string dataDirectory) =>
        SurlOnLoopback.StartOverDirectoryAsync(
            "oci", dataDirectory, ["--allow-uploads", "--self-signed", "--user", $"{User}:{Password}"], TestContext.CancellationToken);

    private async Task<CurlPortClientRunResult> RunPortAsync(SurlOnLoopback surl, params string[] arguments)
    {
        var runner = PinnedCurlPortClientOnThisMachine.RunnerFor(await PinnedCurlPortClientOnThisMachine.LocateAsync(TestContext));
        string[] withTls = ["-s", "-k", .. arguments];

        var result = await runner.RunAsync(withTls, ReadOnlyMemory<byte>.Empty, localFiles.Environment, TestContext.CancellationToken);

        TestContext.WriteLine($"curl port client {string.Join(' ', withTls)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}; surl: {surl.Log}");
        return result;
    }
}
