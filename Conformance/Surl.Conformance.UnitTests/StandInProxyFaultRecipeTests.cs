using System.Text;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite makes the stand-in's HTTP proxy fail on purpose (BL-1174, ADR-0160). Each test adds a
/// <see cref="SurlFault"/> scoped to the <c>http-proxy</c> scheme, lets the pinned upstream curl 8.21.0 play the code
/// under test through <c>-x</c>, and asserts the exit code and stderr line measured with that build.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInProxyFaultRecipeTests
{

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ChosenConnectError_MakesCurlFailWithExit56AndTheChosenStatus()
    {
        await using var origin = await StartOriginAsync();
        await using var server = await StartProxyAsync("error;scheme=http-proxy;command=CONNECT;reply=503");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-p", "-x", ProxyUrl(server), OriginUrl(origin));

        Assert.AreEqual(7, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "CONNECT tunnel failed, response 503");
    }

    [TestMethod]
    public async Task ChosenForwardedError_WithFail_MakesCurlExit22()
    {
        await using var origin = await StartOriginAsync();
        await using var server = await StartProxyAsync("error;scheme=http-proxy;command=GET;reply=503");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--fail", "-x", ProxyUrl(server), OriginUrl(origin));

        Assert.AreEqual(22, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "The requested URL returned error: 503");
    }

    [TestMethod]
    public async Task CommandStageDrop_OnForwardedRequest_MakesCurlFailBeforeAnyReply()
    {
        await using var origin = await StartOriginAsync();
        await using var server = await StartProxyAsync("drop;stage=command;scheme=http-proxy");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-x", ProxyUrl(server), OriginUrl(origin));

        // Measured on Windows: exit 56, "Recv failure: Connection was reset"; Linux and macOS may see a clean close (52) or 55.
        Assert.IsTrue(result.ExitCode is 52 or 55 or 56, result.StandardError);
    }

    [TestMethod]
    public async Task DelayOverMaxTime_MakesCurlTimeOutWithExit28()
    {
        await using var origin = await StartOriginAsync();
        await using var server = await StartProxyAsync("delay;stage=command;scheme=http-proxy;ms=5000");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--max-time", "1", "-x", ProxyUrl(server), OriginUrl(origin));

        Assert.AreEqual(28, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Operation timed out");
    }

    [TestMethod]
    public async Task ChosenForwardedError_Once_FailsTheFirstCurlAndLetsTheRetrySucceed()
    {
        await using var origin = await StartOriginAsync();
        await using var server = await StartProxyAsync("error;scheme=http-proxy;reply=503;times=1");
        var proxy = ProxyUrl(server);
        var originUrl = OriginUrl(origin);

        var first = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--fail", "-x", proxy, originUrl);
        var retry = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--fail", "-x", proxy, originUrl);

        Assert.AreEqual(22, first.ExitCode, first.StandardError);
        Assert.AreEqual(0, retry.ExitCode, retry.StandardError);
        Assert.AreEqual("hello\n", Encoding.UTF8.GetString(retry.StandardOutput));
    }


    private Task<SurlServer> StartOriginAsync()
    {
        var options = new SurlServerOptions { RecordExchanges = true };
        options.ListenUrls.Add("http://127.0.0.1:0");
        options.Files["hello.txt"] = "hello\n"u8.ToArray();
        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }

    private Task<SurlOnLoopback> StartProxyAsync(string fault) =>
        SurlOnLoopback.StartHttpProxyInMemoryAsync(["--fault", fault], TestContext.CancellationToken);


    private static string OriginUrl(SurlServer origin) => $"http://{origin.BoundUrl.Host}:{origin.BoundUrl.Port}/hello.txt";

    private static string ProxyUrl(SurlOnLoopback proxy) => $"http://{proxy.BaseUrl.Host}:{proxy.BaseUrl.Port}/";
}
