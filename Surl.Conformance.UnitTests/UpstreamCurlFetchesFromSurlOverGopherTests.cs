using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build fetches from a live, in-process <c>surl</c> over Gopher, and
/// its exit code and output are what the recordings in
/// <c>Surl.Protocol.Gopher.UnitTests/Fixtures</c> predict: the file's bytes, the root menu
/// with the port surl bound (under <c>--list-directories</c>, as listings are off by default),
/// and the error menu, each with exit code 0. Inconclusive where no
/// pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverGopherTests
{
    private static readonly byte[] FileBytes = "Hello from Surl.\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Fetch_FileSelector_ExitsZeroWithTheFilesBytes()
    {
        await using var surl = await StartSurlAsync();

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("0/file.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(FileBytes, result.StandardOutput);
    }

    [TestMethod]
    public async Task FetchWithListDirectories_RootMenu_ExitsZeroWithAMenuOfTheServedDirectory()
    {
        await using var surl = await StartSurlAsync("--list-directories");
        var port = surl.BaseUrl.Port;

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.BaseUrl.AbsoluteUri);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            $"0file.txt\t/file.txt\t127.0.0.1\t{port}\r\n1sub\t/sub\t127.0.0.1\t{port}\r\n.\r\n",
            Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Fetch_MissingSelector_ExitsZeroWithTheErrorMenu()
    {
        await using var surl = await StartSurlAsync();

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("0/missing.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n",
            Encoding.ASCII.GetString(result.StandardOutput));
    }

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync(
            "gopher",
            new Dictionary<string, byte[]> { ["file.txt"] = FileBytes },
            ["sub"],
            options,
            TestContext.CancellationToken);
}
