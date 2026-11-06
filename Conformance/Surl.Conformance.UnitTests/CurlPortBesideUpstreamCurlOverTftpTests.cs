using System.Runtime.CompilerServices;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for TFTP (ADR-0089, BL-584): every conversation <see cref="UpstreamCurlFetchesFromSurlOverTftpTests"/>
/// runs, through pinned upstream curl and the pinned Curl port, each against its own fresh <c>surl</c>,
/// judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/> and listed or not in
/// <c>Documentation/Conformance/CurlPort/Dict-Gopher-Telnet-Tftp-Mqtt.md</c> (decision 5). TFTP is UDP,
/// so no request bytes are recorded (decision 4): the exit code, standard output and <c>surl</c>'s
/// <c>-v</c> notes carry the comparison. The mirrored upload test's three client runs are three cases
/// here, the upload, the read back and the read from a fresh <c>surl</c>; the read's own upload is a
/// set-up step run once through upstream curl (amendment 1). Inconclusive when either pinned binary is
/// absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverTftpTests
{
    private const string FindingsFile = "Dict-Gopher-Telnet-Tftp-Mqtt.md";

    private const string Mirrored = nameof(UpstreamCurlFetchesFromSurlOverTftpTests);

    private static readonly byte[] Uploaded = [.. "surl in memory "u8, 0x00, 0x01, 0xFE, 0xFF];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverTftpTests.Read_DefaultOptions_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task Read_DefaultOptions_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeReadAsync("default-read", "file.txt", serveFile: true);

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverTftpTests.Read_TftpBlksize1024_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task Read_TftpBlksize1024_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeReadAsync("blksize-1024-read", "big.txt", serveFile: true, ["--tftp-blksize", "1024"]);

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverTftpTests.Read_TftpNoOptions_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task Read_TftpNoOptions_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeReadAsync("no-options-read", "file.txt", serveFile: true, ["--tftp-no-options"]);

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverTftpTests.Read_ExactlyOneBlock_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task Read_ExactlyOneBlock_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeReadAsync("exactly-512-bytes", "exact-512.txt", serveFile: true);

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverTftpTests.Read_MissingFile_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task Read_MissingFile_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeReadAsync("missing-file", "missing.txt", serveFile: false);

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverTftpTests.UploadThenRead_NoDirectory_KeepsTheUploadInMemoryForTheProcessLifetimeOnly")]
    public async Task UploadThenRead_NoDirectory_KeepsTheUploadInMemoryForTheProcessLifetimeOnly()
    {
        const string Method = nameof(UploadThenRead_NoDirectory_KeepsTheUploadInMemoryForTheProcessLifetimeOnly);
        using var localFiles = new IsolatedCurlHome();
        var localFile = await localFiles.WriteFileAsync("up.bin", Uploaded, TestContext.CancellationToken);
        var missingOnDiskExitCode = await MeasureMissingOnDiskExitCodeAsync("up.bin");

        await JudgeAsync(
            Case($"{Method}[upload]", Method, ["-sS", "-T", localFile, "{surl}/up.bin"], ["--allow-uploads"], Exits(0, [])),
            InMemory(["--allow-uploads"]));
        await JudgeAsync(
            Case($"{Method}[read]", Method, ["-sS", "{surl}/up.bin"], ["--allow-uploads"], Exits(0, Uploaded)),
            new CurlPortRunSetup { StartSurlAsync = cancellationToken => StartInMemoryHoldingUploadAsync(localFile, cancellationToken) });
        await JudgeAsync(
            Case($"{Method}[fresh]", Method, ["-sS", "{surl}/up.bin"], [], Exits(missingOnDiskExitCode, [])),
            InMemory([]));

        Assert.AreNotEqual(0, missingOnDiskExitCode);
        Assert.IsFalse(File.Exists(Path.Combine(Environment.CurrentDirectory, "up.bin")));
    }

    private static CurlPortMeasurementCase Case(
        string name, string mirrors, IReadOnlyList<string> arguments, IReadOnlyList<string> surlOptions, Func<CurlPortMeasuredRun, string?> check,
        IReadOnlyDictionary<string, byte[]>? files = null) =>
        new(name, $"{Mirrored}.{mirrors}", "tftp", arguments, "tftp", surlOptions, files ?? new Dictionary<string, byte[]>(), check);

    private static CurlPortRunSetup InMemory(string[] surlOptions) =>
        new() { StartSurlAsync = cancellationToken => SurlOnLoopback.StartInMemoryAsync("tftp", surlOptions, cancellationToken) };

    private static Func<CurlPortMeasuredRun, string?> Exits(int exitCode, byte[] expectedOutput) =>
        run => run.ExitCode == exitCode && run.StandardOutput.AsSpan().SequenceEqual(expectedOutput)
            ? null
            : $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    /// <summary>
    /// Starts an in-memory <c>surl --allow-uploads</c> and uploads <paramref name="localFile"/> to it as
    /// <c>up.bin</c> through upstream curl, the set-up step before the measured read.
    /// </summary>
    private async Task<SurlOnLoopback> StartInMemoryHoldingUploadAsync(string localFile, CancellationToken cancellationToken)
    {
        var surl = await SurlOnLoopback.StartInMemoryAsync("tftp", ["--allow-uploads"], cancellationToken);
        var upload = await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", "-T", localFile, surl.UrlOf("up.bin")]);
        Assert.AreEqual(0, upload.ExitCode, $"Surl defect: upstream's set-up upload failed: {upload.StandardError}");
        return surl;
    }

    /// <summary>
    /// Measures, with upstream curl as the mirrored test does, the exit code a <c>surl</c> serving an empty
    /// directory on disk answers a read of <paramref name="fileName"/> with.
    /// </summary>
    private async Task<int> MeasureMissingOnDiskExitCodeAsync(string fileName)
    {
        await using var onDisk = await SurlOnLoopback.StartAsync("tftp", new Dictionary<string, byte[]>(), [], [], TestContext.CancellationToken);
        var result = await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", onDisk.UrlOf(fileName)]);
        Assert.IsNotNull(result.ExitCode);
        return result.ExitCode.Value;
    }

    private async Task JudgeReadAsync(
        string fixtureFolder, string fileName, bool serveFile, string[]? tftpOptions = null, [CallerMemberName] string method = "")
    {
        var fixture = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Protocols", "FileTransfer", "Surl.Protocol.Tftp.UnitTests", "Fixtures", fixtureFolder);
        var expectedExitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixture, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var expectedOutput = await File.ReadAllBytesAsync(Path.Combine(fixture, "stdout.bin"), TestContext.CancellationToken);
        var files = serveFile ? new Dictionary<string, byte[]> { [fileName] = expectedOutput } : new Dictionary<string, byte[]>();

        await JudgeAsync(
            Case(method, method, ["-sS", .. tftpOptions ?? [], "{surl}/" + fileName], [], Exits(expectedExitCode, expectedOutput), files),
            CurlPortRunSetup.None);
    }

    private async Task JudgeAsync(CurlPortMeasurementCase measurementCase, CurlPortRunSetup setup)
    {
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, setup);

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, measurementCase.Name, measurement);
    }
}
