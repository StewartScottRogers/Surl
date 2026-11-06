namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build uploads to and manages files on a live, in-process
/// <c>surl --allow-anonymous ftp://</c>, each upload case ADR-0052 decision 12 lists:
/// <c>-T</c> with and without <c>--allow-uploads</c>, <c>-a -T</c>, <c>-C - -T</c>,
/// <c>--ftp-create-dirs</c>, an upload past <c>--max-filesize</c>, and <c>-Q</c> deletes,
/// renames, directory changes and <c>SITE</c>, with the exit code curl gives. surl serves a
/// fresh temporary directory holding <c>a.txt</c> and <c>c.txt</c>, which each test reads back.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlUploadsToSurlOverFtpTests
{
    // CURLE_QUOTE_ERROR: a -Q command was answered with an error.
    private const int QuoteError = 21;

    // CURLE_UPLOAD_FAILED: STOR was answered 550.
    private const int UploadFailed = 25;

    // CURLE_REMOTE_DISK_FULL: the upload was answered 552.
    private const int RemoteDiskFull = 70;

    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly byte[] HelloAgain = "hello again"u8.ToArray();
    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["a.txt"] = HelloWorld,
        ["c.txt"] = "hello"u8.ToArray(),
    };

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    [TestMethod]
    public async Task Upload_UploadsOff_ExitsUploadFailedAndWritesNothing()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-T", await WriteUploadAsync(), surl.UrlOf("b.txt"));

        Assert.AreEqual(UploadFailed, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (25) Failed FTP upload: 550", result.StandardError.Trim());
        Assert.IsFalse(File.Exists(ServedPath(surl, "b.txt")));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-P -")]
    public async Task Upload_UploadsOn_WritesTheFile(string options)
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync([.. Split(options), "-T", await WriteUploadAsync(), surl.UrlOf("b.txt")]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await ReadServedAsync(surl, "b.txt"));
    }

    [TestMethod]
    public async Task Append_ExistingFile_AppendsTheUpload()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync("-a", "-T", await WriteUploadAsync(), surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld.Concat(HelloAgain).ToArray(), await ReadServedAsync(surl, "a.txt"));
    }

    [TestMethod]
    public async Task ResumeUpload_PartialFile_SendsTheRest()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync("-C", "-", "-T", await WriteUploadAsync(), surl.UrlOf("c.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await ReadServedAsync(surl, "c.txt"));
    }

    [TestMethod]
    public async Task Upload_CreateDirs_CreatesEachDirectoryAndWritesTheFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync("--ftp-create-dirs", "-T", await WriteUploadAsync(), surl.UrlOf("new/deep/b.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await ReadServedAsync(surl, "new/deep/b.txt"));
    }

    [TestMethod]
    public async Task Upload_PastMaxFilesize_ExitsRemoteDiskFullAndWritesNothing()
    {
        await using var surl = await StartSurlAsync("--allow-uploads", "--max-filesize", "4");

        var result = await RunCurlAsync("-T", await WriteUploadAsync(), surl.UrlOf("b.txt"));

        Assert.AreEqual(RemoteDiskFull, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "b.txt")));
    }

    [TestMethod]
    public async Task Quote_Delete_DeletesTheFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads", "--list-directories");

        var result = await RunCurlAsync("-Q", "DELE a.txt", surl.UrlOf(""));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "a.txt")));
    }

    [TestMethod]
    public async Task Quote_Rename_RenamesTheFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads", "--list-directories");

        var result = await RunCurlAsync("-Q", "RNFR a.txt", "-Q", "RNTO renamed.txt", surl.UrlOf(""));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "a.txt")));
        CollectionAssert.AreEqual(HelloWorld, await ReadServedAsync(surl, "renamed.txt"));
    }

    [TestMethod]
    public async Task Quote_MakeThenRemoveDirectoryAroundADownload_WritesTheFileAndLeavesNoDirectory()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync("-Q", "MKD x", "-Q", "-RMD x", surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        Assert.IsFalse(Directory.Exists(ServedPath(surl, "x")));
    }

    [TestMethod]
    public async Task Quote_DeleteWithUploadsOff_ExitsQuoteErrorAndKeepsTheFile()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-Q", "DELE a.txt", surl.UrlOf(""));

        Assert.AreEqual(QuoteError, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (21) QUOT command failed with 550", result.StandardError.Trim());
        Assert.IsTrue(File.Exists(ServedPath(surl, "a.txt")));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("--allow-uploads")]
    public async Task Quote_SiteChmod_ExitsQuoteError(string options)
    {
        await using var surl = await StartSurlAsync(Split(options));

        var result = await RunCurlAsync("-Q", "SITE CHMOD 644 a.txt", surl.UrlOf("a.txt"));

        Assert.AreEqual(QuoteError, result.ExitCode, result.StandardError);
    }

    private static string[] Split(string options) => options.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string ServedPath(SurlOnLoopback surl, string relativePath) =>
        Path.Combine(surl.ServedDirectory!, relativePath);

    private Task<byte[]> ReadServedAsync(SurlOnLoopback surl, string relativePath) =>
        File.ReadAllBytesAsync(ServedPath(surl, relativePath), TestContext.CancellationToken);

    private Task<string> WriteUploadAsync() => localFiles.WriteFileAsync("up.txt", HelloAgain, TestContext.CancellationToken);

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync("ftp", ServedFiles, [], ["--allow-anonymous", .. options], TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", .. arguments]);
}
