using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port client's <c>webtransport</c> extension connects to a live <c>surl webtransport://</c>
/// (ADR-0083 decisions 1 to 3, BL-462). Every assertion is a rule of WebTransport over HTTP/3
/// (draft-ietf-webtrans-http3-16) as ADR-0117 decides it, listed in
/// <c>Documentation/Conformance/Extended/WebTransport.md</c>; what the client prints is never an expected value
/// beyond the echo the server was required to send. Inconclusive when no pinned client is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class PinnedCurlPortClientTalksToSurlOverWebTransportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("WebTransport:WTH3-3.2-2, WebTransport:Surl-3-1: data sent on a bidirectional stream is echoed on it byte for byte")]
    public async Task Send_OnABidirectionalStream_IsEchoedOnItByteForByte()
    {
        await using var surl = await StartAsync("--allow-anonymous");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-d", "hello bidi", Url(surl, string.Empty));

        Assert.AreEqual(0, result.ExitCode, Diagnostics(result, surl));
        Assert.AreEqual("hello bidi", Output(result), Diagnostics(result, surl));
    }

    [TestMethod]
    [Description("WebTransport:WTH3-4.2-1, WebTransport:Surl-3-2: data sent on a unidirectional stream is answered on a server unidirectional stream byte for byte")]
    public async Task Send_OnAUnidirectionalStream_IsAnsweredOnAServerUnidirectionalStreamByteForByte()
    {
        await using var surl = await StartAsync("--allow-anonymous");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-d", "hello uni", Url(surl, "?wt=uni"));

        Assert.AreEqual(0, result.ExitCode, Diagnostics(result, surl));
        Assert.AreEqual("hello uni", Output(result), Diagnostics(result, surl));
    }

    [TestMethod]
    [Description("WebTransport:WTH3-4.5-1: a datagram is echoed as a datagram with the same payload")]
    public async Task Send_AsADatagram_IsEchoedAsADatagramWithTheSamePayload()
    {
        await using var surl = await StartAsync("--allow-anonymous");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-d", "hello datagram", Url(surl, "?wt=datagram"));

        Assert.AreEqual(0, result.ExitCode, Diagnostics(result, surl));
        Assert.AreEqual("hello datagram", Output(result), Diagnostics(result, surl));
    }

    [TestMethod]
    [Description("WebTransport:Surl-7-1: with an account set, a CONNECT without credentials is refused 401, so nothing is echoed (the client's exit code is not asserted: it exits 0 on the refusal)")]
    public async Task Connect_WithoutCredentials_EchoesNothing()
    {
        await using var surl = await StartAsync("--user", "alice:secret");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-d", "hello refused", Url(surl, string.Empty));

        Assert.DoesNotContain("hello refused", Output(result), Diagnostics(result, surl));
    }

    private Task<SurlOnLoopback> StartAsync(params string[] extraOptions) =>
        SurlOnLoopback.StartAsync("webtransport", new Dictionary<string, byte[]>(), [], ["--self-signed", .. extraOptions], TestContext.CancellationToken);

    private static string Url(SurlOnLoopback surl, string query) =>
        $"webtransport://127.0.0.1:{surl.BaseUrl.Port}/{query}";

    private static string Output(CurlPortClientRunResult result) =>
        Encoding.UTF8.GetString(result.StandardOutput.ToArray());

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
}
