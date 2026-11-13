namespace Surl.Conformance;

/// <summary>
/// One <c>surl</c> serves one content store over several protocols (BL-669, BL-622 decision 5). The pinned
/// upstream curl uploads over <c>ftp</c> and the pinned Curl port's <c>s3</c>, <c>az</c> and <c>gs</c> clients
/// fetch the same bytes back; the port's <c>s3</c> client uploads and the pinned upstream curl fetches them over
/// <c>http</c>. Every expected value is the bytes the sending side was given, never what the port printed
/// (ADR-0083 decisions 1 to 3, ADR-0003). Inconclusive when a pinned build is absent, or, for <c>az</c>, when
/// this machine will not let surl listen on port 443, the only port the port's Azure client connects to.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientReadsFilesSharedAcrossSurlProtocolsTests
{
    private const string Bucket = "box";
    private const string AzureTlsPort = "443";

    private static readonly byte[] Content = "shared across surl protocols\n"u8.ToArray();

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
    public async Task FtpUpload_ThenS3GetObject_ReturnsTheUploadedBytes()
    {
        await using var surl = await StartAsync(["ftp", "s3"], "--s3-plaintext");
        await UploadOverFtpAsync(surl);

        var fetched = await RunPortAsync(
            new Dictionary<string, string> { ["AWS_ENDPOINT_URL_S3"] = EndpointOf(surl, "s3", "http") },
            "-s", $"s3://{Bucket}/up.txt");

        Assert.AreEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        CollectionAssert.AreEqual(Content, fetched.StandardOutput, Diagnostics(fetched, surl));
    }

    [TestMethod]
    public async Task FtpUpload_ThenAzGetBlob_ReturnsTheUploadedBytes()
    {
        // The port's Azure client always connects to port 443 over TLS, so surl must listen there.
        await using var surl = await StartOnAzurePortOrInconclusiveAsync();
        await UploadOverFtpAsync(surl);

        var fetched = await RunPortAsync(new Dictionary<string, string>(), "-s", "-k", $"az://127.0.0.1/{Bucket}/up.txt");

        Assert.AreEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        CollectionAssert.AreEqual(Content, fetched.StandardOutput, Diagnostics(fetched, surl));
    }

    [TestMethod]
    public async Task FtpUpload_ThenGsGetObject_ReturnsTheUploadedBytes()
    {
        await using var surl = await StartAsync(["ftp", "gs"], "--self-signed");
        await UploadOverFtpAsync(surl);

        var fetched = await RunPortAsync(
            new Dictionary<string, string> { ["STORAGE_EMULATOR_HOST"] = EndpointOf(surl, "gs", "https") },
            "-s", "-k", $"gs://{Bucket}/up.txt");

        Assert.AreEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        CollectionAssert.AreEqual(Content, fetched.StandardOutput, Diagnostics(fetched, surl));
    }

    [TestMethod]
    public async Task S3PutObject_ThenHttpGet_ReturnsTheUploadedBytes()
    {
        await using var surl = await StartAsync(["s3", "http"], "--s3-plaintext", "--user", "tester:secret");
        var file = await localFiles.WriteFileAsync("up.txt", Content, TestContext.CancellationToken);

        var uploaded = await RunPortAsync(
            new Dictionary<string, string>
            {
                ["AWS_ENDPOINT_URL_S3"] = EndpointOf(surl, "s3", "http"),
                ["AWS_ACCESS_KEY_ID"] = "tester",
                ["AWS_SECRET_ACCESS_KEY"] = "secret",
            },
            "-s", "-T", file, $"s3://{Bucket}/up.txt");
        var fetched = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", new Uri(surl.BaseUrlOf("http"), $"{Bucket}/up.txt").AbsoluteUri);

        Assert.AreEqual(0, uploaded.ExitCode, Diagnostics(uploaded, surl));
        Assert.AreEqual(0, fetched.ExitCode, fetched.StandardError);
        CollectionAssert.AreEqual(Content, fetched.StandardOutput);
    }

    private static string EndpointOf(SurlOnLoopback surl, string scheme, string endpointScheme) =>
        $"{endpointScheme}://127.0.0.1:{surl.BaseUrlOf(scheme).Port}";

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";

    private string NewDataDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-shared-store-");
        directory.CreateSubdirectory(Bucket);
        directories.Add(directory.FullName);
        return directory.FullName;
    }

    private Task<SurlOnLoopback> StartAsync(string[] schemes, params string[] options) =>
        SurlOnLoopback.StartOverDirectoryAsync(
            schemes, NewDataDirectory(), ["--allow-anonymous", "--allow-uploads", .. options], TestContext.CancellationToken);

    private async Task<SurlOnLoopback> StartOnAzurePortOrInconclusiveAsync()
    {
        try
        {
            return await SurlOnLoopback.StartOverDirectoryOnUrlsAsync(
                ["ftp://127.0.0.1:0/", $"az://127.0.0.1:{AzureTlsPort}/"],
                NewDataDirectory(),
                ["--allow-anonymous", "--allow-uploads", "--self-signed"],
                TestContext.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Assert.Inconclusive($"surl could not listen on port {AzureTlsPort}: {exception.Message}");
            throw;
        }
    }

    private async Task UploadOverFtpAsync(SurlOnLoopback surl)
    {
        var file = await localFiles.WriteFileAsync("up.txt", Content, TestContext.CancellationToken);
        var uploaded = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-T", file, new Uri(surl.BaseUrlOf("ftp"), $"{Bucket}/up.txt").AbsoluteUri);

        Assert.AreEqual(0, uploaded.ExitCode, uploaded.StandardError);
    }

    private async Task<CurlPortClientRunResult> RunPortAsync(IReadOnlyDictionary<string, string> extra, params string[] arguments)
    {
        var runner = PinnedCurlPortClientOnThisMachine.RunnerFor(await PinnedCurlPortClientOnThisMachine.LocateAsync(TestContext));
        var environment = localFiles.Environment.Concat(extra).ToDictionary(pair => pair.Key, pair => pair.Value);
        var result = await runner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, TestContext.CancellationToken);

        TestContext.WriteLine($"curl port client {string.Join(' ', arguments)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}");
        return result;
    }
}
