namespace Surl.Api;

// BL-1391, BL-1866: removing the last account is allowed and warned, and the server still asks for a login wherever
// it did, until a reset returns what the configured accounts give (ADR-0132 amendment 5).
public sealed partial class SurlServerTests
{
    private const string NoAccountLeftWarning =
        "surl: warning: no local account left; the server still asks for a login wherever it did";

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
        Assert.AreEqual(1, log.ToString().Split(NoAccountLeftWarning).Length - 1);
    }

    [TestMethod]
    public async Task AccountsRemove_TheLastAccount_AnswersAGetWithoutCredentialsWith401()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(StoreOptions(Http), seams.Host, TestContext.CancellationToken);
        server.Accounts.Remove("alice");

        var response = await ServeAsync(seams, "http", HttpGet("/hello.txt", null));

        Assert.IsFalse(server.Accounts.HasAccounts);
        StringAssert.StartsWith(response, "HTTP/1.1 401 ");
    }

    [TestMethod]
    public async Task AccountsRemove_TheOnlyAccountAddedWhileRunning_AnswersAGetWith401UntilResetAsync()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.AllowPlaintextAuthentication = true;
        options.Files["hello.txt"] = "hello"u8.ToArray();
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        server.Accounts.Add("bob", "builder");
        server.Accounts.Remove("bob");

        var removed = await ServeAsync(seams, "http", HttpGet("/hello.txt", null));
        await server.ResetAsync(TestContext.CancellationToken);
        var reset = await ServeAsync(seams, "http", HttpGet("/hello.txt", null));

        StringAssert.StartsWith(removed, "HTTP/1.1 401 ");
        StringAssert.StartsWith(reset, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(reset, "hello");
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
        Assert.DoesNotContain("no local account left", log.ToString());
    }

    // BL-1668 (ADR-0149 amendment): a configured directory is an account source beside the accounts; the one line is
    // true with it too (ADR-0132 amendment 5, decision 8).
    [TestMethod]
    public async Task AccountsRemove_TheLastLocalAccountWithADirectory_WritesTheSameWarning()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = StoreOptions(Pop3);
        options.Log = log;
        options.AdditionalArguments.Add("--user-ldap");
        options.AdditionalArguments.Add("ldap://127.0.0.1:1/ou=people,dc=example,dc=com;plaintext");
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        var isRemoved = server.Accounts.Remove("alice");

        Assert.IsTrue(isRemoved);
        Assert.IsFalse(server.Accounts.HasAccounts);
        Assert.AreEqual(1, log.ToString().Split(NoAccountLeftWarning).Length - 1);
    }
}
