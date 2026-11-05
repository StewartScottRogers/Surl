namespace Surl.Conformance;

[TestClass]
[TestCategory("Integration")]
public sealed class PinnedCurlPortBesideUpstreamCurlTests
{
    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes")]
    public async Task MeasureAsync_HelloTxtOverHttp_ReachesAVerdict()
    {
        var measurementCase = new CurlPortMeasurementCase(
            nameof(MeasureAsync_HelloTxtOverHttp_ReachesAVerdict),
            "UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes",
            "http",
            ["-s", "{surl}/hello.txt"],
            "http",
            [],
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello },
            upstream => upstream.ExitCode == 0 && upstream.StandardOutput.AsSpan().SequenceEqual(Hello)
                ? null
                : $"upstream exited {upstream.ExitCode} with {upstream.StandardOutput.Length} bytes; stderr: {upstream.StandardError}");

        var verdict = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase);

        Assert.AreNotEqual(CurlPortVerdictKind.SurlDefect, verdict.Kind, verdict.ToString());
        Assert.AreNotEqual(CurlPortVerdictKind.NotComparable, verdict.Kind, verdict.ToString());
    }
}
