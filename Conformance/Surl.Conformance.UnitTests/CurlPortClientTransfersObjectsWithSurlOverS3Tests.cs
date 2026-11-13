namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>s3</c> client against a live <c>surl s3://</c> (BL-422, ADR-0083 decisions 1 to 3):
/// a signed GetObject, a signed PutObject under <c>--allow-uploads</c>, a ListObjectsV2 listing (which needs <c>--list-directories</c>), and an unsigned
/// request refused. Each expected value comes from the Amazon S3 API Reference - GetObject returns the stored
/// bytes, PutObject stores the sent bytes, ListObjectsV2 names every key, and a request with no signature is
/// <c>403 AccessDenied</c> - never from what the port printed. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientTransfersObjectsWithSurlOverS3Tests
{
    private const string Bucket = "box";
    private const string AccessKey = "tester";
    private const string SecretKey = "secret";

    private static readonly byte[] Content = "an object for the s3 client\n"u8.ToArray();

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
    public async Task SignedGet_OfAStoredObject_ReturnsTheStoredBytes()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Bucket, "hello.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartAsync(data);

        var fetched = await RunPortAsync(surl, signed: true, "-s", $"s3://{Bucket}/hello.txt");

        Assert.AreEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        CollectionAssert.AreEqual(Content, fetched.StandardOutput, Diagnostics(fetched, surl));
    }

    [TestMethod]
    public async Task SignedPut_UnderAllowUploads_StoresTheSentBytes()
    {
        var data = NewDataDirectory();
        await using var surl = await StartAsync(data);
        var file = await localFiles.WriteFileAsync("up.txt", Content, TestContext.CancellationToken);

        var uploaded = await RunPortAsync(surl, signed: true, "-s", "-T", file, $"s3://{Bucket}/up.txt");

        Assert.AreEqual(0, uploaded.ExitCode, Diagnostics(uploaded, surl));
        CollectionAssert.AreEqual(
            Content, await File.ReadAllBytesAsync(Path.Combine(data, Bucket, "up.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SignedList_OfABucket_NamesEveryKey()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Bucket, "alpha.txt"), Content, TestContext.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(data, Bucket, "beta.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartAsync(data);

        var listed = await RunPortAsync(surl, signed: true, "-s", $"s3://{Bucket}/");

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
        await using var surl = await StartAsync(data);

        var fetched = await RunPortAsync(surl, signed: false, "-s", $"s3://{Bucket}/hello.txt");

        Assert.AreNotEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        Assert.IsFalse(
            System.Text.Encoding.UTF8.GetString(fetched.StandardOutput).Contains("an object for the s3 client", StringComparison.Ordinal),
            Diagnostics(fetched, surl));
    }

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";

    private string NewDataDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-s3-store-");
        directory.CreateSubdirectory(Bucket);
        directories.Add(directory.FullName);
        return directory.FullName;
    }

    private Task<SurlOnLoopback> StartAsync(string dataDirectory) =>
        SurlOnLoopback.StartOverDirectoryAsync(
            ["s3"], dataDirectory, ["--allow-uploads", "--list-directories", "--s3-plaintext", "--user", $"{AccessKey}:{SecretKey}"], TestContext.CancellationToken);

    private async Task<CurlPortClientRunResult> RunPortAsync(SurlOnLoopback surl, bool signed, params string[] arguments)
    {
        var runner = PinnedCurlPortClientOnThisMachine.RunnerFor(await PinnedCurlPortClientOnThisMachine.LocateAsync(TestContext));
        var environment = localFiles.Environment.ToDictionary(pair => pair.Key, pair => pair.Value);
        environment["AWS_ENDPOINT_URL"] = $"http://127.0.0.1:{surl.BaseUrlOf("s3").Port}";
        if (signed)
        {
            environment["AWS_ACCESS_KEY_ID"] = AccessKey;
            environment["AWS_SECRET_ACCESS_KEY"] = SecretKey;
        }

        var result = await runner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, TestContext.CancellationToken);

        TestContext.WriteLine($"curl port client {string.Join(' ', arguments)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}");
        return result;
    }
}
