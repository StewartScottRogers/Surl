namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build, run with <c>--unix-socket</c>, fetches a file over HTTP from a
/// live <c>surl --unix-socket http://localhost/</c>, with the socket named by a relative path
/// (resolved against the working directory both processes share) and by an absolute path
/// (BL-1565, from a Surly cookbook report of exit 55 on macOS). Inconclusive where no pinned
/// build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverHttpOnAUnixSocketTests
{
    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly Dictionary<string, byte[]> ServedFiles = new() { ["hello.txt"] = HelloWorld };

    private string? relativeSocketPath;

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void DeleteSocketFile()
    {
        if (relativeSocketPath is not null && File.Exists(relativeSocketPath))
        {
            File.Delete(relativeSocketPath);
        }
    }

    [TestMethod]
    public async Task Fetch_RelativeSocketPath_WritesTheFile()
    {
        relativeSocketPath = "surl-" + Guid.NewGuid().ToString("N")[..8] + ".sock";

        await AssertFetchWritesTheFileAsync(relativeSocketPath);
    }

    [TestMethod]
    public async Task Fetch_AbsoluteSocketPath_WritesTheFile()
    {
        // Short enough for macOS's 103-byte sun_path under its temporary directory.
        var absolutePath = Path.Join(Path.GetTempPath(), "surl-" + Guid.NewGuid().ToString("N")[..8] + ".sock");

        await AssertFetchWritesTheFileAsync(absolutePath);
    }

    private async Task AssertFetchWritesTheFileAsync(string socketPath)
    {
        await using var surl = await SurlOnLoopback.StartOverUnixSocketAsync(
            "http", socketPath, ServedFiles, [], [], TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, ["-sS", "--unix-socket", socketPath, "http://localhost/hello.txt"]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }
}
