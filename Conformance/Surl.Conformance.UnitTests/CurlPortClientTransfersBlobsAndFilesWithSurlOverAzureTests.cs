namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>az</c> and <c>abfss</c> clients against a live surl (BL-428, ADR-0083 decisions 1 to 3):
/// a Blob read, write and listing, a Data Lake file read, create-append-flush write and listing, and a request
/// with no credential refused. Each expected value comes from the Azure Storage REST API - Get Blob and Path - Read
/// return the stored bytes, Put Blob and Path - Create, Update (append, flush) store the sent bytes, List Blobs and
/// Path - List name every entry, and a request with no credential is <c>401</c> - never from what the port printed.
/// The port's Azure client always connects to port 443 over TLS, so each test is inconclusive when this machine
/// will not let surl listen there, or when the pinned client is absent. They never run in parallel: port 443 is one
/// listener at a time.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CurlPortClientTransfersBlobsAndFilesWithSurlOverAzureTests
{
    private const string Container = "box";
    private const string Token = "entra-token";
    private const string TlsPort = "443";

    private static readonly byte[] Content = "a blob for the azure client\n"u8.ToArray();

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
    public async Task AzGet_OfAStoredBlob_ReturnsTheStoredBytes()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Container, "hello.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartOnAzurePortOrInconclusiveAsync("az", data);

        var fetched = await RunPortAsync(surl, withToken: true, "-s", "-k", $"az://127.0.0.1/{Container}/hello.txt");

        Assert.AreEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        CollectionAssert.AreEqual(Content, fetched.StandardOutput, Diagnostics(fetched, surl));
    }

    [TestMethod]
    public async Task AzPut_UnderAllowUploads_StoresTheSentBytes()
    {
        var data = NewDataDirectory();
        await using var surl = await StartOnAzurePortOrInconclusiveAsync("az", data);
        var file = await localFiles.WriteFileAsync("up.txt", Content, TestContext.CancellationToken);

        var uploaded = await RunPortAsync(surl, withToken: true, "-s", "-k", "-T", file, $"az://127.0.0.1/{Container}/up.txt");

        Assert.AreEqual(0, uploaded.ExitCode, Diagnostics(uploaded, surl));
        CollectionAssert.AreEqual(
            Content, await File.ReadAllBytesAsync(Path.Combine(data, Container, "up.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AzList_OfAContainer_NamesEveryBlob()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Container, "alpha.txt"), Content, TestContext.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(data, Container, "beta.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartOnAzurePortOrInconclusiveAsync("az", data);

        var listed = await RunPortAsync(surl, withToken: true, "-s", "-k", $"az://127.0.0.1/{Container}/");

        var text = System.Text.Encoding.UTF8.GetString(listed.StandardOutput);
        Assert.AreEqual(0, listed.ExitCode, Diagnostics(listed, surl));
        StringAssert.Contains(text, "alpha.txt", Diagnostics(listed, surl));
        StringAssert.Contains(text, "beta.txt", Diagnostics(listed, surl));
    }

    [TestMethod]
    public async Task AbfssGet_OfAStoredFile_ReturnsTheStoredBytes()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Container, "hello.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartOnAzurePortOrInconclusiveAsync("abfss", data);

        var fetched = await RunPortAsync(surl, withToken: true, "-s", "-k", $"abfss://{Container}@127.0.0.1/hello.txt");

        Assert.AreEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        CollectionAssert.AreEqual(Content, fetched.StandardOutput, Diagnostics(fetched, surl));
    }

    [TestMethod]
    public async Task AbfssPut_CreateAppendFlush_StoresTheSentBytes()
    {
        var data = NewDataDirectory();
        await using var surl = await StartOnAzurePortOrInconclusiveAsync("abfss", data);
        var file = await localFiles.WriteFileAsync("up.txt", Content, TestContext.CancellationToken);

        var uploaded = await RunPortAsync(surl, withToken: true, "-s", "-k", "-T", file, $"abfss://{Container}@127.0.0.1/up.txt");

        Assert.AreEqual(0, uploaded.ExitCode, Diagnostics(uploaded, surl));
        CollectionAssert.AreEqual(
            Content, await File.ReadAllBytesAsync(Path.Combine(data, Container, "up.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AbfssList_OfAFilesystem_NamesEveryPath()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Container, "alpha.txt"), Content, TestContext.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(data, Container, "beta.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartOnAzurePortOrInconclusiveAsync("abfss", data);

        var listed = await RunPortAsync(surl, withToken: true, "-s", "-k", $"abfss://{Container}@127.0.0.1/");

        var text = System.Text.Encoding.UTF8.GetString(listed.StandardOutput);
        Assert.AreEqual(0, listed.ExitCode, Diagnostics(listed, surl));
        StringAssert.Contains(text, "alpha.txt", Diagnostics(listed, surl));
        StringAssert.Contains(text, "beta.txt", Diagnostics(listed, surl));
    }

    [TestMethod]
    public async Task AzGet_WithNoCredentialAndAnonymousOff_IsRefusedAndDeliversNoBlobBytes()
    {
        var data = NewDataDirectory();
        await File.WriteAllBytesAsync(Path.Combine(data, Container, "hello.txt"), Content, TestContext.CancellationToken);
        await using var surl = await StartOnAzurePortOrInconclusiveAsync("az", data);

        var fetched = await RunPortAsync(surl, withToken: false, "-s", "-k", $"az://127.0.0.1/{Container}/hello.txt");

        Assert.AreNotEqual(0, fetched.ExitCode, Diagnostics(fetched, surl));
        Assert.IsFalse(
            System.Text.Encoding.UTF8.GetString(fetched.StandardOutput).Contains("a blob for the azure client", StringComparison.Ordinal),
            Diagnostics(fetched, surl));
    }

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";

    private string NewDataDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-azure-store-");
        directory.CreateSubdirectory(Container);
        directories.Add(directory.FullName);
        return directory.FullName;
    }

    private async Task<SurlOnLoopback> StartOnAzurePortOrInconclusiveAsync(string scheme, string dataDirectory)
    {
        try
        {
            return await SurlOnLoopback.StartOverDirectoryOnUrlsAsync(
                [$"{scheme}://127.0.0.1:{TlsPort}/"],
                dataDirectory,
                ["--allow-uploads", "--list-directories", "--self-signed", "--user", $":{Token}"],
                TestContext.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Assert.Inconclusive($"surl could not listen on port {TlsPort}: {exception.Message}");
            throw;
        }
    }

    private async Task<CurlPortClientRunResult> RunPortAsync(SurlOnLoopback surl, bool withToken, params string[] arguments)
    {
        var runner = PinnedCurlPortClientOnThisMachine.RunnerFor(await PinnedCurlPortClientOnThisMachine.LocateAsync(TestContext));
        var environment = localFiles.Environment.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (withToken)
        {
            environment["AZURE_STORAGE_BEARER_TOKEN"] = Token;
        }

        var result = await runner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, TestContext.CancellationToken);

        TestContext.WriteLine($"curl port client {string.Join(' ', arguments)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}");
        return result;
    }
}
