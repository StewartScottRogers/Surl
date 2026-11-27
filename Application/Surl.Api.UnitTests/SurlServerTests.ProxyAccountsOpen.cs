namespace Surl.Api;

// BL-1478, BL-1852: removing the last proxy account is allowed and warned, and the proxy then lets no new client
// through without --allow-anonymous (ADR-0132 amendment 4).
public sealed partial class SurlServerTests
{
    private const string NoProxyAccountLeftWarning =
        "surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through";

    [TestMethod]
    public async Task ProxyAccountsRemove_TheLastAccount_WarnsInTheLogAndHasAccountsTurnsFalse()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = ProxyOptions();
        options.Log = log;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var hadAccounts = server.ProxyAccounts.HasAccounts;

        var isRemoved = server.ProxyAccounts.Remove("alice");

        Assert.IsTrue(hadAccounts);
        Assert.IsTrue(isRemoved);
        Assert.IsFalse(server.ProxyAccounts.HasAccounts);
        Assert.AreEqual(1, log.ToString().Split(NoProxyAccountLeftWarning).Length - 1);
    }

    [TestMethod]
    public async Task ProxyAccountsRemove_TheLastAccount_AnswersAGetWithoutCredentialsWith407()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ProxyOptions(), seams.Host, TestContext.CancellationToken);
        server.ProxyAccounts.Remove("alice");

        var response = await ServeAsync(
            seams, "http", "GET http://127.0.0.1:1/ HTTP/1.1\r\nHost: 127.0.0.1:1\r\n\r\n");

        Assert.IsFalse(server.ProxyAccounts.HasAccounts);
        StringAssert.StartsWith(response, "HTTP/1.1 407 ");
    }

    [TestMethod]
    public async Task ProxyAccountsRemove_AnAccountWhileOthersRemain_OrNone_DoesNotWarn()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = ProxyOptions();
        options.Log = log;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        server.ProxyAccounts.Add("bob", "builder");

        var removedBob = server.ProxyAccounts.Remove("bob");
        var removedNobody = server.ProxyAccounts.Remove("nobody");

        Assert.IsTrue(removedBob);
        Assert.IsFalse(removedNobody);
        Assert.IsTrue(server.ProxyAccounts.HasAccounts);
        Assert.DoesNotContain("no proxy account left", log.ToString());
    }
}
