using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-813: a test's own protocol server for a scheme Surl does not serve, given in SurlServerOptions.ProtocolServers and
// composed beside the built-in servers (ADR-0124, decision 2).
public sealed partial class SurlServerTests
{
    private const string Echo = "echo://127.0.0.1:0";

    [TestMethod]
    public async Task StartAsync_AnEchoServerBesideHttp_AnswersOnItsListenUrlAndItsExchangeIsRecordedWithIt()
    {
        var seams = new ApiTestSeams();
        var options = EchoOptions(Echo, Http);

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var echoed = await EchoOneAsync(seams, "ping");
        var served = await ServeOneOnHttpAsync(seams);
        var record = await server.WaitForExchangeAsync(
            new SurlEventQuery { Scheme = "echo" }, ExchangeTimeLimit, TestContext.CancellationToken);

        Assert.AreEqual("ping", Encoding.ASCII.GetString(echoed.WrittenToClient));
        StringAssert.StartsWith(Encoding.ASCII.GetString(served.WrittenToClient), "HTTP/1.1 200 OK\r\n");
        CollectionAssert.AreEqual(
            new[] { new Uri($"echo://127.0.0.1:{FakeListenerFactory.BoundPort}/"), new Uri($"http://127.0.0.1:{FakeListenerFactory.BoundPort}/") },
            server.BoundUrls.ToArray());
        Assert.AreEqual(new Uri($"echo://127.0.0.1:{FakeListenerFactory.BoundPort}/"), record.ListenUrl);
        CollectionAssert.AreEqual("ping"u8.ToArray(), record.Received);
    }

    [TestMethod]
    public async Task StartAsync_OnlyACustomListenUrlOnIpv6_ServesItAloneWithoutTheBrackets()
    {
        var seams = new ApiTestSeams();
        var options = EchoOptions("ECHO://[::1]:0/");

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var echoed = await EchoOneAsync(seams, "pong");

        Assert.AreEqual(new Uri($"echo://[::1]:{FakeListenerFactory.BoundPort}/"), server.BoundUrl);
        Assert.HasCount(1, server.BoundUrls);
        Assert.AreEqual("pong", Encoding.ASCII.GetString(echoed.WrittenToClient));
    }

    [TestMethod]
    public async Task StartAsync_ADropRuleOnTheCustomListenUrl_DropsItsConnectionAndListsTheRuleAsGiven()
    {
        var seams = new ApiTestSeams();
        var options = EchoOptions(Http, Echo);
        options.Faults.Add(SurlFault.DropConnection() with { Listener = Echo, Times = 1 });
        options.Faults.Add(SurlFault.RefuseLogin() with { Scheme = "HTTP" });

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var rules = server.Faults.List();
        var dropped = await EchoOneAsync(seams, "ping");
        var applied = (FaultAppliedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.FaultApplied }, ExchangeTimeLimit, TestContext.CancellationToken);
        var echoed = await EchoOneAsync(seams, "ping");

