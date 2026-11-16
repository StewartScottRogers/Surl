using System.Text;
using Surl.Cryptography.Srp;
using Surl.Networking;

namespace Surl.Api;

// BL-1280: adding and removing proxy and TLS-SRP accounts on a running server (ADR-0170, decision 4).
public sealed partial class SurlServerTests
{
    private static string ConnectWithProxyLogin(string userColonPassword) =>
        "CONNECT 127.0.0.1:1 HTTP/1.1\r\nHost: 127.0.0.1:1\r\n"
        + $"Proxy-Authorization: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(userColonPassword))}\r\n\r\n";

    private static SurlServerOptions ProxyOptions()
    {
        var options = ApiTestSeams.Options(Pop3);
        options.AllowPlaintextAuthentication = true;
        options.AdditionalArguments.Add("-x");
        options.AdditionalArguments.Add("http://127.0.0.1:0/");
        options.AdditionalArguments.Add("-U");
        options.AdditionalArguments.Add("alice:secret");
        return options;
    }

    [TestMethod]
    public async Task ProxyAccounts_AddThenRemove_TheAccountLogsInThroughTheProxyOnlyWhileItIsThere()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ProxyOptions(), seams.Host, TestContext.CancellationToken);

        var before = await ServeAsync(seams, "http", ConnectWithProxyLogin("bob:builder"));
        server.ProxyAccounts.Add("bob", "builder");
        var added = await ServeAsync(seams, "http", ConnectWithProxyLogin("bob:builder"));
        var removed = server.ProxyAccounts.Remove("bob");
        var after = await ServeAsync(seams, "http", ConnectWithProxyLogin("bob:builder"));

        StringAssert.StartsWith(before, "HTTP/1.1 407 ");
        Assert.IsFalse(added.StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), added);
        Assert.IsTrue(removed);
        StringAssert.StartsWith(after, "HTTP/1.1 407 ");
        Assert.IsFalse(server.ProxyAccounts.Remove("bob"));
    }

    [TestMethod]
    public async Task ProxyAccounts_Misused_Throws()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(ProxyOptions(), seams.Host, TestContext.CancellationToken);
        await using var anonymous = await SurlServer.StartAsync(StoreOptions(Http), new ApiTestSeams().Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => server.ProxyAccounts.Add(null!, "pw"));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ProxyAccounts.Add("bob", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ProxyAccounts.Remove(null!));
        Assert.ThrowsExactly<ArgumentException>(() => server.ProxyAccounts.Add("a:b", "pw"));
        Assert.ThrowsExactly<ArgumentException>(() => server.ProxyAccounts.Add("alice", "again"));
        Assert.ThrowsExactly<InvalidOperationException>(() => anonymous.ProxyAccounts.Add("bob", "pw"));
    }

    [TestMethod]
    public async Task TlsSrpAccounts_AddThenRemove_TheHandshakeVerifiesOnlyWhileTheAccountIsThere()
    {
        ServerTlsSettings? settings = null;
        var seams = new ApiTestSeams();
        var host = seams.Host with { CreateListenerFactory = (tls, _) => { settings = tls; return seams.ListenerFactory; } };
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.AdditionalArguments.Add("--tlsuser");
        options.AdditionalArguments.Add("jsmith");
        options.AdditionalArguments.Add("--tlspassword");
        options.AdditionalArguments.Add("abc");
        await using var server = await SurlServer.StartAsync(options, host, TestContext.CancellationToken);
        var accounts = settings?.TlsSrpAccounts ?? throw new AssertFailedException("No TLS-SRP accounts were composed.");
        var name = "carol"u8.ToArray();
        using var random = System.Security.Cryptography.RandomNumberGenerator.Create();

        var before = accounts.Find(name, random);
        server.TlsSrpAccounts.Add("carol", "pw");
        var added = accounts.Find(name, random);
        var removed = server.TlsSrpAccounts.Remove("carol");
        var after = accounts.Find(name, random);

        Assert.IsTrue(before.IsSimulated);
        Assert.IsFalse(added.IsSimulated);
        var expected = Srp6a.ToUnsignedBigEndian(Srp6a.ComputeVerifier(added.Group, added.Salt, name, "pw"u8));
        CollectionAssert.AreEqual(expected, added.Verifier);
        Assert.IsTrue(removed);
        Assert.IsTrue(after.IsSimulated);
        Assert.IsFalse(server.TlsSrpAccounts.Remove("carol"));
        Assert.IsFalse(accounts.Find("jsmith"u8.ToArray(), random).IsSimulated);
    }

    [TestMethod]
    public async Task TlsSrpAccounts_Misused_Throws()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Http), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => server.TlsSrpAccounts.Add(null!, "pw"));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.TlsSrpAccounts.Add("bob", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.TlsSrpAccounts.Remove(null!));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.TlsSrpAccounts.Add("bob", "pw"));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.TlsSrpAccounts.Remove("bob"));
    }
}
