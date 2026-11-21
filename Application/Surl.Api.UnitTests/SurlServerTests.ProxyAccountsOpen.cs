namespace Surl.Api;

// BL-1478: removing the last proxy account leaves the proxy needing no login, which is allowed and warned.
public sealed partial class SurlServerTests
{
    private const string NoProxyAccountLeftWarning = "surl: warning: no proxy account left; the proxy now needs no login";

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
