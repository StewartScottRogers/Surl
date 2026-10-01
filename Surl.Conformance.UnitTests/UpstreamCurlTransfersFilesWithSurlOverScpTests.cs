namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build logs in to a live, in-process <c>surl scp://</c> with a
/// password and downloads and uploads, each case ADR-0054 decision 16 lists for SCP, with the
/// exit code and result it gives. surl serves a fresh temporary directory holding
/// <c>a.txt</c>, <c>dir/b.txt</c> and <c>.hidden.txt</c> with a throwaway host key, which curl
/// pins with <c>--hostpubsha256</c> from surl's fingerprint note; curl runs with <c>HOME</c> and
/// <c>USERPROFILE</c> in a temporary directory (ADR-0051 decision 8). Inconclusive where no
/// pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTransfersFilesWithSurlOverScpTests
{
    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly byte[] HelloAgain = "hello again"u8.ToArray();
    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["a.txt"] = HelloWorld,
        ["dir/b.txt"] = "bee\n"u8.ToArray(),
        [".hidden.txt"] = "hidden\n"u8.ToArray(),
    };

    private IsolatedCurlHome curlHome = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateCurlHome() => curlHome = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteCurlHome() => curlHome.Dispose();

    [TestMethod]
    [DataRow("a.txt")]
    [DataRow("~/a.txt")]
    public async Task Download_ServedFile_WritesItsBytes(string path)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl, surl.UrlOf(path));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    [DataRow("missing.txt")]
    [DataRow(".hidden.txt")]
    [DataRow("dir")]
    public async Task Download_AbsentHiddenOrDirectory_Exits78FailedToReceive(string path)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl, surl.UrlOf(path));

        Assert.AreEqual(78, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (78) Failed to recv file", result.StandardError.Trim());
    }

    [TestMethod]
    public async Task Upload_UploadsOff_Exits25AndWritesNothing()
    {
        await using var surl = await StartSurlAsync();
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-T", upload, surl.UrlOf("new.txt"));

        Assert.AreEqual(25, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (25) failed to send file", result.StandardError.Trim());
        Assert.IsFalse(File.Exists(ServedPath(surl, "new.txt")));
    }

    [TestMethod]
    public async Task Upload_UploadsOn_WritesTheFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-T", upload, surl.UrlOf("new.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(
            HelloAgain, await File.ReadAllBytesAsync(ServedPath(surl, "new.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Upload_MissingDirectory_Exits25()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-T", upload, surl.UrlOf("nodir/new.txt"));

        Assert.AreEqual(25, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Upload_PastMaxFilesize_Exits25AndWritesNothing()
    {
        await using var surl = await StartSurlAsync("--allow-uploads", "--max-filesize", "4");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-T", upload, surl.UrlOf("new.txt"));

        Assert.AreEqual(25, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "new.txt")));
    }

    private static string ServedPath(SurlOnLoopback surl, string relativePath) =>
        Path.Combine(surl.ServedDirectory!, relativePath);

    private Task<string> WriteUploadAsync() => curlHome.WriteFileAsync("up.txt", HelloAgain, TestContext.CancellationToken);

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync(
            "scp",
            ServedFiles,
            ["dir"],
            ["--throwaway-hostkey", "--user", $"{AccountsFile.User}:{AccountsFile.Password}", "-v", .. options],
            TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(SurlOnLoopback surl, params string[] arguments) =>
        PinnedUpstreamCurlOverSsh.RunAsync(
            TestContext,
            curlHome,
            surl,
            [
                "--hostpubsha256",
                SshLogNotes.HostKeySha256(surl.Log, "ssh-rsa"),
                "-u",
                $"{AccountsFile.User}:{AccountsFile.Password}",
                .. arguments,
            ]);
}
