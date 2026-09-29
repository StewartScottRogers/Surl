namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build reads files from a live, in-process <c>surl</c> over TFTP
/// (UDP) and exits with the exit code and stdout recorded in
/// <c>Surl.Protocol.Tftp.UnitTests/Fixtures</c>: a default read, a read with
/// <c>--tftp-blksize 1024</c>, a read with <c>--tftp-no-options</c>, a file of exactly 512
/// bytes, and a missing file. Each read serves the recording's stdout as the file, since the
/// recorder answered with those bytes as the file's contents, so every expected result is the
/// recording, never a copy of it. It also uploads to and reads back from a surl run without
/// <c>--directory</c>, which keeps the file in memory only (ADR-0031 decision 4).
/// Inconclusive where no pinned build is installed for the platform.
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

    [TestMethod]
    public async Task UploadThenRead_NoDirectory_KeepsTheUploadInMemoryForTheProcessLifetimeOnly()
    {
        byte[] uploaded = [.. "surl in memory "u8, 0x00, 0x01, 0xFE, 0xFF];
        var localDirectory = Directory.CreateTempSubdirectory("surl-conformance-upload-");
        try
        {
            var localFile = Path.Combine(localDirectory.FullName, "up.bin");
            await File.WriteAllBytesAsync(localFile, uploaded, TestContext.CancellationToken);
            await using (var surl = await SurlOnLoopback.StartInMemoryAsync("tftp", ["--allow-uploads"], TestContext.CancellationToken))
            {
                var upload = await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", "-T", localFile, surl.UrlOf("up.bin")]);
                var read = await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", surl.UrlOf("up.bin")]);

                Assert.AreEqual(0, upload.ExitCode, upload.StandardError);
                Assert.AreEqual(0, read.ExitCode, read.StandardError);
                CollectionAssert.AreEqual(uploaded, read.StandardOutput);
            }

            Assert.IsFalse(File.Exists(Path.Combine(Environment.CurrentDirectory, "up.bin")));
            await AssertFreshInMemorySurlAnswersAsMissingAsync("up.bin");
        }
        finally
        {
            localDirectory.Delete(recursive: true);
        }
    }

    // Measures, with the pinned build, the exit code a surl serving an empty directory on disk
    // answers a read of a missing file with, and asserts a fresh in-memory surl answers the same.
    private async Task AssertFreshInMemorySurlAnswersAsMissingAsync(string fileName)
    {
        int? missingOnDiskExitCode;
        await using (var onDisk = await SurlOnLoopback.StartAsync("tftp", new Dictionary<string, byte[]>(), [], [], TestContext.CancellationToken))
        {
            missingOnDiskExitCode = (await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", onDisk.UrlOf(fileName)])).ExitCode;
        }

        await using var inMemory = await SurlOnLoopback.StartInMemoryAsync("tftp", [], TestContext.CancellationToken);
        var fresh = await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", inMemory.UrlOf(fileName)]);

        Assert.IsNotNull(missingOnDiskExitCode);
        Assert.AreNotEqual(0, missingOnDiskExitCode);
        Assert.AreEqual(missingOnDiskExitCode, fresh.ExitCode, fresh.StandardError);
        Assert.IsEmpty(fresh.StandardOutput);
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
