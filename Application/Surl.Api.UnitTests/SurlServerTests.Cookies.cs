using System.Text;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1675: SurlServerOptions.Cookies is --set-cookie, once per item (ADR-0189), and the cookies a client sends
// back are read as CookieReceived events.
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task StartAsync_HttpWithOneCookie_SendsItsSetCookieLine()
    {
        var reply = await ReplyToOneGetWithCookiesAsync("session=t1;Path=/;HttpOnly");

        StringAssert.StartsWith(reply, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(reply, "\r\nSet-Cookie: session=t1; Path=/; HttpOnly\r\n");
    }

    [TestMethod]
    public async Task StartAsync_HttpWithNoCookie_SendsNoSetCookie()
    {
        var reply = await ReplyToOneGetWithCookiesAsync();

        StringAssert.StartsWith(reply, "HTTP/1.1 200 OK\r\n");
        Assert.DoesNotContain("Set-Cookie", reply);
    }

    [TestMethod]
    public async Task StartAsync_ABadCookie_ThrowsWithTheExitCodeAndTextSurlWritesForSetCookie()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.Cookies.Add("noequals");
        var expected = CommandLineParser.Parse([Http, "--set-cookie", "noequals"]).Failure!;

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
        Assert.AreEqual(expected.Message, exception.Lines[0]);
        StringAssert.Contains(exception.Lines[0], "option --set-cookie: the cookie needs a name=value pair");
        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task WaitForEventAsync_AClientSendsACookieBack_GivesItsNameAndValue()
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(
            "GET /hello.txt HTTP/1.1\r\nHost: x\r\nCookie: session=t1\r\nConnection: close\r\n\r\n"));
        var seams = new ApiTestSeams(new FakeListenerFactory { ConnectionToAccept = connection });
        var options = ApiTestSeams.Options(Http);
        options.RecordExchanges = true;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var received = (CookieReceivedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.CookieReceived, Name = "session" },
            TimeSpan.FromSeconds(30),
            TestContext.CancellationToken);

        Assert.AreEqual("t1", received.Value);
    }

    private async Task<string> ReplyToOneGetWithCookiesAsync(params string[] cookies)
    {
        var connection = new FakeConnection(CurlGetHello);
        var seams = new ApiTestSeams(new FakeListenerFactory { ConnectionToAccept = connection });
        var options = ApiTestSeams.Options(Http);
        options.Files["hello.txt"] = "hello"u8.ToArray();
        foreach (var cookie in cookies)
        {
            options.Cookies.Add(cookie);
        }

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        return Encoding.ASCII.GetString(connection.WrittenToClient);
    }
}
