using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1505: SetPassword on the proxy accounts (ADR-0132 amendment 1).
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task ProxyAccountsSetPassword_AKnownUser_TheNewPasswordLogsInTheOldOneDoesNotAndOthersStay()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ProxyOptions(), seams.Host, TestContext.CancellationToken);
        server.ProxyAccounts.Add("bob", "builder");

        server.ProxyAccounts.SetPassword("alice", "changed");

        var oldPassword = await ServeAsync(seams, "http", ConnectWithProxyLogin("alice:secret"));
        var newPassword = await ServeAsync(seams, "http", ConnectWithProxyLogin("alice:changed"));
        var other = await ServeAsync(seams, "http", ConnectWithProxyLogin("bob:builder"));
        StringAssert.StartsWith(oldPassword, "HTTP/1.1 407 ");
        Assert.IsFalse(newPassword.StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), newPassword);
        Assert.IsFalse(other.StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), other);
    }

    [TestMethod]
    public async Task ProxyAccountsSetPassword_AKnownUser_PublishesRemovedThenAddedAndNeverWarns()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = ProxyOptions();
        options.RecordExchanges = true;
        options.Log = log;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        server.ProxyAccounts.SetPassword("alice", "changed");

        var added = await server.WaitForEventAsync(
            new SurlEventQuery { Store = StoreKind.ProxyAccounts, Name = "alice" }, TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var changes = server.FindEvents(new SurlEventQuery { Store = StoreKind.ProxyAccounts })
            .OfType<StoreChangedEvent>().Select(change => change.Change).ToArray();
        Assert.IsNotNull(added);
        CollectionAssert.AreEqual(new[] { StoreChange.AccountRemoved, StoreChange.AccountAdded }, changes);
        Assert.DoesNotContain("no proxy account left", log.ToString());
        Assert.IsTrue(server.ProxyAccounts.HasAccounts);
    }

    [TestMethod]
    public async Task ProxyAccountsSetPassword_AnUnknownUser_IsRefusedAndChangesNothing()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ProxyOptions(), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentException>(() => server.ProxyAccounts.SetPassword("nobody", "pw"));

        var stillWorks = await ServeAsync(seams, "http", ConnectWithProxyLogin("alice:secret"));
        Assert.IsFalse(stillWorks.StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), stillWorks);
    }

    [TestMethod]
    public async Task ProxyAccountsSetPassword_AnEmptyPasswordOrNulls_AreRefusedAndTheOldPasswordStays()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ProxyOptions(), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentException>(() => server.ProxyAccounts.SetPassword("alice", string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ProxyAccounts.SetPassword(null!, "pw"));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ProxyAccounts.SetPassword("alice", null!));

        var stillWorks = await ServeAsync(seams, "http", ConnectWithProxyLogin("alice:secret"));
        Assert.IsFalse(stillWorks.StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), stillWorks);
    }

    [TestMethod]
    public async Task ProxyAccountsSetPassword_ServerWithoutProxyAccounts_Throws()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ApiTestSeams.Options(Pop3), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<InvalidOperationException>(() => server.ProxyAccounts.SetPassword("alice", "pw"));
    }
}
