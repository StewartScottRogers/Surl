namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build uploads a file with <c>-T</c> over one listener of a live,
/// in-process <c>surl</c> and fetches it over another listener of the same <c>surl</c>, and gets
/// back exactly the bytes it uploaded, because every file server shares one content store
/// (ADR-0100 decisions 1 and 5; BL-638). The pairs are ADR-0100 decision 5's: <c>ftp</c> then
/// <c>http</c>, <c>sftp</c> then <c>ftp</c>, and <c>http</c> (a <c>PUT</c>, ADR-0133 decision 2)
/// then <c>scp</c>; each runs in memory and over a data directory. One <c>surl</c> serves
/// <c>ftp</c>, <c>http</c>, <c>scp</c> and <c>sftp</c> under <c>--allow-uploads</c> with the
/// account <c>tester:secret</c> from <c>--user</c> and a throwaway host key, which curl pins with
/// <c>--hostpubsha256</c> from surl's fingerprint note; curl runs with <c>-sS -f</c> and
/// <c>HOME</c> and <c>USERPROFILE</c> in a temporary directory (ADR-0051 decision 8).
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlSharesFilesAcrossSurlProtocolsTests
{
    private static readonly string[] Schemes = ["ftp", "http", "scp", "sftp"];

    // Not ASCII text alone, so a transfer that rewrote line ends or stopped at a zero byte fails.
    private static readonly byte[] Uploaded = [.. "shared across protocols\r\n"u8, 0x00, 0xFF, 0x7F, .. "\nend"u8];

    private static readonly byte[] Overwritten = "written last, over sftp\n"u8.ToArray();

    private IsolatedCurlHome curlHome = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateCurlHome() => curlHome = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteCurlHome() => curlHome.Dispose();

    [TestMethod]
    [DataRow(false, DisplayName = "in memory")]
    [DataRow(true, DisplayName = "with --directory")]
    public async Task UploadOverFtp_GetOverHttp_FetchesTheUploadedBytes(bool withDataDirectory)
    {
        await WithSurlAsync(withDataDirectory, async surl =>
        {
            var upload = await RunCurlAsync(
                surl, "--ftp-create-dirs", "-T", await WriteLocalAsync("up.bin", Uploaded), UrlOf(surl, "ftp", "dir/f.bin"));
            var fetch = await RunCurlAsync(surl, UrlOf(surl, "http", "dir/f.bin"));

            AssertFetched(Uploaded, upload, fetch);
        });
    }

    [TestMethod]
    [DataRow(false, DisplayName = "in memory")]
    [DataRow(true, DisplayName = "with --directory")]
    public async Task UploadOverSftp_FetchOverFtp_FetchesTheUploadedBytes(bool withDataDirectory)
    {
        await WithSurlAsync(withDataDirectory, async surl =>
        {
            var upload = await RunCurlAsync(
                surl, "--ftp-create-dirs", "-T", await WriteLocalAsync("up.bin", Uploaded), UrlOf(surl, "sftp", "dir/f.bin"));
            var fetch = await RunCurlAsync(surl, UrlOf(surl, "ftp", "dir/f.bin"));

            AssertFetched(Uploaded, upload, fetch);
        });
    }

    [TestMethod]
    [DataRow(false, DisplayName = "in memory")]
    [DataRow(true, DisplayName = "with --directory")]
    public async Task PutOverHttp_FetchOverScp_FetchesTheUploadedBytes(bool withDataDirectory)
    {
        await WithSurlAsync(withDataDirectory, async surl =>
        {
            var upload = await RunCurlAsync(surl, "-T", await WriteLocalAsync("up.bin", Uploaded), UrlOf(surl, "http", "f.bin"));
            var fetch = await RunCurlAsync(surl, UrlOf(surl, "scp", "f.bin"));

            AssertFetched(Uploaded, upload, fetch);
        });
    }

    [TestMethod]
    [DataRow(false, DisplayName = "in memory")]
    [DataRow(true, DisplayName = "with --directory")]
    public async Task UploadOverFtp_OverwriteOverSftp_GetOverHttp_FetchesTheLastWrite(bool withDataDirectory)
    {
        await WithSurlAsync(withDataDirectory, async surl =>
        {
            var first = await RunCurlAsync(surl, "-T", await WriteLocalAsync("first.bin", Uploaded), UrlOf(surl, "ftp", "f.bin"));
            var overwrite = await RunCurlAsync(
                surl, "-T", await WriteLocalAsync("last.bin", Overwritten), UrlOf(surl, "sftp", "f.bin"));
            var fetch = await RunCurlAsync(surl, UrlOf(surl, "http", "f.bin"));

            Assert.AreEqual(0, first.ExitCode, first.StandardError);
            AssertFetched(Overwritten, overwrite, fetch);
        });
    }

    private static void AssertFetched(byte[] expected, UpstreamCurlRunResult upload, UpstreamCurlRunResult fetch)
    {
        Assert.AreEqual(0, upload.ExitCode, upload.StandardError);
        Assert.AreEqual(0, fetch.ExitCode, fetch.StandardError);
        CollectionAssert.AreEqual(expected, fetch.StandardOutput);
    }

    private static string UrlOf(SurlOnLoopback surl, string scheme, string relativePath) =>
        new Uri(surl.BaseUrlOf(scheme), relativePath).AbsoluteUri;

    private async Task WithSurlAsync(bool withDataDirectory, Func<SurlOnLoopback, Task> exchange)
    {
        string[] options =
        [
            "--allow-uploads",
            "--allow-plaintext-auth",
            "--throwaway-hostkey",
            "--user",
            $"{AccountsFile.User}:{AccountsFile.Password}",
            "-v",
        ];
        if (!withDataDirectory)
        {
            await using var inMemory = await SurlOnLoopback.StartInMemoryAsync(Schemes, options, TestContext.CancellationToken);
            await exchange(inMemory);
            return;
        }

        var dataDirectory = Directory.CreateTempSubdirectory("surl-conformance-");
        try
        {
            await using var overDirectory = await SurlOnLoopback.StartOverDirectoryAsync(
                Schemes, dataDirectory.FullName, options, TestContext.CancellationToken);
            await exchange(overDirectory);
        }
        finally
        {
            dataDirectory.Delete(recursive: true);
        }
    }

    private Task<string> WriteLocalAsync(string name, byte[] contents) =>
        curlHome.WriteFileAsync(name, contents, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(SurlOnLoopback surl, params string[] arguments) =>
        PinnedUpstreamCurlOverSsh.RunAsync(
            TestContext,
            curlHome,
            surl,
            [
                "-f",
                "--hostpubsha256",
                SshLogNotes.HostKeySha256(surl.Log, "ssh-rsa"),
                "-u",
                $"{AccountsFile.User}:{AccountsFile.Password}",
                .. arguments,
            ]);
}
