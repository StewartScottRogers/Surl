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

    private Task<SurlOnLoopback> StartSurlAsync() =>
        SurlOnLoopback.StartAsync(
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello, ["empty.txt"] = [] },
            TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
