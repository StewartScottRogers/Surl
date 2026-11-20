using System.Text;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task StartAsync_OnlyAConfigFileNamingTheListenUrl_BindsItAndListsItInBoundUrls()
    {
        var seams = new ApiTestSeams();
        seams.StartFiles["fixture.conf"] = Encoding.UTF8.GetBytes("url http://127.0.0.1:0/\n");
        var options = new SurlServerOptions();
        options.ConfigFiles.Add("fixture.conf");

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { new Uri($"http://127.0.0.1:{FakeListenerFactory.BoundPort}/") }, server.BoundUrls.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_OnlyAConfigFileNamingNoUrl_ThrowsStartExceptionWithNoUrlSpecified()
    {
        var seams = new ApiTestSeams();
        seams.StartFiles["empty.conf"] = Encoding.UTF8.GetBytes("# nothing\n");
        var options = new SurlServerOptions();
        options.ConfigFiles.Add("empty.conf");

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.FailedInit, exception.ExitCode);
        Assert.AreEqual("(2) no URL specified", exception.Lines[0]);
    }

    [TestMethod]
    public async Task StartAsync_NothingThatCouldNameAListenUrl_StillThrowsArgument()
    {
        var seams = new ApiTestSeams();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(new SurlServerOptions(), seams.Host, TestContext.CancellationToken));
    }
}
