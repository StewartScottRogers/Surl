using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1176: the loopback control endpoint through SurlServerOptions.ControlListenUrl and ControlTokenFile.
public sealed partial class SurlServerTests
{
    private const string ControlToken = "0123456789abcdef0123456789abcdef";

    private static void GiveTokenFile(ApiTestSeams seams, string path = "token.txt") =>
        seams.StartFiles[path] = Encoding.ASCII.GetBytes(ControlToken + "\n");

    [TestMethod]
    public async Task StartAsync_ControlListenUrlAndATokenFile_ReportsTheBoundControlUrlAndAnswersEvents()
    {
        var request = new FakeConnection(Encoding.ASCII.GetBytes(
            $"GET /v1/events HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {ControlToken}\r\n\r\n"));
        var seams = new ApiTestSeams();
        GiveTokenFile(seams);
        seams.ListenerFactory.Accept("http", request);
        var options = ApiTestSeams.Options("ftp://127.0.0.1:0");
        options.ControlListenUrl = "http://127.0.0.1:0";
        options.ControlTokenFile = "token.txt";

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await request.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(new Uri($"http://127.0.0.1:{FakeListenerFactory.BoundPort}/"), server.ControlUrl);
        StringAssert.StartsWith(Encoding.ASCII.GetString(request.WrittenToClient), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task StartAsync_WithoutControlOptions_ControlUrlIsNull()
    {
        var seams = new ApiTestSeams();

        await using var server = await SurlServer.StartAsync(ApiTestSeams.Options(Http), seams.Host, TestContext.CancellationToken);

        Assert.IsNull(server.ControlUrl);
    }

    [TestMethod]
    [DataRow("http://0.0.0.0:0", "token.txt", "(2) --control must listen on loopback (127.0.0.0/8, ::1 or localhost): http://0.0.0.0:0")]
    [DataRow("ftp://127.0.0.1:0", "token.txt", "(2) --control needs http, not ftp")]
    [DataRow("http://127.0.0.1:0", null, "(2) --control needs --control-token-file")]
    [DataRow(null, "token.txt", "(2) --control-token-file without --control")]
    public async Task StartAsync_ARefusedControlCombination_ThrowsWithSurlsMessage(string? listenUrl, string? tokenFile, string message)
    {
        var seams = new ApiTestSeams();
        GiveTokenFile(seams);
        var options = ApiTestSeams.Options(Http);
        options.ControlListenUrl = listenUrl;
        options.ControlTokenFile = tokenFile;

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.FailedInit, exception.ExitCode);
        Assert.AreEqual(message, exception.Lines[0]);
    }
}
