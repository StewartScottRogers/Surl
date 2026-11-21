using Surl.Cryptography.Srp;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1491: SetPassword on the account book and the TLS-SRP accounts (ADR-0132 amendment 1).
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task AccountsSetPassword_AKnownUser_TheNewPasswordLogsInTheOldOneDoesNotAndPublishesRemovedThenAdded()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = StoreOptions(Http);
        options.RecordExchanges = true;
        options.Log = log;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        server.Accounts.Add("bob", "builder");

        server.Accounts.SetPassword("bob", "changed");

        var oldPassword = await ServeAsync(seams, "http", HttpGet("/hello.txt", "bob:builder"));
        var newPassword = await ServeAsync(seams, "http", HttpGet("/hello.txt", "bob:changed"));
        var changes = server.FindEvents(new SurlEventQuery { Store = StoreKind.Accounts, Name = "bob" })
            .OfType<StoreChangedEvent>().Select(change => change.Change).ToArray();
        StringAssert.StartsWith(oldPassword, "HTTP/1.1 401 ");
        StringAssert.StartsWith(newPassword, "HTTP/1.1 200 OK\r\n");
        CollectionAssert.AreEqual(
            new[] { StoreChange.AccountAdded, StoreChange.AccountRemoved, StoreChange.AccountAdded }, changes);
        Assert.IsTrue(server.Accounts.HasAccounts);
        Assert.DoesNotContain("no account left", log.ToString());
    }

    [TestMethod]
    public async Task AccountsSetPassword_Misused_ThrowsAndChangesNothing()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Http), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentException>(() => server.Accounts.SetPassword("nobody", "pw"));
        Assert.ThrowsExactly<ArgumentException>(() => server.Accounts.SetPassword("alice", string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.Accounts.SetPassword(null!, "pw"));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.Accounts.SetPassword("alice", null!));
        Assert.IsTrue(server.Accounts.HasAccounts);
    }

    [TestMethod]
    public async Task TlsSrpAccountsSetPassword_AKnownUser_TheVerifierFollowsTheNewPasswordAndPublishesRemovedThenAdded()
    {
        ServerTlsSettings? settings = null;
        var seams = new ApiTestSeams();
        var host = seams.Host with { CreateListenerFactory = (tls, _) => { settings = tls; return seams.ListenerFactory; } };
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.RecordExchanges = true;
        options.AdditionalArguments.Add("--tlsuser");
        options.AdditionalArguments.Add("jsmith");
        options.AdditionalArguments.Add("--tlspassword");
        options.AdditionalArguments.Add("abc");
        await using var server = await SurlServer.StartAsync(options, host, TestContext.CancellationToken);
        var accounts = settings?.TlsSrpAccounts ?? throw new AssertFailedException("No TLS-SRP accounts were composed.");
        var name = "jsmith"u8.ToArray();
        using var random = System.Security.Cryptography.RandomNumberGenerator.Create();

        server.TlsSrpAccounts.SetPassword("jsmith", "changed");

        var found = accounts.Find(name, random);
        Assert.IsFalse(found.IsSimulated);
        var expected = Srp6a.ToUnsignedBigEndian(Srp6a.ComputeVerifier(found.Group, found.Salt, name, "changed"u8));
        CollectionAssert.AreEqual(expected, found.Verifier);
        var changes = server.FindEvents(new SurlEventQuery { Store = StoreKind.TlsSrpAccounts, Name = "jsmith" })
            .OfType<StoreChangedEvent>().Select(change => change.Change).ToArray();
        CollectionAssert.AreEqual(new[] { StoreChange.AccountRemoved, StoreChange.AccountAdded }, changes);
    }

    [TestMethod]
    public async Task TlsSrpAccountsSetPassword_Misused_Throws()
    {
        var seams = new ApiTestSeams();
        await using var plain = await SurlServer.StartAsync(StoreOptions(Http), seams.Host, TestContext.CancellationToken);
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.AdditionalArguments.Add("--tlsuser");
        options.AdditionalArguments.Add("jsmith");
        options.AdditionalArguments.Add("--tlspassword");
        options.AdditionalArguments.Add("abc");
        await using var srp = await SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => srp.TlsSrpAccounts.SetPassword(null!, "pw"));
        Assert.ThrowsExactly<ArgumentNullException>(() => srp.TlsSrpAccounts.SetPassword("jsmith", null!));
        Assert.ThrowsExactly<ArgumentException>(() => srp.TlsSrpAccounts.SetPassword("jsmith", string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => srp.TlsSrpAccounts.SetPassword("nobody", "pw"));
        Assert.ThrowsExactly<InvalidOperationException>(() => plain.TlsSrpAccounts.SetPassword("jsmith", "pw"));
    }
}
