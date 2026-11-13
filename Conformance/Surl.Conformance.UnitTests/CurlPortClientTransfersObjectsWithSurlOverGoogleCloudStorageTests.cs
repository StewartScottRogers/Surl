namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>gs</c> client against a live <c>surl gs://</c> (BL-434, ADR-0083 decisions 1 to 3):
/// a Bearer GET Object, a <c>GOOG4-HMAC-SHA256</c> PUT Object under <c>--allow-uploads</c>, a List Objects listing
/// (which needs <c>--list-directories</c>), and an unsigned request refused. Each expected value comes from the
/// Cloud Storage XML API pages - GET Object returns the stored bytes, PUT Object stores the sent bytes, List Objects
/// names every object, and a request with no login is <c>401</c> - never from what the port printed. The client
/// reaches surl through <c>STORAGE_EMULATOR_HOST</c> over TLS (surl's <c>gs</c> is implicit TLS, here with
/// <c>--self-signed</c>, and the client is given <c>-k</c>). Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientTransfersObjectsWithSurlOverGoogleCloudStorageTests
{
    private const string Bucket = "box";
    private const string Token = "oauth-token";
    private const string AccessId = "tester";
    private const string Secret = "secret";

    private static readonly byte[] Content = "an object for the gs client\n"u8.ToArray();

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
    public async Task BearerGet_OfAStoredObject_ReturnsTheStoredBytes()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Bucket, "hello.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartAsync(data, $":{Token}");

        var fetched = await RunPortAsync(surl, "-s", "-k", "--oauth2-bearer", Token, $"gs://{Bucket}/hello.txt");

        Assert.AreEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        CollectionAssert.AreEqual(Content, fetched.StandardOutput, Diagnostics(fetched, surl));
    }

    [TestMethod]
    public async Task HmacPut_UnderAllowUploads_StoresTheSentBytes()
    {
        var data = NewDataDirectory();
        await using var surl = await StartAsync(data, $"{AccessId}:{Secret}");
        var file = await localFiles.WriteFileAsync("up.txt", Content, TestContext.CancellationToken);

        var uploaded = await RunPortAsync(surl, "-s", "-k", "--user", $"{AccessId}:{Secret}", "-T", file, $"gs://{Bucket}/up.txt");

        Assert.AreEqual(0, uploaded.ExitCode, Diagnostics(uploaded, surl));
        CollectionAssert.AreEqual(
            Content, await File.ReadAllBytesAsync(Path.Combine(data, Bucket, "up.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task BearerList_OfABucket_NamesEveryObject()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Bucket, "alpha.txt"), Content, TestContext.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(data, Bucket, "beta.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartAsync(data, $":{Token}");

        var listed = await RunPortAsync(surl, "-s", "-k", "--oauth2-bearer", Token, $"gs://{Bucket}/");

        var text = System.Text.Encoding.UTF8.GetString(listed.StandardOutput);
        Assert.AreEqual(0, listed.ExitCode, Diagnostics(listed, surl));
        StringAssert.Contains(text, "alpha.txt", Diagnostics(listed, surl));
        StringAssert.Contains(text, "beta.txt", Diagnostics(listed, surl));
    }

    [TestMethod]
    public async Task UnsignedGet_WithAnonymousOff_IsRefusedAndDeliversNoObjectBytes()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Bucket, "hello.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartAsync(data, $":{Token}");

        var fetched = await RunPortAsync(surl, "-s", "-k", $"gs://{Bucket}/hello.txt");

        Assert.AreNotEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        Assert.IsFalse(
            System.Text.Encoding.UTF8.GetString(fetched.StandardOutput).Contains("an object for the gs client", StringComparison.Ordinal),
            Diagnostics(fetched, surl));
    }

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";

    private string NewDataDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-gs-store-");
        directory.CreateSubdirectory(Bucket);
        directories.Add(directory.FullName);
        return directory.FullName;
    }

    private Task<SurlOnLoopback> StartAsync(string dataDirectory, string account) =>
        SurlOnLoopback.StartOverDirectoryAsync(
            ["gs"], dataDirectory, ["--allow-uploads", "--list-directories", "--self-signed", "--user", account], TestContext.CancellationToken);

    private async Task<CurlPortClientRunResult> RunPortAsync(SurlOnLoopback surl, params string[] arguments)
    {
        var runner = PinnedCurlPortClientOnThisMachine.RunnerFor(await PinnedCurlPortClientOnThisMachine.LocateAsync(TestContext));
        var environment = localFiles.Environment.ToDictionary(pair => pair.Key, pair => pair.Value);
        environment["STORAGE_EMULATOR_HOST"] = $"https://127.0.0.1:{surl.BaseUrlOf("gs").Port}";

        var result = await runner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, TestContext.CancellationToken);

        TestContext.WriteLine($"curl port client {string.Join(' ', arguments)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}");
        return result;
    }
}