        Assert.AreEqual("drop;listener=echo%3A%2F%2F127.0.0.1%3A0", rules[0].Item);
        Assert.AreEqual("refuse-login;scheme=http", rules[1].Item);
        Assert.IsEmpty(dropped.WrittenToClient);
        Assert.AreEqual("f1", applied.RuleId);
        Assert.AreEqual("ping", Encoding.ASCII.GetString(echoed.WrittenToClient));
    }

    [TestMethod]
    public async Task Faults_AddADropRuleForTheCustomScheme_DropsItsNextConnectionAndLeavesHttpServed()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(EchoOptions(Http, Echo), seams.Host, TestContext.CancellationToken);

        var ruleId = server.Faults.Add(SurlFault.DropConnection() with { Scheme = "ECHO" });
        var dropped = await EchoOneAsync(seams, "ping");
        var served = await ServeOneOnHttpAsync(seams);

        Assert.AreEqual("f1", ruleId);
        Assert.AreEqual("drop;scheme=echo", server.Faults.List().Single().Item);
        Assert.IsEmpty(dropped.WrittenToClient);
        StringAssert.StartsWith(Encoding.ASCII.GetString(served.WrittenToClient), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ErrorRuleScopedToTheCustomScheme_IsRefusedAtStartAndWhenAdded()
    {
        var seams = new ApiTestSeams();
        var refusedAtStart = EchoOptions(Http, Echo);
        refusedAtStart.Faults.Add(SurlFault.ProtocolError("451 busy") with { Listener = Echo });

        var startException = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(refusedAtStart, seams.Host, TestContext.CancellationToken));
        await using var server = await SurlServer.StartAsync(EchoOptions(Http, Echo), seams.Host, TestContext.CancellationToken);
        var addException = Assert.ThrowsExactly<ArgumentException>(
            () => server.Faults.Add(SurlFault.ProtocolError("451 busy") with { Scheme = "echo" }));

        StringAssert.Contains(startException.Message, "error fault cannot be scoped to a custom protocol server's scheme");
        Assert.AreEqual("options", startException.ParamName);
        Assert.AreEqual("fault", addException.ParamName);
        Assert.IsEmpty(server.Faults.List());
    }

    [TestMethod]
    [DataRow("http", DisplayName = "an original scheme")]
    [DataRow("HTTPS", DisplayName = "an original implicit-TLS scheme in capitals")]
    [DataRow("abfs", DisplayName = "an extended scheme while extensions are off")]
    public async Task StartAsync_AProtocolServerForABuiltInScheme_IsRefusedAsAddOnly(string scheme)
    {
        var options = ApiTestSeams.Options(Http);
        options.ProtocolServers.Add(new SurlProtocolServer(scheme, _ => new EchoServer()));

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, $"ProtocolServers may only add a scheme: {scheme} is served by Surl or given twice.");
    }

    [TestMethod]
    [DataRow("twice", "may only add a scheme", DisplayName = "the same scheme twice")]
    [DataRow("no factory", "needs a factory", DisplayName = "a null factory")]
    [DataRow("null scheme", "needs a factory", DisplayName = "a null scheme")]
    [DataRow("bad scheme", "needs a factory", DisplayName = "a scheme that is not a URI scheme")]
    [DataRow("null entry", "may not hold null", DisplayName = "a null entry")]
    [DataRow("no port", "must be <scheme>://<host>:<port>", DisplayName = "a custom listen URL without its port")]
    [DataRow("path", "must be <scheme>://<host>:<port>", DisplayName = "a custom listen URL with a path")]
    [DataRow("user", "must be <scheme>://<host>:<port>", DisplayName = "a custom listen URL with a user")]
    [DataRow("fragment", "must be <scheme>://<host>:<port>", DisplayName = "a custom listen URL with a fragment")]
    [DataRow("not a URL", "must be <scheme>://<host>:<port>", DisplayName = "a custom listen URL Uri cannot read")]
    public async Task StartAsync_AMisusedProtocolServer_ThrowsArgumentException(string misuse, string expected)
    {
        var options = ApiTestSeams.Options(Http);
        options.ProtocolServers.Add(misuse switch
        {
            "twice" => new SurlProtocolServer("Echo", _ => new EchoServer()),
            "no factory" => new SurlProtocolServer("echo", null!),
            "null scheme" => new SurlProtocolServer(null!, _ => new EchoServer()),
            "bad scheme" => new SurlProtocolServer("1echo", _ => new EchoServer()),
            "null entry" => null!,
            _ => new SurlProtocolServer("echo", _ => new EchoServer()),
        });
        AddMisuse(options, misuse);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, expected);
    }

    [TestMethod]
    public async Task StartAsync_ANonsenseListenUrlBesideACustomServer_IsLeftToSurlWhichRefusesIt()
    {
        var options = EchoOptions("nonsense");

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken));

        Assert.IsNotEmpty(exception.Message);
    }

    [TestMethod]
    public async Task StartAsync_AFactoryReturningNull_ThrowsInvalidOperationExceptionNamingTheScheme()
    {
        var options = ApiTestSeams.Options(Http, Echo);
        options.ProtocolServers.Add(new SurlProtocolServer("echo", _ => null!));

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken));

        Assert.AreEqual("The protocol server factory for scheme echo returned null.", exception.Message);
    }

    [TestMethod]
    public async Task StartAsync_ACustomServersFactory_IsGivenTheInstancesStoresAccountsAndClock()
    {
        SurlProtocolServices? given = null;
        var options = ApiTestSeams.Options(Http, Echo);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.ProtocolServers.Add(new SurlProtocolServer("echo", services =>
        {
            given = services;
            return new EchoServer();
        }));

        await using var server = await SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken);

        Assert.IsNotNull(given);
        Assert.IsNotNull(given.ContentStore);
        Assert.IsNotNull(given.MailStore);
        Assert.IsNotNull(given.MessageBus);
        Assert.IsNotNull(given.AuthenticationPolicy);
        Assert.AreSame(options.TimeProvider, given.TimeProvider);
    }

    private static void AddMisuse(SurlServerOptions options, string misuse)
    {
        var listenUrl = misuse switch
        {
            "twice" => null,
            "no port" => "echo://127.0.0.1",
            "path" => "echo://127.0.0.1:0/path",
            "user" => "echo://alice@127.0.0.1:0",
            "fragment" => "echo://127.0.0.1:0#here",
            "not a URL" => "echo://127.0.0.1:99999",
            _ => Echo,
        };
        if (listenUrl is null)
        {
            options.ProtocolServers.Add(new SurlProtocolServer("ECHO", _ => new EchoServer()));
            return;
        }

        options.ListenUrls.Add(listenUrl);
    }

    private static SurlServerOptions EchoOptions(params string[] listenUrls)
    {
        var options = ApiTestSeams.Options(listenUrls);
        options.Files["hello.txt"] = "hello"u8.ToArray();
        options.RecordExchanges = true;
        options.ProtocolServers.Add(new SurlProtocolServer("echo", _ => new EchoServer()));
        return options;
    }

    // Brings one fake client in on the echo listener and waits until its exchange has ended.
    private async Task<FakeConnection> EchoOneAsync(ApiTestSeams seams, string text)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(text));
        seams.ListenerFactory.Accept("echo", connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return connection;
    }

    // Brings one fake client in with upstream curl's GET on the http listener only and waits until its exchange has ended.
    private async Task<FakeConnection> ServeOneOnHttpAsync(ApiTestSeams seams)
    {
        var connection = new FakeConnection(CurlGetHello);
        seams.ListenerFactory.Accept("http", connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return connection;
    }

    // A test's own server: writes back every byte the client sends until the client ends.
    private sealed class EchoServer : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["echo"];

        public async Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            var buffer = new byte[256];
            int read;
            while ((read = await connection.ReadAsync(buffer, context.CancellationToken)) > 0)
            {
                await connection.WriteAsync(buffer.AsMemory(0, read), context.CancellationToken);
            }
        }
    }
}
