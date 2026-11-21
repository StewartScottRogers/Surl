using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1281: add_proxy_account, remove_proxy_account, add_tls_srp_account and remove_tls_srp_account (ADR-0170 decision 4).
[TestClass]
public sealed class ProxyAndTlsSrpAccountToolsTests
{
    private static readonly TimeSpan ExchangeTimeLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    private static string ConnectWithProxyLogin(string userColonPassword) =>
        "CONNECT 127.0.0.1:1 HTTP/1.1\r\nHost: 127.0.0.1:1\r\n"
        + $"Proxy-Authorization: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(userColonPassword))}\r\n\r\n";

    [TestMethod]
    public async Task ProxyAccount_AddThenRemove_TheLoginWorksOnlyWhileTheAccountIsThereAndThePasswordIsNeverEchoed()
    {
        await using var host = await StartProxyHostAsync();

        var added = await CallAsync(host.AddProxyAccount, """{"server_id": "s1", "user": "bob", "password": "pa55-w0rd"}""");
        var whileThere = await ServeAsync(host, ConnectWithProxyLogin("bob:pa55-w0rd"));
        var removed = await CallAsync(host.RemoveProxyAccount, """{"server_id": "s1", "user": "bob"}""");
        var after = await ServeAsync(host, ConnectWithProxyLogin("bob:pa55-w0rd"));

        StoreToolsTests.AssertResult(added, """{"user":"bob"}""");
        Assert.DoesNotContain("pa55-w0rd", added.TextContent[0]);
        Assert.IsFalse(whileThere.StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), whileThere);
        StoreToolsTests.AssertResult(removed, """{"user":"bob"}""");
        StringAssert.StartsWith(after, "HTTP/1.1 407 ");
    }

    [TestMethod]
    public async Task RemoveProxyAccount_TheLastAccount_AnswersSuccessWithAWarningThatTheProxyNeedsNoLogin()
    {
        await using var host = await StartProxyHostAsync();

        var result = await CallAsync(host.RemoveProxyAccount, """{"server_id": "s1", "user": "alice"}""");

        Assert.IsFalse(result.IsError);
        Assert.HasCount(2, result.TextContent);
        Assert.AreEqual("server s1 has no proxy account left and its proxy now needs no login", result.TextContent[1]);
        StringAssert.Contains(result.TextContent[0], "\"warning\"");
    }

    [TestMethod]
    public async Task ProxyAccount_Refusals_NameTheServerAndUser()
    {
        await using var host = await StartProxyHostAsync();

        var colon = await CallAsync(host.AddProxyAccount, """{"server_id": "s1", "user": "a:b", "password": "pw"}""");
        var taken = await CallAsync(host.AddProxyAccount, """{"server_id": "s1", "user": "alice", "password": "pw"}""");
        var unknown = await CallAsync(host.RemoveProxyAccount, """{"server_id": "s1", "user": "nobody"}""");

        StoreToolsTests.AssertFailure(colon, "user a:b cannot be added: it holds ':' or already has a proxy account");
        StoreToolsTests.AssertFailure(taken, "user alice cannot be added: it holds ':' or already has a proxy account");
        StoreToolsTests.AssertFailure(unknown, "server s1 has no proxy account nobody");
    }

    [TestMethod]
    public async Task ProxyAccount_ServerWithoutProxyAccounts_IsRefused()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");
        const string Refusal = "server s1 holds no proxy accounts, so its proxies are anonymous";

        var added = await CallAsync(host.AddProxyAccount, """{"server_id": "s1", "user": "bob", "password": "pw"}""");
        var removed = await CallAsync(host.RemoveProxyAccount, """{"server_id": "s1", "user": "bob"}""");

        StoreToolsTests.AssertFailure(added, Refusal);
        StoreToolsTests.AssertFailure(removed, Refusal);
    }

    [TestMethod]
    public async Task TlsSrpAccount_AddThenRemove_AnswerTheUser()
    {
        await using var host = await StartHostAsync("https://127.0.0.1:0", "--tlsuser", "jsmith", "--tlspassword", "abc");

        var added = await CallAsync(host.AddTlsSrpAccount, """{"server_id": "s1", "user": "carol", "password": "pa55-w0rd"}""");
        var taken = await CallAsync(host.AddTlsSrpAccount, """{"server_id": "s1", "user": "carol", "password": "pw"}""");
        var removed = await CallAsync(host.RemoveTlsSrpAccount, """{"server_id": "s1", "user": "carol"}""");
        var gone = await CallAsync(host.RemoveTlsSrpAccount, """{"server_id": "s1", "user": "carol"}""");

        StoreToolsTests.AssertResult(added, """{"user":"carol"}""");
        Assert.DoesNotContain("pa55-w0rd", added.TextContent[0]);
        StoreToolsTests.AssertFailure(taken, "user carol cannot be added: it holds ':' or already has a TLS-SRP account");
        StoreToolsTests.AssertResult(removed, """{"user":"carol"}""");
        StoreToolsTests.AssertFailure(gone, "server s1 has no TLS-SRP account carol");
    }

    [TestMethod]
    public async Task TlsSrpAccount_ServerWithoutTlsSrp_IsRefused()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");
        const string Refusal = "server s1 was not started with TLS-SRP";

        var added = await CallAsync(host.AddTlsSrpAccount, """{"server_id": "s1", "user": "bob", "password": "pw"}""");
        var removed = await CallAsync(host.RemoveTlsSrpAccount, """{"server_id": "s1", "user": "bob"}""");

        StoreToolsTests.AssertFailure(added, Refusal);
        StoreToolsTests.AssertFailure(removed, Refusal);
    }

    [TestMethod]
    public async Task SetProxyAccountPassword_ChangesTheLoginAndNeverEchoesThePassword()
    {
        await using var host = await StartProxyHostAsync();

        var changed = await CallAsync(host.SetProxyAccountPassword, """{"server_id": "s1", "user": "alice", "password": "n3w-pass"}""");
        var oldLogin = await ServeAsync(host, ConnectWithProxyLogin("alice:secret"));
        var newLogin = await ServeAsync(host, ConnectWithProxyLogin("alice:n3w-pass"));
        var unknown = await CallAsync(host.SetProxyAccountPassword, """{"server_id": "s1", "user": "nobody", "password": "pw"}""");

        StoreToolsTests.AssertResult(changed, """{"user":"alice"}""");
        Assert.DoesNotContain("n3w-pass", changed.TextContent[0]);
        StringAssert.StartsWith(oldLogin, "HTTP/1.1 407 ");
        Assert.IsFalse(newLogin.StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), newLogin);
        StoreToolsTests.AssertFailure(unknown, "user nobody has no account");
    }

    [TestMethod]
    public async Task SetProxyAccountPassword_ServerWithoutProxyAccounts_IsRefused()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");

        var result = await CallAsync(host.SetProxyAccountPassword, """{"server_id": "s1", "user": "bob", "password": "pw"}""");

        StoreToolsTests.AssertFailure(result, "server s1 holds no proxy accounts, so its proxies are anonymous");
    }

    [TestMethod]
    public async Task SetTlsSrpAccountPassword_AnswersTheUserAndRefusesAnUnknownUser()
    {
        await using var host = await StartHostAsync("https://127.0.0.1:0", "--tlsuser", "jsmith", "--tlspassword", "abc");

        var changed = await CallAsync(host.SetTlsSrpAccountPassword, """{"server_id": "s1", "user": "jsmith", "password": "n3w-pass"}""");
        var unknown = await CallAsync(host.SetTlsSrpAccountPassword, """{"server_id": "s1", "user": "nobody", "password": "pw"}""");

        StoreToolsTests.AssertResult(changed, """{"user":"jsmith"}""");
        Assert.DoesNotContain("n3w-pass", changed.TextContent[0]);
        StoreToolsTests.AssertFailure(unknown, "user nobody has no account");
    }

    [TestMethod]
    public async Task SetTlsSrpAccountPassword_ServerWithoutTlsSrp_IsRefused()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");

        var result = await CallAsync(host.SetTlsSrpAccountPassword, """{"server_id": "s1", "user": "bob", "password": "pw"}""");

        StoreToolsTests.AssertFailure(result, "server s1 was not started with TLS-SRP");
    }

    private Task<McpTestHost> StartProxyHostAsync() =>
        StartHostAsync("pop3://127.0.0.1:0", "--allow-plaintext-auth", "-x", "http://127.0.0.1:0/", "-U", "alice:secret");

    private async Task<McpTestHost> StartHostAsync(string listenUrl, params string[] words)
    {
        var host = new McpTestHost();
        var started = await CallAsync(
            host.StartServer, $$"""{"listen_urls": ["{{listenUrl}}"], "arguments": {{JsonSerializer.Serialize(words)}}}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        return host;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    private async Task<string> ServeAsync(McpTestHost host, string clientText)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(clientText));
        host.Seams.ListenerFactory.Accept("http", connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return Encoding.ASCII.GetString(connection.WrittenToClient);
    }
}
