using System.Text;
using Surl.Cli;

namespace Surl.Api;

// BL-668: mail domains, mailboxes, shares, share access, topics and topic access go through Surl.Composition's
// start as the same words do on surl's command line, so a refusal is surl's.
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task StartAsync_AMailDomainAndAMailbox_StartsAndTakesMailForTheMailbox()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("smtp://127.0.0.1:0");
        options.MailDomains.Add("example.com");
        options.Mailboxes.Add("alice@example.com:secret");

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await server.Mail.DeliverAsync("alice@example.com", Encoding.ASCII.GetBytes("Subject: hi\r\n\r\nhello\r\n"), TestContext.CancellationToken);

        Assert.AreEqual(1, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_AShareWithAccessAndATopicWithAGrant_Starts()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0", "mqtt://127.0.0.1:0");
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.Shares.Add("docs=docs");
        options.ShareAccess.Add("docs=alice:rw");
        options.Topics.Add("sensors/#");
        options.TopicAccess.Add("sensors/#=alice:pubsub");

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.AreEqual(2, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_AMalformedShare_ThrowsWithTheExitCodeAndTextSurlWrites()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.Shares.Add("no-equals-sign");
        var expected = CommandLineParser.Parse([Http, "--share", "no-equals-sign"]).Failure!;

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
        Assert.AreEqual(expected.Message, exception.Lines[0]);
        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_AConfigFileInMemory_IsReadThroughTheStartFileSeam()
    {
        var seams = new ApiTestSeams();
        seams.StartFiles["server.conf"] = Encoding.UTF8.GetBytes("mail-domain = \"example.com\"\nmailbox = \"bob@example.com:pw\"\n");
        var options = ApiTestSeams.Options("smtp://127.0.0.1:0");
        options.ConfigFiles.Add("server.conf");

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await server.Mail.DeliverAsync("bob@example.com", Encoding.ASCII.GetBytes("Subject: x\r\n\r\ny\r\n"), TestContext.CancellationToken);

        Assert.AreEqual(1, seams.ListenerFactory.OpenListenerCount);
    }
}
