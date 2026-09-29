namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build reads files from a live, in-process <c>surl</c> over TFTP
/// (UDP) and exits with the exit code and stdout recorded in
/// <c>Surl.Protocol.Tftp.UnitTests/Fixtures</c>: a default read, a read with
/// <c>--tftp-blksize 1024</c>, a read with <c>--tftp-no-options</c>, a file of exactly 512
/// bytes, and a missing file. Each read serves the recording's stdout as the file, since the
/// recorder answered with those bytes as the file's contents, so every expected result is the
/// recording, never a copy of it. Inconclusive where no pinned build is installed for the
/// platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverTftpTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Read_DefaultOptions_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertReadMatchesRecordingAsync("default-read", "file.txt", serveFile: true);
    }

    [TestMethod]
    public async Task Read_TftpBlksize1024_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertReadMatchesRecordingAsync("blksize-1024-read", "big.txt", serveFile: true, "--tftp-blksize", "1024");
    }

    [TestMethod]
    public async Task Read_TftpNoOptions_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertReadMatchesRecordingAsync("no-options-read", "file.txt", serveFile: true, "--tftp-no-options");
    }

    [TestMethod]
    public async Task Read_ExactlyOneBlock_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertReadMatchesRecordingAsync("exactly-512-bytes", "exact-512.txt", serveFile: true);
    }

    [TestMethod]
    public async Task Read_MissingFile_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertReadMatchesRecordingAsync("missing-file", "missing.txt", serveFile: false);
    }

    private async Task AssertReadMatchesRecordingAsync(
        string fixtureFolder, string fileName, bool serveFile, params string[] tftpOptions)
    {
        var fixture = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Surl.Protocol.Tftp.UnitTests", "Fixtures", fixtureFolder);
        var expectedExitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixture, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var expectedOutput = await File.ReadAllBytesAsync(Path.Combine(fixture, "stdout.bin"), TestContext.CancellationToken);
        var files = serveFile
            ? new Dictionary<string, byte[]> { [fileName] = expectedOutput }
            : new Dictionary<string, byte[]>();
        await using var surl = await SurlOnLoopback.StartAsync("tftp", files, [], [], TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", .. tftpOptions, surl.UrlOf(fileName)]);

        Assert.AreEqual(expectedExitCode, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(expectedOutput, result.StandardOutput);
    }
}
