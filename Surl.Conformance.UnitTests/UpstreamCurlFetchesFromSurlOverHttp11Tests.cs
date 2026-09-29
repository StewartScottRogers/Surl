using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Phase 1's proof: the pinned upstream curl build fetches from a live, in-process
/// <c>surl</c> over HTTP/1.1, and its exit code and output are what a correct server produces.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverHttp11Tests
{
    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Get_File_ExitsZeroWithTheFilesBytes()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-s", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task Get_EmptyFile_ExitsZeroWithEmptyOutput()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-s", surl.UrlOf("empty.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Head_File_ExitsZeroWithAnHttp11OkStatusLine()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-sI", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var headers = Encoding.ASCII.GetString(result.StandardOutput);
        var statusLine = headers[..headers.IndexOf("\r\n", StringComparison.Ordinal)];
        StringAssert.StartsWith(statusLine, "HTTP/1.1 200");
    }

    [TestMethod]
    public async Task GetOverHttp10_File_ExitsZeroWithTheFilesBytes()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-s", "-0", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task GetWithFail_MissingFile_ExitsHttpReturnedError()
    {
        const int HttpReturnedError = 22;
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-s", "--fail", surl.UrlOf("missing"));

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Get_SameFileTwice_ExitsZeroWithTheFilesBytesTwice()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-s", surl.UrlOf("hello.txt"), surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat(Hello).ToArray(), result.StandardOutput);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Surl.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Surl.slnx was not found above the test output directory.");
    }

    private Task<SurlOnLoopback> StartSurlAsync() =>
        SurlOnLoopback.StartAsync(
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello, ["empty.txt"] = [] },
            TestContext.CancellationToken);

    private async Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments)
    {
        var pins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), UpstreamCurlBuildPins.FileName), TestContext.CancellationToken));
        var location = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess())
            .Locate(pins, UpstreamCurlLocator.CurrentPlatform);
        if (!location.IsAvailable)
        {
            Assert.Inconclusive(location.Message);
        }

        var runner = new UpstreamCurlRunner(location, TimeSpan.FromSeconds(30), TimeProvider.System);
        var result = await runner.RunAsync(arguments, TestContext.CancellationToken);

        TestContext.WriteLine($"curl {string.Join(' ', arguments)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}");
        return result;
    }
}
