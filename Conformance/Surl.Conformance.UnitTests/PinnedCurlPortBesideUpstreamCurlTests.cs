using System.Text;

namespace Surl.Conformance;

[TestClass]
[TestCategory("Integration")]
public sealed class PinnedCurlPortBesideUpstreamCurlTests
{
    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void InExchangeOrder_InterleavedExchanges_GroupsEachExchangeKeepingItsOwnOrder()
    {
        var notes = "surl: warning: w\n#2 * Exchange 2 opened.\n#1 * Exchange 1 opened.\n#2 * Exchange 2 closed.\n#1 * Exchange 1 closed.\n";

        var ordered = PinnedCurlPortBesideUpstreamCurl.InExchangeOrder(notes);

        Assert.AreEqual(
            "surl: warning: w\n#1 * Exchange 1 opened.\n#1 * Exchange 1 closed.\n#2 * Exchange 2 opened.\n#2 * Exchange 2 closed.\n", ordered);
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes")]
    public async Task MeasureAsync_HelloTxtOverHttp_ReachesAVerdictComparingRequestBytes()
    {
        var measurementCase = new CurlPortMeasurementCase(
            nameof(MeasureAsync_HelloTxtOverHttp_ReachesAVerdictComparingRequestBytes),
            "UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes",
            "http",
            ["-s", "{surl}/hello.txt"],
            "http",
            [],
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello },
            upstream => upstream.ExitCode == 0 && upstream.StandardOutput.AsSpan().SequenceEqual(Hello)
                ? null
                : $"upstream exited {upstream.ExitCode} with {upstream.StandardOutput.Length} bytes; stderr: {upstream.StandardError}");

        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase);

        Assert.AreNotEqual(CurlPortVerdictKind.SurlDefect, measurement.Verdict.Kind, measurement.Verdict.ToString());
        Assert.AreNotEqual(CurlPortVerdictKind.NotComparable, measurement.Verdict.Kind, measurement.Verdict.ToString());
        Assert.HasCount(1, measurement.UpstreamSurl.ClientRequestBytes!);
        Assert.IsNotNull(measurement.PortSurl.ClientRequestBytes);
        Assert.AreEqual(
            "GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1:<port>\r\nUser-Agent: <user-agent>\r\nAccept: */*\r\n\r\n",
            Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseRequestBytes(
                measurement.UpstreamSurl.ClientRequestBytes![0], measurement.UpstreamSurl.ClientFacingPort)));
    }
}
