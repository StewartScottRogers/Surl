using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1175: delete_file and remove_account (ADR-0161), each proved by a fake client of the protocol that reads the store.
[TestClass]
public sealed class DeleteAndRemoveToolsTests
{
    private static readonly TimeSpan ExchangeTimeLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DeleteFile_AfterPutFile_MakesTheFileUnreadableAndAnswersItsPath()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");
        await CallAsync(host.PutFile, """{"server_id": "s1", "path": "/a.txt", "text": "seeded"}""");

        var result = await CallAsync(host.DeleteFile, """{"server_id": "s1", "path": "/a.txt"}""");
        var served = await ServeAsync(host, "http", "GET /a.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n");

        StoreToolsTests.AssertResult(result, """{"path":"/a.txt"}""");
        StringAssert.StartsWith(served, "HTTP/1.1 404");
    }

    [TestMethod]
    public async Task DeleteFile_NothingAtThePath_IsAnErrorNamingThePath()
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");

        var result = await CallAsync(host.DeleteFile, """{"server_id": "s1", "path": "/none.txt"}""");

        StoreToolsTests.AssertFailure(result, "no file at /none.txt");
    }

    [TestMethod]
    [DataRow("reports/b.txt", "path reports/b.txt must start with /")]
    [DataRow("/../x.txt", "path /../x.txt may not hold a . or .. segment")]
    [DataRow("/a//b.txt", "path /a//b.txt is not one a request may name")]
    public async Task DeleteFile_AtAPathNoRequestMayName_IsAnErrorNamingThePathAndTheReason(string path, string expected)
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");

        var result = await CallAsync(host.DeleteFile, $$"""{"server_id": "s1", "path": "{{path}}"}""");

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(expected, Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task RemoveAccount_AfterAddAccount_MakesALoginFailAndNeverEchoesThePassword()
    {
        await using var host = await StartHostAsync("pop3://127.0.0.1:0", "--user", "alice:secret", "--allow-plaintext-auth");
        await CallAsync(host.AddAccount, """{"server_id": "s1", "user": "bob", "password": "pa55-w0rd"}""");

        var result = await CallAsync(host.RemoveAccount, """{"server_id": "s1", "user": "bob"}""");
        var served = await ServeAsync(host, "pop3", "USER bob\r\nPASS pa55-w0rd\r\nQUIT\r\n");

        StoreToolsTests.AssertResult(result, """{"user":"bob"}""");
        StringAssert.Contains(served, "-ERR");
        Assert.DoesNotContain("pa55-w0rd", result.TextContent[0]);
    }

    [TestMethod]
    public async Task RemoveAccount_TheLastAccount_AnswersSuccessWithAWarningThatTheServerStillAsksForALogin()
    {
        const string Warning = "server s1 has no local account left; it still asks for a login wherever it did";
        await using var host = await StartHostAsync("pop3://127.0.0.1:0", "--user", "alice:secret", "--allow-plaintext-auth");

        var result = await CallAsync(host.RemoveAccount, """{"server_id": "s1", "user": "alice"}""");

        Assert.IsFalse(result.IsError);
        Assert.AreEqual(2, result.TextContent.Count);
        Assert.AreEqual(Warning, result.TextContent[1]);
        using var answer = JsonDocument.Parse(result.TextContent[0]);
        Assert.AreEqual(Warning, answer.RootElement.GetProperty("warning").GetString());
    }

    [TestMethod]
    public async Task RemoveAccount_AnAccountWhileAnotherRemains_KeepsTheAnswerWithoutAWarning()
    {
        await using var host = await StartHostAsync("pop3://127.0.0.1:0", "--user", "alice:secret", "--allow-plaintext-auth");
        await CallAsync(host.AddAccount, """{"server_id": "s1", "user": "bob", "password": "pw"}""");

        var result = await CallAsync(host.RemoveAccount, """{"server_id": "s1", "user": "bob"}""");

        StoreToolsTests.AssertResult(result, """{"user":"bob"}""");
    }

    [TestMethod]
    public async Task RemoveAccount_UnknownAccount_IsAnErrorNamingTheServerAndUser()
    {
        await using var host = await StartHostAsync("pop3://127.0.0.1:0", "--user", "alice:secret", "--allow-plaintext-auth");

        var result = await CallAsync(host.RemoveAccount, """{"server_id": "s1", "user": "nobody"}""");

        StoreToolsTests.AssertFailure(result, "server s1 has no account nobody");
    }

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

    private async Task<string> ServeAsync(McpTestHost host, string scheme, string clientText)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(clientText));
        host.Seams.ListenerFactory.Accept(scheme, connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return Encoding.ASCII.GetString(connection.WrittenToClient);
    }
}
