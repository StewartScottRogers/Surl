using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port client's <c>socketio</c> extension connects to a live <c>surl socketio://</c>
/// and <c>socketios://</c> (ADR-0083 decisions 1 to 3, BL-456). Every assertion is a rule of the
/// Engine.IO 4 and Socket.IO 5 specifications as ADR-0120 decides them, listed in
/// <c>Documentation/Conformance/Extended/SocketIo.md</c>; what the client prints is never an expected
/// value beyond the bytes the server was required to send back. Inconclusive when no pinned client is
/// installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class PinnedCurlPortClientTalksToSurlOverSocketIoTests
{
    private const string EchoedEvent = "[\"hello\",1]";

    /// <summary>Ends the session from the server's side, so the client, which reads until the server disconnects, exits.</summary>
    private const string EndSessionAfter = "3";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("SocketIo:SIO-Connect-2, SocketIo:Surl-5-1, SocketIo:Surl-7-5: the client joins the main namespace, emits an event and receives its echo byte for byte; the session ends with the server's DISCONNECT")]
    public async Task Emit_MainNamespace_IsEchoedByteForByte()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "socketio", ["--allow-anonymous", "--max-time", EndSessionAfter], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-d", EchoedEvent, Url(surl, "socketio", "/"));

        AssertEchoed(result, surl);
    }

    [TestMethod]
    [Description("SocketIo:Surl-4-1: a listen URL has no path, so only the main namespace is served; a client joining /chat gets CONNECT_ERROR 'Invalid namespace', no echo and a non-zero exit")]
    public async Task Connect_NamespaceThatIsNotServed_IsRefusedAndNothingIsEchoed()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "socketio", ["--allow-anonymous", "--max-time", EndSessionAfter], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-d", EchoedEvent, Url(surl, "socketio", "/chat"));

        Assert.AreNotEqual(0, result.ExitCode, $"{result}; stderr: {result.StandardError}; surl: {surl.Log}");
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), $"stderr: {result.StandardError}");
    }

    [TestMethod]
    [Description("SocketIo:Surl-0-1, SocketIo:Surl-5-1: socketios is the same server inside TLS, and the client's echo comes back over it")]
    public async Task Emit_OverTls_IsEchoedByteForByte()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "socketios", ["--allow-anonymous", "--self-signed", "--max-time", EndSessionAfter], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-d", EchoedEvent, Url(surl, "socketios", "/"));

        AssertEchoed(result, surl);
    }

    [TestMethod]
    [Description("SocketIo:SIO-Connect-3, SocketIo:Surl-4-2: without --allow-anonymous a CONNECT with no credentials is refused with CONNECT_ERROR, so the client does not get its echo and does not exit 0")]
    public async Task Connect_WithoutCredentials_IsRefusedAndNothingIsEchoed()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "socketio", ["--max-time", EndSessionAfter], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-d", EchoedEvent, Url(surl, "socketio", "/"));

        Assert.AreNotEqual(0, result.ExitCode, $"{result}; stderr: {result.StandardError}; surl: {surl.Log}");
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), $"stderr: {result.StandardError}");
    }

    private static string Url(SurlOnLoopback surl, string scheme, string path) =>
        $"{scheme}://127.0.0.1:{surl.BaseUrl.Port}{path}";

    private static void AssertEchoed(CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(EchoedEvent, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }
}
