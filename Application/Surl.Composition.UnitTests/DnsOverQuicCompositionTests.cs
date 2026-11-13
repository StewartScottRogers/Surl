using System.Net;
using System.Security.Cryptography;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class DnsOverQuicCompositionTests
{
    public TestContext TestContext { get; set; } = null!;

    private static SurlCommandLine Parse(params string[] args) =>
        CommandLineParser.Parse(args, ["dns"]).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private static ListenUrl[] Expanded(params string[] args) =>
        [.. DnsTransportComposition.ExpandDnsListenUrls(Parse(args)).ListenUrls];

    [TestMethod]
    public void ExpandDnsListenUrls_QuicWithNoPort_IsOneDatagramListenerThatIsTlsFromTheFirstByteOn853()
    {
        var listenUrl = Expanded("--dns-transport", "quic", "dns://127.0.0.1").Single();

        Assert.AreEqual(853, listenUrl.Port);
        Assert.IsTrue(listenUrl.IsImplicitTls);
        Assert.AreEqual(ListenTransport.Datagrams, listenUrl.OnlyTransport);
    }

    [TestMethod]
    public void ExpandDnsListenUrls_QuicOnAnExplicitPort_KeepsThePort()
    {
        var listenUrl = Expanded("--dns-transport", "quic", "dns://127.0.0.1:5300").Single();

        Assert.AreEqual(5300, listenUrl.Port);
        Assert.AreEqual(ListenTransport.Datagrams, listenUrl.OnlyTransport);
    }

    [TestMethod]
    public void ExpandDnsListenUrls_UdpTcpTlsQuicWithNoPort_IsThePairOn53AndTlsAndQuicOn853()
    {
        var listenUrls = Expanded("--dns-transport", "udp,tcp,tls,quic", "dns://127.0.0.1");

        CollectionAssert.AreEqual(new[] { 53, 853, 853 }, listenUrls.Select(listenUrl => listenUrl.Port).ToArray());
        CollectionAssert.AreEqual(
            new ListenTransport?[] { null, ListenTransport.Connections, ListenTransport.Datagrams },
            listenUrls.Select(listenUrl => listenUrl.OnlyTransport).ToArray());
    }

    [TestMethod]
    [DataRow("dns://127.0.0.1:5300", "5300")]
    [DataRow("dns://[::1]:853", "853")]
    public void FindRefusal_UdpWithQuicOnOneExplicitPort_NamesBothAndThePort(string listenUrl, string port) =>
        Assert.AreEqual(
            $"--dns-transport udp and quic cannot share port {port}",
            DnsTransportComposition.FindRefusal(Parse("--dns-transport", "udp,quic", listenUrl)));

    [TestMethod]
    [DataRow("dns://127.0.0.1")]
    [DataRow("dns://127.0.0.1:0")]
    public void FindRefusal_UdpWithQuicOnNoPortOrPortZero_IsNone(string listenUrl) =>
        Assert.IsNull(DnsTransportComposition.FindRefusal(Parse("--dns-transport", "udp,quic", listenUrl)));

    [TestMethod]
    public void FindRefusal_QuicOnPort53_IsRefused() =>
        Assert.AreEqual(
            "--dns-transport quic cannot use port 53",
            DnsTransportComposition.FindRefusal(Parse("--dns-transport", "quic", "dns://127.0.0.1:53")));

    [TestMethod]
    public void FindRefusal_UdpOnPort53WithoutQuic_IsNone() =>
        Assert.IsNull(DnsTransportComposition.FindRefusal(Parse("--dns-transport", "udp", "dns://127.0.0.1:53")));

    [TestMethod]
    public void FindRefusal_QuicOnAnotherPort_IsNone() =>
        Assert.IsNull(DnsTransportComposition.FindRefusal(Parse("--dns-transport", "quic", "dns://127.0.0.1:5300")));

    [TestMethod]
    public async Task ComposeAsync_UdpWithQuicOnOneExplicitPort_IsRefusedFailedInitWithTheDecisionsText()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            Parse("--self-signed", "--dns-transport", "udp,quic", "dns://127.0.0.1:5300"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        Assert.AreEqual("--dns-transport udp and quic cannot share port 5300", start.Failure.Lines.Single());
    }

    [TestMethod]
    public async Task ComposeAsync_QuicOnPort53_IsRefusedFailedInitWithTheDecisionsText()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            Parse("--self-signed", "--dns-transport", "quic", "dns://127.0.0.1:53"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        Assert.AreEqual("--dns-transport quic cannot use port 53", start.Failure.Lines.Single());
    }

    [TestMethod]
    public async Task ComposeAsync_QuicWithoutACertificate_IsRefusedCertificateProblem()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            Parse("--dns-transport", "quic", "dns://127.0.0.1:5300"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
    }

    [TestMethod]
    public async Task ComposeAsync_QuicWithSelfSigned_BindsADatagramListenerThatIsTlsFromTheFirstByteOn853()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var start = await SurlServerComposition.ComposeAsync(
            Parse("--self-signed", "--dns-transport", "quic", "dns://127.0.0.1"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.IsNull(start.Failure);
        var listenUrl = boundUrls.Single();
        Assert.AreEqual(853, listenUrl.Port);
        Assert.IsTrue(listenUrl.IsImplicitTls);
        Assert.AreEqual(ListenTransport.Datagrams, listenUrl.OnlyTransport);
    }

    [TestMethod]
    public void Compose_QuicWithSelfSigned_HasTheQuicCertificate()
    {
        using var composition = ServerTlsComposition.Compose(
            DnsTransportComposition.ExpandDnsListenUrls(Parse("--self-signed", "--dns-transport", "quic", "dns://127.0.0.1")), TimeProvider.System);

        Assert.IsNotNull(composition.QuicCertificate);
    }

    [TestMethod]
    public void Compose_UdpWithSelfSigned_HasNoQuicCertificate()
    {
        using var composition = ServerTlsComposition.Compose(
            Parse("--self-signed", "--dns-transport", "tls", "dns://127.0.0.1"), TimeProvider.System);

        Assert.IsNull(composition.QuicCertificate);
    }

    [TestMethod]
    public void ComposeDnsOverQuic_WithoutTheServedCertificate_Throws() =>
        Assert.ThrowsExactly<InvalidOperationException>(() => ExtendedProtocolServers.ComposeDnsOverQuic(ComposeServices()));

    [TestMethod]
    public void ComposeDnsOverQuic_WithTheServedCertificate_AnswersDns()
    {
        var services = ComposeServices();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        services.QuicCertificate.Certificate = new QuicServerCertificate([new byte[] { 1 }], key);

        var server = ExtendedProtocolServers.ComposeDnsOverQuic(services);

        CollectionAssert.AreEqual(new[] { "dns" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_PlainListenUrl_GoesToTheUdpServerAndBuildsNoQuicServer()
    {
        var udp = new RecordingDatagramServer();
        var server = new DnsDatagramRoutingProtocolServer(udp, () => throw new AssertFailedException("DoQ must not be built."));
        await using var flow = new InMemoryDatagramFlow(new byte[1], []);

        await server.ServeAsync(flow, ContextFor(new ListenUrl("dns", "127.0.0.1", 53)));

        Assert.AreEqual(1, udp.Served);
        CollectionAssert.AreEqual(new[] { "dns" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ListenUrlThatIsTlsFromTheFirstByte_GoesToTheQuicServerBuiltOnce()
    {
        var udp = new RecordingDatagramServer();
        var services = ComposeServices();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        services.QuicCertificate.Certificate = new QuicServerCertificate([new byte[] { 1 }], key);
        var built = 0;
        var server = new DnsDatagramRoutingProtocolServer(udp, () =>
        {
            built++;
            return ExtendedProtocolServers.ComposeDnsOverQuic(services);
        });
        var listenUrl = new ListenUrl("dns", "127.0.0.1", 853) { IsImplicitTls = true };

        await ServeCancelledAsync(server, listenUrl);
        await ServeCancelledAsync(server, listenUrl);

        Assert.AreEqual(0, udp.Served);
        Assert.AreEqual(1, built);
    }

    [TestMethod]
    public void ServeAsync_NullContext_Throws()
    {
        var server = new DnsDatagramRoutingProtocolServer(new RecordingDatagramServer(), () => throw new AssertFailedException("unused"));

        Assert.ThrowsExactly<ArgumentNullException>(() => server.ServeAsync(null!, null!));
    }

    [TestMethod]
    public void Registrations_Dns_HasTheRoutingDatagramServerAndTheTcpServer()
    {
        var servers = ExtendedProtocolServers.Compose(new ExtendedProtocolServerServices(ComposeServices()));

        Assert.AreEqual(1, servers.OfType<DnsDatagramRoutingProtocolServer>().Count());
        Assert.IsTrue(servers.OfType<DnsDatagramRoutingProtocolServer>().Single() is IDatagramProtocolServer);
    }

    private static ExchangeContext ContextFor(ListenUrl listenUrl, CancellationToken token = default) =>
        new(
            1,
            listenUrl,
            new IPEndPoint(IPAddress.Loopback, 1),
            new IPEndPoint(IPAddress.Loopback, 2),
            new RecordingExchangeLog(),
            TimeProvider.System,
            token);

    private static async Task ServeCancelledAsync(DnsDatagramRoutingProtocolServer server, ListenUrl listenUrl)
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using var flow = new InMemoryDatagramFlow(new byte[1], []);
        try
        {
            await server.ServeAsync(flow, ContextFor(listenUrl, cancelled.Token));
        }
        catch (OperationCanceledException)
        {
            // The cancelled exchange stops the flow once the server is built.
        }
    }

    private static ComposedProtocolServices ComposeServices()
    {
        var unserved = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);
        return new ComposedProtocolServices(
            unserved.ContentStore,
            new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex),
            unserved.AuthenticationPolicy,
            TimeProvider.System,
            unserved.SshHostKeys,
            unserved.SshAlgorithms,
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            advertisesHttp3: false);
    }

    private sealed class RecordingDatagramServer : IDatagramProtocolServer
    {
        public int Served { get; private set; }

        public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["dns"]);

        public Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
        {
            Served++;
            return Task.CompletedTask;
        }
    }
}
