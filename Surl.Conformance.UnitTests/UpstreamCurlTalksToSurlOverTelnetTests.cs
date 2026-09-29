namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build runs a TELNET session against a live, in-process
/// <c>surl</c>, fed the standard input BL-035 recorded, and exits with the exit code and
/// stdout recorded in <c>Surl.Protocol.Telnet.UnitTests/Fixtures</c>: the plain session, the
/// session with <c>-t TTYPE=vt100 -t NEW_ENV=USER,alice</c>, and the session whose data holds
/// the byte 255. Each case's expected result is read from its fixture folder, so it is the
/// recording, never a copy of it. Inconclusive where no pinned build is installed for the
/// platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTalksToSurlOverTelnetTests
{
    private static readonly byte[] HelloThenQuit = "hello\nquit\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Session_PlainSession_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertSessionMatchesRecordingAsync("plain-session", HelloThenQuit);
    }

    [TestMethod]
    public async Task SessionWithTerminalTypeAndEnvironment_TelnetOptionsSession_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertSessionMatchesRecordingAsync(
            "telnet-options-session", HelloThenQuit, "-t", "TTYPE=vt100", "-t", "NEW_ENV=USER,alice");
    }

    [TestMethod]
    public async Task Session_IacInData_ExitsAndPrintsWhatTheRecordingHolds()
    {
        await AssertSessionMatchesRecordingAsync("iac-in-data", [0xFF, .. "ab\nquit\n"u8]);
    }

    private async Task AssertSessionMatchesRecordingAsync(
        string fixtureFolder, byte[] standardInput, params string[] telnetOptions)
    {
        var fixture = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Surl.Protocol.Telnet.UnitTests", "Fixtures", fixtureFolder);
        var expectedExitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixture, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var expectedOutput = await File.ReadAllBytesAsync(Path.Combine(fixture, "stdout.bin"), TestContext.CancellationToken);
        await using var surl = await SurlOnLoopback.StartAsync(
            "telnet", new Dictionary<string, byte[]>(), [], [], TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunWithStandardInputAsync(
            TestContext,
            standardInput,
            ["-sS", .. telnetOptions, $"telnet://{surl.BaseUrl.Host}:{surl.BaseUrl.Port}"]);

        Assert.AreEqual(expectedExitCode, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(expectedOutput, result.StandardOutput);
    }
}
