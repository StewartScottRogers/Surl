namespace Surl.Conformance;

/// <summary>
/// Phase 7 for DICT (ADR-0089, BL-584): every conversation <see cref="UpstreamCurlQueriesSurlOverDictTests"/>
/// runs, through pinned upstream curl and the pinned Curl port, each against its own fresh <c>surl</c>,
/// judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/> and listed or not in
/// <c>Documentation/Conformance/CurlPort/Dict-Gopher-Telnet-Tftp-Mqtt.md</c> (decision 5). The relay
/// records every <c>dict://</c> connection. Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverDictTests
{
    private const string FindingsFile = "Dict-Gopher-Telnet-Tftp-Mqtt.md";

    private static readonly Dictionary<string, byte[]> ServedWords = new()
    {
        ["hello"] = "A greeting.\n"u8.ToArray(),
        ["help"] = "Assistance.\n"u8.ToArray(),
        ["world"] = "The earth.\n"u8.ToArray(),
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mirrors UpstreamCurlQueriesSurlOverDictTests.Query_RecordedCase_ExitsAndWritesWhatTheRecordingHolds")]
    [DataRow("define-hello", "d:hello")]
    [DataRow("match-hel", "m:hel")]
    [DataRow("bare-hello", "hello")]
    [DataRow("define-missing", "d:missing")]
    [DataRow("show-db", "show:db")]
    public async Task Query_RecordedCase_ExitsAndWritesWhatTheRecordingHolds(string fixture, string path)
    {
        var fixtureDirectory = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Protocols", "Lookup", "Surl.Protocol.Dict.UnitTests", "Fixtures", fixture);
        var expectedExitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixtureDirectory, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var expectedOutput = await File.ReadAllBytesAsync(Path.Combine(fixtureDirectory, "stdout.bin"), TestContext.CancellationToken);
        const string Method = nameof(Query_RecordedCase_ExitsAndWritesWhatTheRecordingHolds);
        var name = $"{Method}[{fixture}, {path}]";
        var measurementCase = new CurlPortMeasurementCase(
            name,
            $"{nameof(UpstreamCurlQueriesSurlOverDictTests)}.{Method}",
            "dict",
            ["-sS", CurlPortMeasurementCase.SurlBaseUrlPlaceholder + path],
            "dict",
            [],
            ServedWords,
            run => run.ExitCode == expectedExitCode && run.StandardOutput.AsSpan().SequenceEqual(expectedOutput)
                ? null
                : $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}");

        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase);

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, name, measurement);
    }
}
