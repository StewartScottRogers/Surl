namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build uploads a file with <c>-T</c> over <c>ftp://</c> to a live,
/// in-process <c>surl</c> and fetches it over <c>smb://</c> from the same <c>surl</c>, and gets
/// back exactly the bytes it uploaded, because the file servers share one content store
/// (ADR-0100 decisions 1 and 5; BL-1248). It sits beside
/// <see cref="UpstreamCurlSharesFilesAcrossSurlProtocolsTests"/>, whose pairs use the reference
/// build, because <c>smb</c> needs the build that lists it: on Windows ADR-0030's static-curl
/// one. The FTP upload runs with the reference build and the SMB fetch with
/// <see cref="PinnedUpstreamCurl.RunForProtocolAsync"/>; inconclusive, with the pin named, where
/// no pinned build lists <c>smb</c>. SMB serves each top-level folder as a share (ADR-0073), so
/// the file goes to <c>docs/shared.bin</c>.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlSharesFilesFromFtpToSmbTests
{
    private static readonly string[] Schemes = ["ftp", "smb"];

    // Not ASCII text alone, so a transfer that rewrote line ends or stopped at a zero byte fails.
    private static readonly byte[] Uploaded = [.. "shared across protocols\r\n"u8, 0x00, 0xFF, 0x7F, .. "\nend"u8];

    private IsolatedCurlHome curlHome = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateCurlHome() => curlHome = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteCurlHome() => curlHome.Dispose();

    [TestMethod]
    [DataRow(false, DisplayName = "in memory")]
    [DataRow(true, DisplayName = "with --directory")]
    public async Task UploadOverFtp_FetchOverSmb_FetchesTheUploadedBytes(bool withDataDirectory)
    {
        await WithSurlAsync(withDataDirectory, async surl =>
        {
            var local = await curlHome.WriteFileAsync("up.bin", Uploaded, TestContext.CancellationToken);
            var upload = await PinnedUpstreamCurl.RunAsync(
                TestContext,
                "-sS", "-f", "-m", "20", "--ftp-create-dirs", "-u", Account, "-T", local, UrlOf(surl, "ftp"));

            var fetch = await PinnedUpstreamCurl.RunForProtocolAsync(
                TestContext, "smb", "-sS", "-m", "20", "-u", Account, UrlOf(surl, "smb"));

            Assert.AreEqual(0, upload.ExitCode, upload.StandardError);
            Assert.AreEqual(0, fetch.ExitCode, fetch.StandardError);
            CollectionAssert.AreEqual(Uploaded, fetch.StandardOutput);
        });
    }

    private static string Account => $"{AccountsFile.User}:{AccountsFile.Password}";

    private static string UrlOf(SurlOnLoopback surl, string scheme) =>
        new Uri(surl.BaseUrlOf(scheme), "docs/shared.bin").AbsoluteUri;

    private async Task WithSurlAsync(bool withDataDirectory, Func<SurlOnLoopback, Task> exchange)
    {
        string[] options = ["--allow-uploads", "--allow-plaintext-auth", "--auth", "ntlmv1", "--user", Account];
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
}
