using System.Reflection;
using System.Text;
using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ProxyRedactionCompositionTests
{
    private const string Wrong = "Basic d3Jvbmc6Y3JlZHM=";
    private const string Right = "Basic YWxpY2U6c2VjcmV0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Start_AProxyWithAccounts_LogsNoProxyCredentialForARefusedOrAnAcceptedLoginAndStillSends407()
    {
        var wrong = await ServeAsync("http://127.0.0.1:3128", [], $"Proxy-Authorization: {Wrong}\r\n");
        var right = await ServeAsync("http://127.0.0.1:3128", [], $"Proxy-Authorization: {Right}\r\n");

        StringAssert.StartsWith(Text(wrong.Client.WrittenBytes), "HTTP/1.1 407 ");
        foreach (var log in new[] { wrong.Log, right.Log })
        {
            StringAssert.Contains(log, "Proxy-Authorization: Basic ****");
            Assert.IsFalse(log.Contains("d3Jvbmc6Y3JlZHM", StringComparison.Ordinal) || log.Contains("YWxpY2U6c2VjcmV0", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task Start_AnOriginHttpListenUrlBesideAProxy_LogsAuthorizationBasicMasked()
    {
        var served = await ServeAsync("http://127.0.0.1:3128", ["http://127.0.0.1:8080"], $"Authorization: {Right}\r\n", originListenUrl: "http://127.0.0.1:8080");

        StringAssert.Contains(served.Log, "Authorization: Basic ****");
        Assert.IsFalse(served.Log.Contains("YWxpY2U6c2VjcmV0", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateRedactor_OriginRedacts_IsTheOriginsOne()
    {
        var origin = new RedactingServer();
        var router = new ProxyRoutingProtocolServer(origin, new RedactingServer());

        var redactor = router.CreateRedactor(new ExchangeRedactionContext(1, "http"));

        Assert.AreSame(origin.Created, redactor);
    }

    [TestMethod]
    public void CreateRedactor_OnlyTheProxyRedacts_IsTheProxysOne()
    {
        var proxy = new RedactingServer();
        var router = new ProxyRoutingProtocolServer(new PlainServer(), proxy);

        var redactor = router.CreateRedactor(new ExchangeRedactionContext(1, "http"));

        Assert.AreSame(proxy.Created, redactor);
    }

    [TestMethod]
    public void CreateRedactor_NeitherRedacts_PassesBytesThroughUnchanged()
    {
        var router = new ProxyRoutingProtocolServer(new PlainServer(), new PlainServer());
        var redactor = router.CreateRedactor(new ExchangeRedactionContext(1, "http"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();

        redactor.Redact("Authorization: x"u8, output);
        redactor.ObserveSent("sent"u8);
        redactor.Flush(output);

        Assert.AreEqual("Authorization: x", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void CreateRedactor_TheSignalRHubRouter_MasksBasicCredentials()
    {
        var router = new SignalRHubRoutingProtocolServer(_ => throw new AssertFailedException("No hub is built for a redactor."));
        var redactor = router.CreateRedactor(new ExchangeRedactionContext(1, "signalr"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();

        redactor.Redact(Encoding.ASCII.GetBytes($"GET /x HTTP/1.1\r\nAuthorization: {Right}\r\n\r\n"), output);
        redactor.Flush(output);

        StringAssert.Contains(Encoding.ASCII.GetString(output.WrittenSpan), "Authorization: Basic ****");
    }

    [TestMethod]
    public void ComposedServers_WithAProxy_EveryWrapperOfARedactingServerRedactsToo()
    {
        var commandLine = new SurlCommandLine { ProxyListenUrls = [ListenUrlParser.ParseProxy("http://127.0.0.1:3128").ListenUrl!] };
        var original = SurlServerComposition.ComposeUnservedProtocolServers(
            SurlServerComposition.ComposeContentStore(commandLine, TimeProvider.System), TimeProvider.System);
        var routed = ProxyComposition.RouteProxyListeners(original, commandLine, new InMemoryOriginConnector(), new AnonymousAuthenticationPolicy());
        var extended = SurlServerComposition.ComposeExtendedProtocolServers(
            true, null, SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System));
        var wrappers = 0;

        foreach (var server in routed.Concat(extended))
        {
            if (server.GetType().Assembly != typeof(ProxyRoutingProtocolServer).Assembly)
            {
                continue;
            }

            var wrapped = server.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.GetValue(server))
                .Concat(server.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(property => property.GetIndexParameters().Length == 0 && property.PropertyType != typeof(IReadOnlyList<string>))
                    .Select(property => property.GetValue(server)))
                .OfType<IExchangeBytesRedactingServer>()
                .Where(inner => !ReferenceEquals(inner, server));
            if (wrapped.Any())
            {
                wrappers++;
                Assert.IsInstanceOfType<IExchangeBytesRedactingServer>(server, $"{server.GetType().Name} wraps a redacting server but does not redact.");
            }
        }

        Assert.IsGreaterThan(0, wrappers);
    }

    private async Task<(string Log, FakeConnection Client)> ServeAsync(
        string proxyUrl, string[] originUrls, string extraHeader, string? originListenUrl = null)
    {
        var request = originListenUrl is null
            ? $"GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\n{extraHeader}Connection: close\r\n\r\n"
            : $"GET /a HTTP/1.1\r\nHost: x\r\n{extraHeader}Connection: close\r\n\r\n";
        var client = new FakeConnection(Encoding.ASCII.GetBytes(request));
        var factory = new FakeListenerFactory();
        if (originListenUrl is null)
        {
            factory = new FakeListenerFactory { ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["http"] = Task.FromResult(client) } };
        }
        else
        {
            factory.ConnectionsFor(originListenUrl).TryWrite(client);
        }

        var commandLine = new SurlCommandLine
        {
            ProxyListenUrls = [ListenUrlParser.ParseProxy(proxyUrl).ListenUrl!],
            ListenUrls = [.. originUrls.Select(url => ListenUrlParser.Parse(url).ListenUrl!)],
            ProxyAccounts = [new CommandLineAccount("alice", "secret")],
            AllowPlaintextAuthentication = true,
            LogLevel = LogLevel.Verbose,
        };
        var seams = new StartTestSeams(factory).Seams with { CreateOriginConnector = _ => new InMemoryOriginConnector() };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(' ', start.Failure!.Lines));
        var outputs = new SurlServingOutputs(
            log, log, new LevelledExchangeLogFactory(log, LogLevel.Verbose, false, TimeProvider.System), bound.SetResult);

        var serve = serving.ServeAsync(outputs, stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await client.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
        return (log.ToString(), client);
    }

    private static string Text(byte[] bytes) => Encoding.ASCII.GetString(bytes);

    private class PlainServer : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["http"];

        public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;
    }

    private sealed class RedactingServer : PlainServer, IExchangeBytesRedactingServer
    {
        public IReceivedBytesRedactor Created { get; } = new PassThroughRedactor();

        public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) => Created;
    }
}
