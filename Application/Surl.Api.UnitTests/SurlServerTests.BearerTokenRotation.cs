using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1604: the Bearer token's account is the empty user name; a test rotates, adds and removes it in-process (ADR-0132 amendment 2).
public sealed partial class SurlServerTests
{
    private const string OldToken = "tok123";

    private const string NewToken = "tok456";

    private static SurlServerOptions BearerOptions(bool withToken)
    {
        var options = ApiTestSeams.Options(Http);
        if (withToken)
        {
            options.Accounts.Add(new SurlAccount(string.Empty, OldToken));
        }

        options.AllowPlaintextAuthentication = true;
        options.Files["hello.txt"] = "hello"u8.ToArray();
        return options;
    }

    private static string HttpGetWithBearer(string token) =>
        $"GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1:{FakeListenerFactory.BoundPort}\r\n"
        + $"Authorization: Bearer {token}\r\nConnection: close\r\n\r\n";

    [TestMethod]
    public async Task SetPassword_EmptyUserOnABearerTokenServer_RefusesTheOldTokenAndAcceptsTheNew()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(BearerOptions(true), seams.Host, TestContext.CancellationToken);
        StringAssert.StartsWith(await ServeAsync(seams, "http", HttpGetWithBearer(OldToken)), "HTTP/1.1 200 OK\r\n");

        server.Accounts.SetPassword(string.Empty, NewToken);

        var oldToken = await ServeAsync(seams, "http", HttpGetWithBearer(OldToken));
        var newToken = await ServeAsync(seams, "http", HttpGetWithBearer(NewToken));
        StringAssert.StartsWith(oldToken, "HTTP/1.1 401 ");
        StringAssert.StartsWith(newToken, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task SetPassword_EmptyUser_PublishesAccountRemovedThenAccountAddedWithTheEmptyName()
    {
        var seams = new ApiTestSeams();
        var options = BearerOptions(true);
        options.RecordExchanges = true;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        server.Accounts.SetPassword(string.Empty, NewToken);

        var changes = server.FindEvents(new SurlEventQuery { Store = StoreKind.Accounts })
            .OfType<StoreChangedEvent>().ToArray();
        CollectionAssert.AreEqual(
            new[] { StoreChange.AccountRemoved, StoreChange.AccountAdded }, changes.Select(change => change.Change).ToArray());
        Assert.IsTrue(changes.All(change => change.Name.Length == 0));
        var everything = string.Join('\n', server.FindEvents(new SurlEventQuery()).Select(found => found.ToString()));
        Assert.DoesNotContain(OldToken, everything);
        Assert.DoesNotContain(NewToken, everything);
    }

    [TestMethod]
    public async Task Add_EmptyUserWhileNoTokenAccount_AcceptsTheBearerToken()
    {
        var seams = new ApiTestSeams();
        var options = BearerOptions(false);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        server.Accounts.Add(string.Empty, NewToken);

        StringAssert.StartsWith(await ServeAsync(seams, "http", HttpGetWithBearer(NewToken)), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task Remove_EmptyUser_RefusesTheBearerTokenAfterwards()
    {
        var seams = new ApiTestSeams();
        var options = BearerOptions(true);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        var isRemoved = server.Accounts.Remove(string.Empty);

        Assert.IsTrue(isRemoved);
        StringAssert.StartsWith(await ServeAsync(seams, "http", HttpGetWithBearer(OldToken)), "HTTP/1.1 401 ");
    }
}
