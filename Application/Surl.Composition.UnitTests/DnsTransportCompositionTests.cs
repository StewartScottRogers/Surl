using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class DnsTransportCompositionTests
{
    public TestContext TestContext { get; set; } = null!;

    private static SurlCommandLine Parse(params string[] args) =>
        CommandLineParser.Parse(args, ["dns"]).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private static ListenUrl[] Expanded(params string[] args) =>
        [.. DnsTransportComposition.ExpandDnsListenUrls(Parse(args)).ListenUrls];

    [TestMethod]
    public void FindRefusal_NullCommandLine_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsTransportComposition.FindRefusal(null!));

    [TestMethod]
    public void ExpandDnsListenUrls_NullCommandLine_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsTransportComposition.ExpandDnsListenUrls(null!));

    [TestMethod]
    public void ExpandDnsListenUrls_DefaultTransports_LeavesTheListenUrlAsTheSharedPortPair()
    {
        var listenUrls = Expanded("dns://127.0.0.1:5300", "tftp://127.0.0.1:69");

        Assert.AreEqual(new ListenUrl("dns", "127.0.0.1", 5300), listenUrls[0]);
        Assert.IsNull(listenUrls[0].OnlyTransport);
        Assert.AreEqual("tftp", listenUrls[1].Scheme);
    }

    [TestMethod]
    public void ExpandDnsListenUrls_TlsOnAnExplicitPort_IsOneConnectionListenerThatIsTlsFromTheFirstByte()
    {
        var listenUrl = Expanded("--dns-transport", "tls", "dns://127.0.0.1:5300").Single();

        Assert.AreEqual(5300, listenUrl.Port);
        Assert.IsTrue(listenUrl.IsImplicitTls);
        Assert.AreEqual(ListenTransport.Connections, listenUrl.OnlyTransport);
    }

    [TestMethod]
    public void ExpandDnsListenUrls_TlsWithNoPort_Takes853()
    {
        var listenUrl = Expanded("--dns-transport", "tls", "dns://127.0.0.1").Single();

        Assert.AreEqual(853, listenUrl.Port);
        Assert.IsTrue(listenUrl.IsImplicitTls);
    }

    [TestMethod]
    public void ExpandDnsListenUrls_UdpTcpTlsWithNoPort_IsThePairOn53AndTlsOn853()
    {
        var listenUrls = Expanded("--dns-transport", "udp,tcp,tls", "dns://127.0.0.1");

        CollectionAssert.AreEqual(new[] { 53, 853 }, listenUrls.Select(listenUrl => listenUrl.Port).ToArray());
        CollectionAssert.AreEqual(new[] { false, true }, listenUrls.Select(listenUrl => listenUrl.IsImplicitTls).ToArray());
        Assert.IsNull(listenUrls[0].OnlyTransport);
    }

    [TestMethod]
    public void ExpandDnsListenUrls_UdpAlone_BindsOnlyTheDatagramListener() =>
        Assert.AreEqual(ListenTransport.Datagrams, Expanded("--dns-transport", "udp", "dns://127.0.0.1:5300").Single().OnlyTransport);

    [TestMethod]
    public void ExpandDnsListenUrls_TcpAlone_BindsOnlyTheConnectionListener() =>
        Assert.AreEqual(ListenTransport.Connections, Expanded("--dns-transport", "tcp", "dns://127.0.0.1:5300").Single().OnlyTransport);

    [TestMethod]
    public void ExpandDnsListenUrls_UdpWithTlsOnOneExplicitPort_PutsBothOnThatPort()
    {
        var listenUrls = Expanded("--dns-transport", "udp,tls", "dns://[::1]:5300");

        CollectionAssert.AreEqual(new[] { 5300, 5300 }, listenUrls.Select(listenUrl => listenUrl.Port).ToArray());
        CollectionAssert.AreEqual(
            new ListenTransport?[] { ListenTransport.Datagrams, ListenTransport.Connections },
            listenUrls.Select(listenUrl => listenUrl.OnlyTransport).ToArray());
    }

    [TestMethod]
    [DataRow("dns://127.0.0.1:5300/", "5300")]
    [DataRow("dns://[::1]:5300", "5300")]
    public void FindRefusal_TcpWithTlsOnOneExplicitPort_NamesBothAndThePort(string listenUrl, string port) =>
        Assert.AreEqual(
            $"--dns-transport tcp and tls cannot share port {port}",
            DnsTransportComposition.FindRefusal(Parse("--dns-transport", "udp,tcp,tls", listenUrl)));

    [TestMethod]
    [DataRow("dns://127.0.0.1")]
    [DataRow("dns://[::1]")]
    [DataRow("dns://127.0.0.1:0")]
    public void FindRefusal_TcpWithTlsOnNoPortOrPortZero_IsNone(string listenUrl) =>
        Assert.IsNull(DnsTransportComposition.FindRefusal(Parse("--dns-transport", "tcp,tls", listenUrl)));

    [TestMethod]
    public void FindRefusal_DefaultTransports_IsNone() =>
        Assert.IsNull(DnsTransportComposition.FindRefusal(Parse("dns://127.0.0.1:5300")));

    [TestMethod]
    public async Task ComposeAsync_TcpWithTlsOnOneExplicitPort_IsRefusedFailedInitWithTheDecisionsText()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            Parse("--self-signed", "--dns-transport", "tcp,tls", "dns://127.0.0.1:5300"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        Assert.AreEqual("--dns-transport tcp and tls cannot share port 5300", start.Failure.Lines.Single());
    }

    [TestMethod]
    public async Task ComposeAsync_TlsWithoutACertificate_IsRefusedCertificateProblem()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };

        var start = await SurlServerComposition.ComposeAsync(
            Parse("--dns-transport", "tls", "dns://127.0.0.1:5300"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
    }

    [TestMethod]
    public async Task ComposeAsync_UdpTcpAndTlsWithNoPort_BindsTheTlsListenerOn853AndTheSharedPairOn53()
    {
        var factory = new FakeListenerFactory();
        var seams = new StartTestSeams(factory).Seams with { AreExtensionsTurnedOn = true };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var start = await SurlServerComposition.ComposeAsync(
            Parse("--self-signed", "--dns-transport", "udp,tcp,tls", "dns://127.0.0.1"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.IsNull(start.Failure);
        CollectionAssert.AreEquivalent(new[] { 53, 853 }, boundUrls.Select(listenUrl => listenUrl.Port).ToArray());
        Assert.IsTrue(boundUrls.Single(listenUrl => listenUrl.Port == 853).IsImplicitTls);
        Assert.IsFalse(boundUrls.Single(listenUrl => listenUrl.Port == 53).IsImplicitTls);
    }
}
