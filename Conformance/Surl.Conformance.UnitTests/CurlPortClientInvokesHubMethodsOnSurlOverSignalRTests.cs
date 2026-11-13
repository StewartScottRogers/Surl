using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>signalr</c> client against a live <c>surl signalr://</c> and <c>signalrs://</c>
/// (BL-451, ADR-0083 decisions 1 to 3): an <c>Echo</c> invocation, a <c>Fail</c> invocation and a negotiate refused
/// for want of a login. Each expected value comes from HubProtocol.md and TransportProtocols.md as
/// <c>Documentation/Conformance/Extended/SignalR.md</c> lists them - <c>Echo</c> completes with its argument,
/// <c>Fail</c> completes with its fixed error, and a negotiate with no credentials is <c>401</c> - never from what
/// the port printed beyond the bytes the server was required to send. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientInvokesHubMethodsOnSurlOverSignalRTests
{
    private const string EchoCall = """{"type":1,"target":"Echo","arguments":[{"greeting":"hello","n":1}]}""";
    private const string EchoedResult = """{"greeting":"hello","n":1}""";
    private const string FailCall = """{"type":1,"target":"Fail","arguments":[]}""";
    private const string FailError = "An unexpected error occurred invoking 'Fail' on the server.";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("SignalR:TP-Negotiate-1, SignalR:HUB-Handshake-1, SignalR:HUB-Invocation-1: the client negotiates, handshakes and invokes Echo, and gets the argument back as the Completion's result byte for byte")]
    public async Task Echo_OverPlainText_CompletesWithItsArgument()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("signalr", ["--allow-anonymous"], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-d", EchoCall, Url(surl, "signalr"));

        AssertEchoed(result, surl);
    }

    [TestMethod]
    [Description("SignalR:Surl-0-1, SignalR:HUB-Invocation-1: signalrs is the same server inside TLS from the first byte, and Echo completes over it")]
    public async Task Echo_OverTls_CompletesWithItsArgument()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "signalrs", ["--allow-anonymous", "--self-signed"], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-k", "-d", EchoCall, Url(surl, "signalrs"));

        AssertEchoed(result, surl);
    }

    [TestMethod]
    [Description("SignalR:HUB-Errors-2: Fail completes its invocation with the fixed error text and no result, so the client prints no result and does not exit 0")]
    public async Task Fail_CompletesWithTheFixedErrorAndNoResult()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("signalr", ["--allow-anonymous"], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-d", FailCall, Url(surl, "signalr"));

        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreNotEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
        StringAssert.Contains(result.StandardError, FailError, diagnostics);
    }

    [TestMethod]
    [Description("SignalR:Surl-5-1: with an account configured a negotiate with no credentials is 401, and the client, which reaches no hub, prints no result")]
    public async Task Negotiate_WithoutCredentials_IsRefusedAndNothingIsEchoed()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("signalr", ["--user", "alice:secret"], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-d", EchoCall, Url(surl, "signalr"));

        // The port client exits 0 on this refusal (a port defect, BL-451 Notes), so its exit code is no evidence;
        // the 401 is asserted on the wire and the absence of a result on the client's output.
        using var http = new HttpClient();
        using var negotiate = await http.PostAsync(
            $"http://127.0.0.1:{surl.BaseUrl.Port}/negotiate?negotiateVersion=1", new ByteArrayContent([]), TestContext.CancellationToken);
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, negotiate.StatusCode, diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    [TestMethod]
    [Description("SignalR:Surl-5-1: with no account and no --allow-anonymous a negotiate with no credentials is 401 (BL-1091)")]
    public async Task Negotiate_WithoutCredentialsAndNoAccount_IsRefused()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("signalr", [], TestContext.CancellationToken);

        using var http = new HttpClient();
        using var negotiate = await http.PostAsync(
            $"http://127.0.0.1:{surl.BaseUrl.Port}/negotiate?negotiateVersion=1", new ByteArrayContent([]), TestContext.CancellationToken);

        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, negotiate.StatusCode, surl.Log);
    }

    private static string Url(SurlOnLoopback surl, string scheme) => $"{scheme}://127.0.0.1:{surl.BaseUrl.Port}/";

    private static void AssertEchoed(CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(EchoedResult, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }
}
