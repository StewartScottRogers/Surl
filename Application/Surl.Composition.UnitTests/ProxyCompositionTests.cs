using System.Net;
using System.Text;
using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ProxyCompositionTests
{
    private const string ForwardedRequest = "GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nConnection: close\r\n\r\n";
    private const string ConnectRequest = "CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Start_AnHttpProxy_ForwardsOneRequestToAFakeOriginThroughTheComposedConnector()
    {
        var origin = new InMemoryOriginConnector().ScriptConnection(
            "origin.test", 80, new InMemoryConnection([Bytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok")]));
        var client = new FakeConnection(Bytes(ForwardedRequest));

        var (bound, _) = await ServeAsync(CommandLine("http://127.0.0.1:3128"), origin, "http", client);

        StringAssert.StartsWith(Text(client.WrittenBytes), "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(Text(client.WrittenBytes), "\r\n\r\nok");
        Assert.AreEqual("origin.test", origin.Requests.Single().Host);
        Assert.IsTrue(bound.Single().IsProxy);
    }

    [TestMethod]
    public async Task Start_AnHttpProxy_TunnelsOneConnectToAFakeOriginThroughTheComposedConnector()
    {
        var origin = new InMemoryOriginConnector().ScriptConnection("origin.test", 443, new InMemoryConnection([]));
        var client = new FakeConnection(Bytes(ConnectRequest));

        await ServeAsync(CommandLine("http://127.0.0.1:3128"), origin, "http", client);

        Assert.AreEqual("HTTP/1.1 200 Connection established\r\n\r\n", Text(client.WrittenBytes));
        Assert.AreEqual(443, origin.Requests.Single().Port);
    }

    [TestMethod]
    public async Task Start_AnHttpsProxy_IsMarkedAsAProxyListenerAndForwardsOneRequest()
    {
        var origin = new InMemoryOriginConnector().ScriptConnection(
            "origin.test", 80, new InMemoryConnection([Bytes("HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n")]));
        var client = new FakeConnection(Bytes(ForwardedRequest));

        var (bound, _) = await ServeAsync(CommandLine("https://127.0.0.1:3129") with { SelfSigned = true }, origin, "https", client);

        StringAssert.StartsWith(Text(client.WrittenBytes), "HTTP/1.1 204 No Content\r\n");
        Assert.IsTrue(bound.Single().IsProxy);
    }

    [TestMethod]
    public async Task Start_AnHttpsProxy_TunnelsOneConnectToAFakeOrigin()
    {
        var origin = new InMemoryOriginConnector().ScriptConnection("origin.test", 443, new InMemoryConnection([]));
        var client = new FakeConnection(Bytes(ConnectRequest));

        await ServeAsync(CommandLine("https://127.0.0.1:3129") with { SelfSigned = true }, origin, "https", client);

        Assert.AreEqual("HTTP/1.1 200 Connection established\r\n\r\n", Text(client.WrittenBytes));
    }

    [TestMethod]
    public async Task Start_NoProxyListenUrl_ServesHttpWithTheHttpServer()
    {
        var origin = new InMemoryOriginConnector();
        var client = new FakeConnection(Bytes("GET /missing HTTP/1.1\r\nHost: x\r\nConnection: close\r\n\r\n"));

        await ServeAsync(StartTestSeams.Parse("http://127.0.0.1:8080"), origin, "http", client);

        StringAssert.StartsWith(Text(client.WrittenBytes), "HTTP/1.1 404");
        Assert.AreEqual(0, origin.Requests.Count);
    }

    [TestMethod]
    public async Task Start_ProxyAccounts_ReachTheServerAndAnUnauthenticatedRequestIsChallenged()
    {
        var origin = new InMemoryOriginConnector();
        var client = new FakeConnection(Bytes(ForwardedRequest));
        var commandLine = CommandLine("http://127.0.0.1:3128") with { ProxyAccounts = [new CommandLineAccount("alice", "secret")] };

        await ServeAsync(commandLine, origin, "http", client);

        StringAssert.StartsWith(Text(client.WrittenBytes), "HTTP/1.1 407 Proxy Authentication Required\r\n");
        StringAssert.Contains(Text(client.WrittenBytes), "Proxy-Authenticate: ");
        Assert.AreEqual(0, origin.Requests.Count);
    }

    [TestMethod]
    public async Task Start_ProxyAccounts_AnAccountWithItsPasswordIsForwarded()
    {
        var origin = new InMemoryOriginConnector().ScriptConnection(
            "origin.test", 80, new InMemoryConnection([Bytes("HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n")]));
        var credentials = Convert.ToBase64String(Bytes("alice:secret"));
        var client = new FakeConnection(Bytes(
            ForwardedRequest.Replace("Connection: close", $"Proxy-Authorization: Basic {credentials}\r\nConnection: close", StringComparison.Ordinal)));
        var commandLine = CommandLine("http://127.0.0.1:3128") with
        {
            ProxyAccounts = [new CommandLineAccount("alice", "secret")],
            AllowPlaintextAuthentication = true,
        };

        await ServeAsync(commandLine, origin, "http", client);

        StringAssert.StartsWith(Text(client.WrittenBytes), "HTTP/1.1 204 No Content\r\n");
    }

    [TestMethod]
    public async Task Start_TheOriginsAccounts_DoNotMakeTheProxyAskForALogin()
    {
        var origin = new InMemoryOriginConnector();
        var client = new FakeConnection(Bytes(ForwardedRequest));
        var commandLine = CommandLine("http://127.0.0.1:3128") with { Accounts = [new CommandLineAccount("alice", "secret")] };

        await ServeAsync(commandLine, origin, "http", client);

        StringAssert.StartsWith(Text(client.WrittenBytes), "HTTP/1.1 502 Bad Gateway\r\n");
        Assert.AreEqual(1, origin.Requests.Count);
    }

    [TestMethod]
    public async Task Start_AProxyUserFile_ProvidesTheProxyAccounts()
    {
        var origin = new InMemoryOriginConnector();
        var client = new FakeConnection(Bytes(ForwardedRequest));
        var commandLine = CommandLine("http://127.0.0.1:3128") with { ProxyUserFile = "proxy-users.txt" };
        var seams = Seams(origin, "http", client) with { ReadStartFile = _ => Bytes("bob:hunter2\n") };

        await ServeAsync(commandLine, seams, client);

        StringAssert.StartsWith(Text(client.WrittenBytes), "HTTP/1.1 407 ");
    }

    [TestMethod]
    public async Task Start_AnUnreadableProxyUserFile_FailsWithCouldNotReadFile()
    {
        var commandLine = CommandLine("http://127.0.0.1:3128") with { ProxyUserFile = "proxy-users.txt" };
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => throw new IOException("gone") };

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(37) Could not read user file proxy-users.txt" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task Start_AMalformedProxyUserFile_FailsWithFailedInitAndNeverShowsAPassword()
    {
        var commandLine = CommandLine("http://127.0.0.1:3128") with { ProxyUserFile = "proxy-users.txt" };
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => Bytes("no-colon-here\n") };

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        StringAssert.StartsWith(start.Failure.Lines.Single(), "(2) User file proxy-users.txt, ");
    }

    [TestMethod]
    public async Task Start_AnHttpsProxyWithoutACertificate_FailsWithCertificateProblem()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLine("https://127.0.0.1:3129"), new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(58) https://127.0.0.1:3129/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" },
            start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task Start_AnOriginRuleFromTheCommandLine_ReachesTheConnectorTheSeamCreates()
    {
        AllowedOriginRule? composed = null;
        var seams = new StartTestSeams().Seams with
        {
            CreateOriginConnector = rule =>
            {
                composed = rule;
                return new InMemoryOriginConnector();
            },
        };
        var commandLine = CommandLine("http://127.0.0.1:3128") with
        {
            ProxyAllowedOrigins = [ProxyAllowedOrigin.Range(IPAddress.Parse("10.0.0.0"), 8)],
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsTrue(composed!.Admits(new IPEndPoint(IPAddress.Parse("10.1.2.3"), 80)));
        Assert.IsFalse(composed.Admits(new IPEndPoint(IPAddress.Parse("192.168.0.1"), 80)));
    }

    [TestMethod]
    public async Task Start_WithNoSeamForTheConnector_ComposesASocketOriginConnector()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLine("http://127.0.0.1:3128"), new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
    }

    [TestMethod]
    public void ComposeAllowedOriginRule_NothingGiven_AdmitsLoopbackOnlyAndNeverTheProxyItself()
    {
        var rule = ProxyComposition.ComposeAllowedOriginRule(CommandLine("http://127.0.0.1:3128"));

        Assert.IsTrue(rule.Admits(new IPEndPoint(IPAddress.Loopback, 80)));
        Assert.IsFalse(rule.Admits(new IPEndPoint(IPAddress.Loopback, 3128)));
        Assert.IsFalse(rule.Admits(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 80)));
    }

    [TestMethod]
    public void ComposeAllowedOriginRule_EveryAddress_AdmitsAnOriginButStillNotTheProxy()
    {
        var rule = ProxyComposition.ComposeAllowedOriginRule(
            CommandLine("http://127.0.0.1:3128") with { ProxyAllowedOrigins = [ProxyAllowedOrigin.EveryAddress] });

        Assert.IsTrue(rule.Admits(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 80)));
        Assert.IsFalse(rule.Admits(new IPEndPoint(IPAddress.Loopback, 3128)));
    }

    [TestMethod]
    public void ComposeAllowedOriginRule_AProxyHostThatIsNotAnAddress_RefusesItsPortOnEveryAddress()
    {
        var listenUrl = ListenUrlParser.ParseProxy("http://localhost:3128").ListenUrl!;

        var rule = ProxyComposition.ComposeAllowedOriginRule(new SurlCommandLine { ProxyListenUrls = [listenUrl] });

        Assert.IsFalse(rule.Admits(new IPEndPoint(IPAddress.Loopback, 3128)));
        Assert.IsFalse(rule.Admits(new IPEndPoint(IPAddress.IPv6Loopback, 3128)));
        Assert.IsTrue(rule.Admits(new IPEndPoint(IPAddress.Loopback, 80)));
    }

    [TestMethod]
    public void ComposeAllowedOriginRule_AProxyOnPortZero_RefusesNoPort()
    {
        var rule = ProxyComposition.ComposeAllowedOriginRule(CommandLine("http://127.0.0.1:0"));

        Assert.IsTrue(rule.Admits(new IPEndPoint(IPAddress.Loopback, 3128)));
    }

    [TestMethod]
    public void RouteProxyListeners_NoProxyListenUrl_ReturnsTheServersAsTheyAre()
    {
        var servers = UnservedServers();

        var routed = ProxyComposition.RouteProxyListeners(servers, new SurlCommandLine(), new InMemoryOriginConnector(), new AnonymousAuthenticationPolicy());

        Assert.AreSame(servers, routed);
    }

    [TestMethod]
    public void RouteProxyListeners_AProxyListenUrl_RoutesOnlyTheServersAnsweringHttpAndHttps()
    {
        var servers = UnservedServers();

        var routed = ProxyComposition.RouteProxyListeners(
            servers, CommandLine("http://127.0.0.1:3128"), new InMemoryOriginConnector(), new AnonymousAuthenticationPolicy());

        CollectionAssert.AreEqual(
            new[] { "http", "https" },
            routed.OfType<ProxyRoutingProtocolServer>().SelectMany(server => server.Schemes).ToArray());
        Assert.AreEqual(servers.Length, routed.Length);
    }

    [TestMethod]
    public void WriteLooseningWarning_EveryOriginAdmitted_WritesOneWarningFromInfoUp()
    {
        using var log = new StringWriter();

        ProxyComposition.WriteLooseningWarning(
            new SurlCommandLine { LogLevel = LogLevel.Info, ProxyAllowedOrigins = [ProxyAllowedOrigin.EveryAddress] }, log);

        Assert.AreEqual(
            $"surl: warning: --proxy-allow-origin: the proxy connects to every origin address, not only loopback{Environment.NewLine}",
            log.ToString());
    }

    [TestMethod]
    public void WriteLooseningWarning_ARangeOrAQuietLevel_WritesNothing()
    {
        using var log = new StringWriter();

        ProxyComposition.WriteLooseningWarning(
            new SurlCommandLine { LogLevel = LogLevel.Info, ProxyAllowedOrigins = [ProxyAllowedOrigin.Range(IPAddress.Parse("10.0.0.0"), 8)] }, log);
        ProxyComposition.WriteLooseningWarning(
            new SurlCommandLine { LogLevel = LogLevel.Error, ProxyAllowedOrigins = [ProxyAllowedOrigin.EveryAddress] }, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public async Task Start_EveryOriginAdmitted_WarnsOnStart()
    {
        var origin = new InMemoryOriginConnector();
        var client = new FakeConnection(Bytes(ForwardedRequest));
        var commandLine = CommandLine("http://127.0.0.1:3128") with
        {
            ProxyAllowedOrigins = [ProxyAllowedOrigin.EveryAddress],
            LogLevel = LogLevel.Info,
        };

        var (_, log) = await ServeAsync(commandLine, origin, "http", client);

        StringAssert.Contains(log, "surl: warning: --proxy-allow-origin: ");
    }

    private static SurlCommandLine CommandLine(string proxyUrl) =>
        new() { ProxyListenUrls = [ListenUrlParser.ParseProxy(proxyUrl).ListenUrl!] };

    private static IProtocolServer[] UnservedServers() => SurlServerComposition.ComposeUnservedProtocolServers(
        SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);

    private static SurlServerCompositionSeams Seams(InMemoryOriginConnector origin, string scheme, FakeConnection client) =>
        new StartTestSeams(
            new FakeListenerFactory { ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { [scheme] = Task.FromResult(client) } }).Seams
        with
        {
            CreateOriginConnector = _ => origin,
        };

    private Task<(IReadOnlyList<ListenUrl> Bound, string Log)> ServeAsync(
        SurlCommandLine commandLine, InMemoryOriginConnector origin, string scheme, FakeConnection client) =>
        ServeAsync(commandLine, Seams(origin, scheme, client), client);

    private async Task<(IReadOnlyList<ListenUrl> Bound, string Log)> ServeAsync(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, FakeConnection client)
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(' ', start.Failure!.Lines));

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await client.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
        return (boundUrls, log.ToString());
    }

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private static string Text(byte[] bytes) => Encoding.ASCII.GetString(bytes);
}
