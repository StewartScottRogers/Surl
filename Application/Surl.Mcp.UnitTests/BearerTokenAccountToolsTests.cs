using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1603: add_account, set_account_password and remove_account take "user": "" for the Bearer-token account (ADR-0132 amendment 2).
[TestClass]
public sealed class BearerTokenAccountToolsTests
{
    private static readonly TimeSpan ExchangeTimeLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddAccount_EmptyUser_AddsTheBearerTokenAccountAndAnswersTheEmptyUser()
    {
        await using var host = await StartHostAsync("--allow-plaintext-auth", "-u", "alice:secret");

        var added = await CallAsync(host.AddAccount, """{"server_id": "s1", "user": "", "password": "tok456"}""");
        var accepted = await ServeAsync(host, "tok456");

        StoreToolsTests.AssertResult(added, """{"user":""}""");
        Assert.DoesNotContain("tok456", added.TextContent[0]);
        Assert.IsFalse(accepted.StartsWith("HTTP/1.1 401 ", StringComparison.Ordinal), accepted);
    }

    [TestMethod]
    public async Task SetAccountPassword_EmptyUser_RotatesTheBearerToken()
    {
        await using var host = await StartHostAsync("--allow-plaintext-auth", "-u", ":tok123");

        var rotated = await CallAsync(host.SetAccountPassword, """{"server_id": "s1", "user": "", "password": "tok456"}""");
        var oldToken = await ServeAsync(host, "tok123");
        var newToken = await ServeAsync(host, "tok456");
        var none = await CallAsync(host.SetAccountPassword, """{"server_id": "s1", "user": "ghost", "password": "x"}""");

        StoreToolsTests.AssertResult(rotated, """{"user":""}""");
        Assert.DoesNotContain("tok456", rotated.TextContent[0]);
        StringAssert.StartsWith(oldToken, "HTTP/1.1 401 ");
        Assert.IsFalse(newToken.StartsWith("HTTP/1.1 401 ", StringComparison.Ordinal), newToken);
        StoreToolsTests.AssertFailure(none, "user ghost has no account");
    }

    [TestMethod]
    public async Task SetAccountPassword_EmptyUserWithNoTokenAccount_IsAToolError()
    {
        await using var host = await StartHostAsync("--allow-plaintext-auth", "-u", "alice:secret");

        var result = await CallAsync(host.SetAccountPassword, """{"server_id": "s1", "user": "", "password": "tok"}""");

        StoreToolsTests.AssertFailure(result, "the Bearer-token account does not exist");
    }

    [TestMethod]
    public async Task RemoveAccount_EmptyUser_RemovesTheBearerTokenAccount()
    {
        await using var host = await StartHostAsync("--allow-plaintext-auth", "-u", ":tok123", "-u", "alice:secret");

        var removed = await CallAsync(host.RemoveAccount, """{"server_id": "s1", "user": ""}""");
        var after = await ServeAsync(host, "tok123");
        var again = await CallAsync(host.RemoveAccount, """{"server_id": "s1", "user": ""}""");

        StoreToolsTests.AssertResult(removed, """{"user":""}""");
        StringAssert.StartsWith(after, "HTTP/1.1 401 ");
        StoreToolsTests.AssertFailure(again, "server s1 has no Bearer-token account");
    }

    [TestMethod]
    public async Task AddAccount_EmptyUserTwice_IsAToolErrorThatHoldsNoToken()
    {
        await using var host = await StartHostAsync("--allow-plaintext-auth", "-u", ":tok123");

        var result = await CallAsync(host.AddAccount, """{"server_id": "s1", "user": "", "password": "tok456"}""");

        StoreToolsTests.AssertFailure(result, "the Bearer-token account already exists");
        Assert.DoesNotContain("tok456", result.TextContent[0]);
    }

    private async Task<McpTestHost> StartHostAsync(params string[] words)
    {
        var host = new McpTestHost();
        var started = await CallAsync(
            host.StartServer, $$"""{"listen_urls": ["http://127.0.0.1:0"], "arguments": {{JsonSerializer.Serialize(words)}}}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        return host;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    private async Task<string> ServeAsync(McpTestHost host, string token)
    {
        var request = $"GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {token}\r\nConnection: close\r\n\r\n";
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(request));
        host.Seams.ListenerFactory.Accept("http", connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return Encoding.ASCII.GetString(connection.WrittenToClient);
    }
}
