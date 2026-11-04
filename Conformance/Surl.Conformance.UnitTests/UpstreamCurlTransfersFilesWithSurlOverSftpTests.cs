namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build logs in to a live, in-process <c>surl sftp://</c> with a
/// password and downloads, lists, uploads and runs <c>-Q</c> commands, each case ADR-0054
/// decision 16 lists for SFTP, with the exit code and result it gives. surl serves a fresh
/// temporary directory holding <c>a.txt</c>, <c>dir/b.txt</c> and <c>.hidden.txt</c> with a
/// throwaway host key, which curl pins with <c>--hostpubsha256</c> from surl's fingerprint note;
/// curl runs with <c>HOME</c> and <c>USERPROFILE</c> in a temporary directory (ADR-0051 decision 8).
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTransfersFilesWithSurlOverSftpTests
{
    private const string NoSuchFile = "curl: (78) Could not open remote file for reading: No such file or directory";
    private const string MtimeDate = "\"Sun, 27 Sep 2026 12:34:56 GMT\"";

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
    [DataRow(".surl/lock")]
    public async Task Download_AbsentHiddenOrStateFile_Exits78NoSuchFile(string path)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl, surl.UrlOf(path));

        Assert.AreEqual(78, result.ExitCode, result.StandardError);
        Assert.AreEqual(NoSuchFile, result.StandardError.Trim());
    }

    [TestMethod]
    public async Task Download_Directory_Exits79()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl, surl.UrlOf("dir"));

        Assert.AreEqual(79, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Download_Range_WritesThoseBytes()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl, "-r", "0-4", surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual("hello"u8.ToArray(), result.StandardOutput);
    }

    [TestMethod]
    public async Task Download_ResumedFromPartialFile_CompletesIt()
    {
        await using var surl = await StartSurlAsync();
        var part = await curlHome.WriteFileAsync("part.txt", "hello"u8.ToArray(), TestContext.CancellationToken);

        var result = await RunCurlAsync(surl, "-C", "-", "-o", part, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, await File.ReadAllBytesAsync(part, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Download_RemoteTime_KeepsTheServedFilesModificationTime()
    {
        await using var surl = await StartSurlAsync();
        var output = curlHome.PathOf("out.txt");

        var result = await RunCurlAsync(surl, "-R", "-o", output, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            WholeSeconds(File.GetLastWriteTimeUtc(ServedPath(surl, "a.txt"))),
            File.GetLastWriteTimeUtc(output));
    }

    [TestMethod]
    public async Task Listing_ListingsOff_Exits78()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl, surl.UrlOf("dir/"));

        Assert.AreEqual(78, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "curl: (78) Could not open directory for reading: No such file or directory", result.StandardError.Trim());
    }

    [TestMethod]
    public async Task Listing_ListingsOn_WritesOneLongNameLinePerEntry()
    {
        await using var surl = await StartSurlAsync("--list-directories");

        var result = await RunCurlAsync(surl, surl.UrlOf("dir/"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var listing = System.Text.Encoding.UTF8.GetString(result.StandardOutput);
        StringAssert.EndsWith(listing, " b.txt\n");
        Assert.AreEqual(1, listing.Count(character => character == '\n'), listing);
    }

    [TestMethod]
    public async Task Listing_NamesOnly_ListsVisibleEntries()
    {
        await using var surl = await StartSurlAsync("--list-directories");

        var result = await RunCurlAsync(surl, "-l", surl.UrlOf(string.Empty));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual("a.txt\ndir\n"u8.ToArray(), result.StandardOutput);
    }

    [TestMethod]
    public async Task Upload_UploadsOff_Exits9AndWritesNothing()
    {
        await using var surl = await StartSurlAsync();
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-T", upload, surl.UrlOf("new.txt"));

        Assert.AreEqual(9, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "new.txt")));
    }

    [TestMethod]
    [DataRow(new string[0])]
    [DataRow(new[] { "--create-file-mode", "0600" })]
    public async Task Upload_UploadsOn_WritesTheFile(string[] modeOptions)
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, [.. modeOptions, "-T", upload, surl.UrlOf("new.txt")]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await ReadServedAsync(surl, "new.txt"));
    }

    [TestMethod]
    public async Task Upload_MissingDirectory_Exits78()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-T", upload, surl.UrlOf("nodir/new.txt"));

        Assert.AreEqual(78, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Upload_CreateDirectories_CreatesThemAndWritesTheFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "--ftp-create-dirs", "-T", upload, surl.UrlOf("new/deep/b.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await ReadServedAsync(surl, "new/deep/b.txt"));
    }

    [TestMethod]
    public async Task Upload_Append_AppendsToTheFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-a", "-T", upload, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual((byte[])[.. HelloWorld, .. HelloAgain], await ReadServedAsync(surl, "a.txt"));
    }

    [TestMethod]
    public async Task Upload_ResumedOverPartialFile_CompletesIt()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        await File.WriteAllBytesAsync(ServedPath(surl, "c.txt"), "hello"u8.ToArray(), TestContext.CancellationToken);
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-C", "-", "-T", upload, surl.UrlOf("c.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await ReadServedAsync(surl, "c.txt"));
    }

    [TestMethod]
    public async Task Upload_ResumedWithNoFile_WritesTheWholeFile()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-C", "-", "-T", upload, surl.UrlOf("c.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloAgain, await ReadServedAsync(surl, "c.txt"));
    }

    [TestMethod]
    public async Task Upload_PastMaxFilesize_Exits79AndWritesNothing()
    {
        await using var surl = await StartSurlAsync("--allow-uploads", "--max-filesize", "4");
        var upload = await WriteUploadAsync();

        var result = await RunCurlAsync(surl, "-T", upload, surl.UrlOf("new.txt"));

        Assert.AreEqual(79, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "new.txt")));
    }

    [TestMethod]
    [DataRow("rename /a.txt /c.txt")]
    [DataRow("rename /~/a.txt /~/c.txt")]
    public async Task Quote_Rename_RenamesBeforeTheDownload(string command)
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync(surl, "-Q", command, surl.UrlOf("c.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task Quote_RenameOntoExistingFile_Exits21()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync(surl, "-Q", "rename /a.txt /dir/b.txt", surl.UrlOf("a.txt"));

        Assert.AreEqual(21, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Quote_Remove_RemovesBeforeTheListing()
    {
        await using var surl = await StartSurlAsync("--allow-uploads", "--list-directories");

        var result = await RunCurlAsync(surl, "-Q", "rm /a.txt", "-l", surl.UrlOf(string.Empty));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual("dir\n"u8.ToArray(), result.StandardOutput);
    }

    [TestMethod]
    public async Task Quote_MakeThenRemoveDirectory_ListsItEmptyThenFindsItGone()
    {
        await using var surl = await StartSurlAsync("--allow-uploads", "--list-directories");

        var made = await RunCurlAsync(surl, "-Q", "mkdir /x", surl.UrlOf("x/"));
        var removed = await RunCurlAsync(surl, "-Q", "rmdir /x", surl.UrlOf("x/"));

        Assert.AreEqual(0, made.ExitCode, made.StandardError);
        Assert.AreEqual(0, made.StandardOutput.Length);
        Assert.AreEqual(78, removed.ExitCode, removed.StandardError);
    }

    [TestMethod]
    [DataRow("chmod 0600 /a.txt")]
    [DataRow("chown 1000 /a.txt")]
    [DataRow("chgrp 1000 /a.txt")]
    [DataRow("ln /a.txt /l.txt")]
    [DataRow("symlink /a.txt /l.txt")]
    [DataRow("statvfs /")]
    public async Task Quote_Unsupported_Exits21(string command)
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync(surl, "-Q", command, surl.UrlOf("a.txt"));

        Assert.AreEqual(21, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    [DataRow("*chmod 0600 /a.txt")]
    [DataRow("*statvfs /")]
    [DataRow("pwd")]
    [DataRow("atime " + MtimeDate + " /a.txt")]
    public async Task Quote_AcceptedOrFailureAllowed_DownloadsTheFile(string command)
    {
        await using var surl = await StartSurlAsync("--allow-uploads");

        var result = await RunCurlAsync(surl, "-Q", command, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task Quote_ModificationTime_SetsTheFilesTime()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var output = curlHome.PathOf("out.txt");

        var result = await RunCurlAsync(surl, "-Q", "mtime " + MtimeDate + " /a.txt", "-R", "-o", output, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(new DateTime(2026, 9, 27, 12, 34, 56, DateTimeKind.Utc), File.GetLastWriteTimeUtc(output));
    }

    [TestMethod]
    [DataRow("mtime " + MtimeDate + " /a.txt")]
    [DataRow("rm /a.txt")]
    public async Task Quote_ChangeWithUploadsOff_Exits21(string command)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl, "-Q", command, surl.UrlOf("a.txt"));

        Assert.AreEqual(21, result.ExitCode, result.StandardError);
    }

    private static DateTime WholeSeconds(DateTime time) => time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));

    private static string ServedPath(SurlOnLoopback surl, string relativePath) =>
        Path.Combine(surl.ServedDirectory!, relativePath);

    private Task<byte[]> ReadServedAsync(SurlOnLoopback surl, string relativePath) =>
        File.ReadAllBytesAsync(ServedPath(surl, relativePath), TestContext.CancellationToken);

    private Task<string> WriteUploadAsync() => curlHome.WriteFileAsync("up.txt", HelloAgain, TestContext.CancellationToken);

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync(
            "sftp",
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
