namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build that supports SMB logs in to a live, in-process
/// <c>surl smb://</c> and <c>smbs://</c> with NTLMv1 and downloads and uploads files, each case
/// ADR-0073 decision 11 lists, with the exit code and result it gives. surl serves a fresh
/// temporary directory holding <c>files/hello.txt</c>, <c>files/big.bin</c> (40000 bytes),
/// <c>files/sub/</c> and <c>.hidden/x.txt</c>. On Windows the build is ADR-0030's static-curl
/// one, because the reference build has no <c>smb</c>; inconclusive, with the pin named, where
/// no pinned build of the platform lists <c>smb</c> or none is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTransfersFilesWithSurlOverSmbTests
{
    private const string Account = "alice:secret";

    private static readonly byte[] HelloSmb = "hello smb\n"u8.ToArray();
    private static readonly byte[] UploadText = "uploaded\n"u8.ToArray();
    private static readonly byte[] Big = [.. Enumerable.Range(0, 40000).Select(index => (byte)(index * 7 % 251))];
    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["files/hello.txt"] = HelloSmb,
        ["files/big.bin"] = Big,
        [".hidden/x.txt"] = "hidden\n"u8.ToArray(),
    };

    private static readonly string[] ServedDirectories = ["files", "files/sub", ".hidden"];
    private static readonly string[] AccountOptions = ["--auth", "ntlmv1", "-u", Account];

    private IsolatedCurlHome curlHome = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateCurlHome() => curlHome = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteCurlHome() => curlHome.Dispose();

    [TestMethod]
    [DataRow("files/hello.txt")]
    [DataRow("files/big.bin")]
    public async Task Download_ServedFile_WritesItsBytes(string path)
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-u", Account, surl.UrlOf(path));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(ServedFiles[path], result.StandardOutput);
    }

    [TestMethod]
    [DataRow("WORKGROUP/alice:secret")]
    [DataRow("WORKGROUP\\alice:secret")]
    public async Task Download_UserWithDomain_LogsInAsTheUser(string user)
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-u", user, surl.UrlOf("files/hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloSmb, result.StandardOutput);
    }

    [TestMethod]
    [DataRow("alice:wrong")]
    [DataRow("ALICE:secret")]
    public async Task Download_WrongPasswordOrUserNameCase_Exits67(string user)
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-u", user, surl.UrlOf("files/hello.txt"));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Download_NoUser_CurlItselfExits67()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync(surl.UrlOf("files/hello.txt"));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Download_NoFileAfterTheShare_CurlItselfExits3()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-u", Account, surl.UrlOf("files"));

        Assert.AreEqual(3, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    [DataRow("nosuch/hello.txt")]
    [DataRow(".hidden/x.txt")]
    [DataRow("files/missing.txt")]
    [DataRow("files/sub")]
    [DataRow("files/")]
    public async Task Download_MissingShareOrFileHiddenShareOrDirectory_Exits78(string path)
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-u", Account, surl.UrlOf(path));

        Assert.AreEqual(78, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Download_DotDotPathAsIs_Exits78()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync(
            "-u", Account, "--path-as-is", $"{surl.BaseUrl.AbsoluteUri}files/../files/hello.txt");

        Assert.AreEqual(78, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Upload_UploadsOff_Exits9AndStoresNothing()
    {
        await using var surl = await StartSurlAsync(AccountOptions);
        var upload = await curlHome.WriteFileAsync("up.txt", UploadText, TestContext.CancellationToken);

        var result = await RunCurlAsync("-u", Account, "-T", upload, surl.UrlOf("files/up.txt"));

        Assert.AreEqual(9, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "files/up.txt")));
    }

    [TestMethod]
    [DataRow("up.txt", "files/up.txt")]
    [DataRow("big.bin", "files/big2.bin")]
    public async Task Upload_UploadsOn_StoresTheBytes(string name, string path)
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-uploads"]);
        var contents = name == "big.bin" ? Big : UploadText;
        var upload = await curlHome.WriteFileAsync(name, contents, TestContext.CancellationToken);

        var result = await RunCurlAsync("-u", Account, "-T", upload, surl.UrlOf(path));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(
            contents, await File.ReadAllBytesAsync(ServedPath(surl, path), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Upload_MissingShare_Exits78()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-uploads"]);
        var upload = await curlHome.WriteFileAsync("up.txt", UploadText, TestContext.CancellationToken);

        var result = await RunCurlAsync("-u", Account, "-T", upload, surl.UrlOf("nodir/up.txt"));

        Assert.AreEqual(78, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Upload_PastMaxFilesize_Exits25AndLeavesNoFile()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-uploads", "--max-filesize", "1000"]);
        var upload = await curlHome.WriteFileAsync("big.bin", Big, TestContext.CancellationToken);

        var result = await RunCurlAsync("-u", Account, "-T", upload, surl.UrlOf("files/big3.bin"));

        Assert.AreEqual(25, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(ServedPath(surl, "files/big3.bin")));
    }

    [TestMethod]
    [DataRow("--auth", "ntlmv1")]
    [DataRow("-u", Account)]
    public async Task Download_NoAccountsOrNtlmV1NotAccepted_Exits67(string option, string value)
    {
        await using var surl = await StartSurlAsync(option, value);

        var result = await RunCurlAsync("-u", Account, surl.UrlOf("files/hello.txt"));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Download_AllowAnonymous_LetsAnyLoginIn()
    {
        await using var surl = await StartSurlAsync("--allow-anonymous");

        var result = await RunCurlAsync("-u", "anyone:anything", surl.UrlOf("files/hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloSmb, result.StandardOutput);
    }

    [TestMethod]
    public async Task Smbs_Insecure_WritesTheFile()
    {
        await using var surl = await StartSurlAsync("smbs", [.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-k", "-u", Account, surl.UrlOf("files/hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloSmb, result.StandardOutput);
    }

    [TestMethod]
    public async Task Smbs_SelfSignedCertificateNotTrusted_Exits60()
    {
        await using var surl = await StartSurlAsync("smbs", [.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-u", Account, surl.UrlOf("files/hello.txt"));

        Assert.AreEqual(60, result.ExitCode, result.StandardError);
    }

    private static string ServedPath(SurlOnLoopback surl, string path) =>
        Path.Combine(surl.ServedDirectory!, path.Replace('/', Path.DirectorySeparatorChar));

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) => StartSurlAsync("smb", options);

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, string[] options) =>
        SurlOnLoopback.StartAsync(scheme, ServedFiles, ServedDirectories, options, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunForProtocolAsync(TestContext, "smb", ["-sS", "-m", "20", .. arguments]);
}
