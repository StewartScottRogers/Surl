using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build, run with <c>--unix-socket</c>, lists, downloads and uploads
/// through a live, in-process <c>surl --unix-socket ftp://localhost/</c> (ADR-0094 decision 2,
/// BL-605). Measured with <c>Record-CurlExchange.ps1 -Ftp -UnixSocket</c>: curl sends the control
/// connection over the socket, but it cannot open a passive data connection over it - it resolves
/// the socket path as a host name and exits 6, whatever address the <c>PASV</c> reply names - so
/// the data connection is active, <c>-P 127.0.0.1</c>, to a loopback TCP listener curl opens.
/// With <c>-P -</c> curl finds no local address and exits 30. Inconclusive where no pinned build
/// is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlUsesSurlOverFtpOnAUnixSocketTests
{
    // CURLE_COULDNT_RESOLVE_HOST: curl resolves the socket path for a passive data connection.
    private const int CouldNotResolveHost = 6;

    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly byte[] HelloAgain = "hello again"u8.ToArray();
    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["a.txt"] = HelloWorld,
        ["dir/b.txt"] = "bee\n"u8.ToArray(),
    };

    private IsolatedCurlHome localFiles = null!;
    private string socketPath = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles()
    {
        localFiles = new IsolatedCurlHome();

        // Short enough for macOS's 103-byte sun_path under its temporary directory.
        socketPath = Path.Join(Path.GetTempPath(), "surl-" + Guid.NewGuid().ToString("N")[..8] + ".sock");
    }

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    [TestMethod]
    public async Task Download_ActiveDataConnection_WritesTheFile()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-P", "127.0.0.1", "ftp://localhost/a.txt");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task Download_ActiveDataConnectionWithPort_WritesTheFile()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-P", "127.0.0.1", "--disable-eprt", "ftp://localhost/a.txt");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task List_ActiveDataConnection_NamesTheFilesInTheDirectory()
    {
        await using var surl = await StartSurlAsync("--list-directories");

        var result = await RunCurlAsync("-P", "127.0.0.1", "ftp://localhost/dir/");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(Encoding.ASCII.GetString(result.StandardOutput), "b.txt");
    }

    [TestMethod]
    public async Task Upload_ActiveDataConnection_WritesTheFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await localFiles.WriteFileAsync("up.txt", HelloAgain, TestContext.CancellationToken);

        var result = await RunCurlAsync("-P", "127.0.0.1", "-T", upload, "ftp://localhost/b.txt");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await File.ReadAllBytesAsync(Path.Combine(surl.ServedDirectory!, "b.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("--disable-epsv")]
    public async Task Download_PassiveDataConnection_UpstreamCurlCannotResolveTheSocketPath(string options)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync([.. options.Split(' ', StringSplitOptions.RemoveEmptyEntries), "ftp://localhost/a.txt"]);

        Assert.AreEqual(CouldNotResolveHost, result.ExitCode, result.StandardError);
    }

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartOverUnixSocketAsync(
            "ftp", socketPath, ServedFiles, ["dir"], ["--allow-anonymous", .. options], TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", "--unix-socket", socketPath, .. arguments]);
}
