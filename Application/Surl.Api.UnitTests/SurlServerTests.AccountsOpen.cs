namespace Surl.Api;

// BL-1391: removing the last account leaves the server needing no login, which is allowed and warned.
public sealed partial class SurlServerTests
{
    private const string NoAccountLeftWarning = "surl: warning: no account left; the server now needs no login";

    [TestMethod]
    public async Task AccountsRemove_TheLastAccount_WarnsInTheLogAndHasAccountsTurnsFalse()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = StoreOptions(Pop3);
        options.Log = log;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        var isRemoved = server.Accounts.Remove("alice");

        Assert.IsTrue(isRemoved);
        Assert.IsFalse(server.Accounts.HasAccounts);
        StringAssert.Contains(log.ToString(), NoAccountLeftWarning);
    }

    [TestMethod]
    public async Task AccountsRemove_AnAccountWhileOthersRemain_OrNone_DoesNotWarn()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = StoreOptions(Pop3);
        options.Log = log;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        server.Accounts.Add("bob", "builder");

        var removedBob = server.Accounts.Remove("bob");
        var removedNobody = server.Accounts.Remove("nobody");

        Assert.IsTrue(removedBob);
        Assert.IsFalse(removedNobody);
        Assert.IsTrue(server.Accounts.HasAccounts);
        Assert.DoesNotContain("no account left", log.ToString());
    }
}
