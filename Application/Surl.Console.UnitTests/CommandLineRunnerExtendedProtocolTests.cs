using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Cli;
using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// ADR-0083 decisions 6 to 8: the extended protocol servers are composed in one place, behind the
/// one switch <c>SURL_EXTENSIONS</c>, each scheme at its extended default port.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerExtendedProtocolTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_SwitchUnsetWithAFakeExtendedServer_RegistersItAndListensAtItsExtendedDefaultPort()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, _ => [new FakeExtendedProtocolServer()]);

        var running = runner.RunAsync(["oci://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl("oci", "127.0.0.1", 443), factory.StartedListenUrls.Single());
        Assert.AreEqual($"Listening on oci://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_SwitchAtZeroWithAFakeExtendedServer_RefusesItsSchemeAsAnUnknownSchemeAndNeverComposesIt()
    {
        var factory = new FakeListenerFactory();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var composed = 0;
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.ExtensionsTurnedOff, _ =>
        {
            composed++;
            return [new FakeExtendedProtocolServer()];
        });

        var exitCode = await runner.RunAsync(["oci://127.0.0.1/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"oci\" not supported" + NewLine, error.ToString());
        Assert.AreEqual(string.Empty, output.ToString());
        Assert.IsEmpty(factory.StartedListenUrls);
        Assert.AreEqual(0, composed);
    }

    [TestMethod]
    public async Task RunAsync_SwitchUnsetWithThisBuildsExtendedServers_RefusesAnExtendedSchemeNoneAnswersAsAnUnknownScheme()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["oci://127.0.0.1/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"oci\" not supported" + NewLine, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_SwitchUnsetWithAFakeExtendedServer_KeepsEveryOriginalSchemesDefaultPort()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, _ => [new FakeExtendedProtocolServer()]);

        var running = runner.RunAsync(["http://127.0.0.1/", "oci://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await running;

        CollectionAssert.AreEqual(
            new[] { new ListenUrl("http", "127.0.0.1", 80), new ListenUrl("oci", "127.0.0.1", 0) },
            factory.StartedListenUrls.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_VersionWithAFakeExtendedServer_AddsTheExtendedLineAndLeavesProtocolsUnchanged()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, _ => [new FakeExtendedProtocolServer()]);

        var exitCode = await runner.RunAsync(["--version"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(
            VersionText.Compose(InformationalVersion(), RuntimeInformation.RuntimeIdentifier, runner.ComposeRegisteredSchemes(), ["oci"]),
            output.ToString());
        StringAssert.EndsWith(output.ToString(), NewLine + "Extended: oci" + NewLine);
        Assert.DoesNotContain(" oci", output.ToString().Split(NewLine)[1]);
    }

    [TestMethod]
    public async Task RunAsync_VersionWithTheSwitchAtZero_WritesNoExtendedLine()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(
            new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, _ => [new FakeExtendedProtocolServer()]);

        await runner.RunAsync(["--version"], output, error, TestContext.CancellationToken);

        Assert.DoesNotContain("Extended:", output.ToString());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_SwitchUnset_AreTheExtendedServersSchemes()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, _ => [new FakeExtendedProtocolServer()]);

        var schemes = runner.ComposeExtendedSchemes();

        CollectionAssert.AreEqual(new[] { "oci" }, schemes.ToArray());
    }

    [TestMethod]
    public void ComposeRegisteredSchemes_WithAFakeExtendedServer_ListsNoExtendedScheme()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, _ => [new FakeExtendedProtocolServer()]);

        var schemes = runner.ComposeRegisteredSchemes();

        CollectionAssert.AreEquivalent(SchemeDefaultPorts.Schemes.ToArray(), schemes.ToArray());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_RegistersS3()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var schemes = runner.ComposeExtendedSchemes();

        CollectionAssert.Contains(schemes.ToArray(), "s3");
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuildWithTheSwitchAtZero_RegistersNoS3()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        var schemes = runner.ComposeExtendedSchemes();

        Assert.IsEmpty(schemes);
    }

    [TestMethod]
    public async Task RunAsync_S3WithSelfSigned_ListensAtItsDefaultPort443()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync(["--self-signed", "s3://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl("s3", "127.0.0.1", 443), factory.StartedListenUrls.Single());
        Assert.AreEqual($"Listening on s3://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
    }

    [TestMethod]
    public async Task RunAsync_S3WithS3PlaintextAndNoCertificate_ServesItPlainAndWarns()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync(["--s3-plaintext", "s3://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl("s3", "127.0.0.1", 0) { IsImplicitTls = false }, factory.StartedListenUrls.Single());
        Assert.AreEqual($"Listening on s3://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.AreEqual("surl: warning: --s3-plaintext: s3 is served as plain HTTP, without TLS" + NewLine, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_S3WithoutACertificate_ExitsWithCertificateProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["s3://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) s3://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_S3WithTheSwitchAtZero_RefusesItsSchemeAsAnUnknownScheme()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["--self-signed", "s3://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"s3\" not supported" + NewLine, error.ToString());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_RegistersGs()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var schemes = runner.ComposeExtendedSchemes();

        CollectionAssert.Contains(schemes.ToArray(), "gs");
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuildWithTheSwitchAtZero_RegistersNoGs()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        var schemes = runner.ComposeExtendedSchemes();

        CollectionAssert.DoesNotContain(schemes.ToArray(), "gs");
    }

    [TestMethod]
    public async Task RunAsync_GsWithSelfSigned_ListensAtItsDefaultPort443()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync(["--self-signed", "gs://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl("gs", "127.0.0.1", 443), factory.StartedListenUrls.Single());
        Assert.AreEqual($"Listening on gs://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
    }

    [TestMethod]
    public async Task RunAsync_GsWithoutACertificate_ExitsWithCertificateProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["gs://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) gs://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_GsWithTheSwitchAtZero_RefusesItsSchemeAsAnUnknownScheme()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["--self-signed", "gs://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"gs\" not supported" + NewLine, error.ToString());
    }
    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_RegistersNatsNatssSignalrSignalrsSocketioAndSocketios()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var schemes = runner.ComposeExtendedSchemes();

        CollectionAssert.IsSubsetOf(new[] { "nats", "natss", "signalr", "signalrs", "socketio", "socketios" }, schemes.ToArray());
    }

    [TestMethod]
    [DataRow("nats")]
    [DataRow("natss")]
    [DataRow("coap")]
    [DataRow("coaps")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    [DataRow("redis")]
    [DataRow("rediss")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    [DataRow("webtransport")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    [DataRow("amqp")]
    [DataRow("amqps")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public async Task RunAsync_NatsSchemeWithTheSwitchAtZero_RefusesItAsAnUnknownScheme(string scheme)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["--self-signed", $"{scheme}://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual($"surl: (1) Protocol \"{scheme}\" not supported" + NewLine, error.ToString());
    }

    [TestMethod]
    [DataRow("signalr", new string[0], 80)]
    [DataRow("signalrs", new[] { "--self-signed" }, 443)]
    [DataRow("redis", new string[0], 6379)]
    [DataRow("rediss", new[] { "--self-signed" }, 6379)]
    public async Task RunAsync_SignalRSchemeWithTheSwitchUnset_ListensAtItsDefaultPort(string scheme, string[] options, int port)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync([.. options, $"{scheme}://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl(scheme, "127.0.0.1", port), factory.StartedListenUrls.Single());
    }

    [TestMethod]
    public async Task RunAsync_SignalRsSchemeWithoutACertificate_ExitsWithTheMissingCertificateCode()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["signalrs://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
    }

    [TestMethod]
    [DataRow("xmpp", new string[0], 5222)]
    [DataRow("xmpps", new[] { "--self-signed" }, 5223)]
    public async Task RunAsync_XmppSchemeWithTheSwitchUnset_ListensAtItsDefaultPort(string scheme, string[] options, int port)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync([.. options, $"{scheme}://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl(scheme, "127.0.0.1", port), factory.StartedListenUrls.Single());
    }

    [TestMethod]
    public async Task RunAsync_XmppsSchemeWithoutACertificate_ExitsWithTheMissingCertificateCode()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["xmpps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
    }

    [TestMethod]
    [DataRow("nats", new string[0])]
    [DataRow("natss", new[] { "--self-signed" })]
    public async Task RunAsync_NatsSchemeWithTheSwitchUnset_ListensAtItsDefaultPort4222(string scheme, string[] options)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync([.. options, $"{scheme}://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl(scheme, "127.0.0.1", 4222), factory.StartedListenUrls.Single());
        Assert.AreEqual($"Listening on {scheme}://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
    }

    [TestMethod]
    [DataRow("amqp", 5672, new string[0])]
    [DataRow("amqps", 5671, new[] { "--self-signed" })]
    [DataRow("stomp", 61613, new string[0])]
    [DataRow("stomps", 61614, new[] { "--self-signed" })]
    [DataRow("kafka", 9092, new string[0])]
    [DataRow("kafkas", 9093, new[] { "--self-signed" })]
    public async Task RunAsync_AmqpStompOrKafkaSchemeWithTheSwitchUnset_ListensAtItsDefaultPort(string scheme, int port, string[] options)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync([.. options, $"{scheme}://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl(scheme, "127.0.0.1", port), factory.StartedListenUrls.Single());
    }

    [TestMethod]
    public async Task RunAsync_StompsWithoutACertificate_ExitsWithCertificateProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["stomps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_AmqpsWithoutACertificate_ExitsWithCertificateProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["amqps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuild_RegistersAmqpAndAmqpsWithTheSwitchUnsetAndNeitherWithItAtZero()
    {
        var on = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);
        var off = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        CollectionAssert.IsSubsetOf(new[] { "amqp", "amqps" }, on.ComposeExtendedSchemes().ToArray());
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "amqp");
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "amqps");
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuild_RegistersMqtt5Mqtt5sAndMqtt5WsWithTheSwitchUnsetAndNoneWithItAtZeroWhileMqttStays()
    {
        var on = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);
        var off = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        CollectionAssert.IsSubsetOf(new[] { "mqtt5", "mqtt5s", "mqtt5+ws" }, on.ComposeExtendedSchemes().ToArray());
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "mqtt5");
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "mqtt5s");
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "mqtt5+ws");
        CollectionAssert.IsSubsetOf(new[] { "mqtt", "mqtts" }, off.ComposeRegisteredSchemes().ToArray());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuild_RegistersStompAndStompsWithTheSwitchUnsetAndNeitherWithItAtZero()
    {
        var on = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);
        var off = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        CollectionAssert.IsSubsetOf(new[] { "stomp", "stomps" }, on.ComposeExtendedSchemes().ToArray());
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "stomp");
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "stomps");
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuild_RegistersWebTransportWithTheSwitchUnsetAndNotWithItAtZero()
    {
        var on = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);
        var off = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        CollectionAssert.Contains(on.ComposeExtendedSchemes().ToArray(), "webtransport");
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "webtransport");
    }

    [TestMethod]
    public async Task RunAsync_WebTransportWithoutACertificate_ExitsWithCertificateProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["webtransport://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) webtransport://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_WebTransportWithSelfSigned_ListensAtItsDefaultPort443()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync(["--self-signed", "webtransport://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl("webtransport", "127.0.0.1", 443), factory.StartedListenUrls.Single());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuild_RegistersCoapAndCoapsWithTheSwitchUnsetAndNotWithItAtZero()
    {
        var on = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);
        var off = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        CollectionAssert.IsSubsetOf(new[] { "coap", "coaps" }, on.ComposeExtendedSchemes().ToArray());
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "coap");
        CollectionAssert.DoesNotContain(off.ComposeExtendedSchemes().ToArray(), "coaps");
    }

    [TestMethod]
    public async Task RunAsync_CoapsWithoutACertificateOrAnAccount_ExitsWithCertificateProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["coaps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) coaps://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
    }

    [TestMethod]
    [DataRow(new[] { "coap://127.0.0.1/" }, "coap", 5683)]
    [DataRow(new[] { "--self-signed", "coaps://127.0.0.1/" }, "coaps", 5684)]
    [DataRow(new[] { "--user", "alice:pw", "coaps://127.0.0.1/" }, "coaps", 5684)]
    public async Task RunAsync_CoapListenUrl_ListensAtItsDefaultPort(string[] arguments, string scheme, int port)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync(arguments, output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl(scheme, "127.0.0.1", port), factory.StartedListenUrls.Single());
    }

    [TestMethod]
    public async Task RunAsync_CoapsWithAnUnusableCertificateKey_ExitsWithCertificateProblem()
    {
        // A P-521 key: TLS serves it, DTLS cannot sign with it (ADR-0087 decision 3).
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP521);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pem = certificate.ExportCertificatePem() + key.ExportPkcs8PrivateKeyPem();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null, readStartFile: _ => System.Text.Encoding.ASCII.GetBytes(pem));

        var exitCode = await runner.RunAsync(["--cert", "server.pem", "coaps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        StringAssert.StartsWith(error.ToString(), "surl: (58) Could not load the server certificate: ");
    }

    [TestMethod]
    public async Task RunAsync_NatssWithoutACertificate_ExitsWithCertificateProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["natss://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) natss://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_ListsDnsOnceThoughItHasAUdpAndATcpServer()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var schemes = runner.ComposeExtendedSchemes();

        CollectionAssert.AreEqual(new[] { "amqp", "amqps", "az", "abfs", "abfss", "coap", "coaps", "dns", "gs", "kafka", "kafkas", "mqtt5", "mqtt5s", "mqtt5+ws", "nats", "natss", "redis", "rediss", "s3", "signalr", "signalrs", "socketio", "socketios", "stomp", "stomps", "webtransport", "xmpp", "xmpps" }, schemes.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_VerboseDnsWithTheSwitchUnset_BindsTcpThenUdpOnItsPortAndWritesOneStatusLineAndNoHttp3Note()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var running = runner.RunAsync(["-v", "--allow-zone-transfer", "dns://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        var udp = new ListenUrl("dns", "127.0.0.1", FakeListenerFactory.BoundPort);
        CollectionAssert.AreEqual(new[] { new ListenUrl("dns", "127.0.0.1", 0), udp }, factory.StartedListenUrls);
        CollectionAssert.AreEqual(new[] { udp }, factory.StartedDatagramListenUrls);
        Assert.AreEqual($"Listening on dns://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.DoesNotContain("HTTP/3", error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_DnsWithTheSwitchAtZero_RefusesItsSchemeAsAnUnknownScheme()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.ExtensionsTurnedOff, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["dns://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"dns\" not supported" + NewLine, error.ToString());
    }

    private static string? InformationalVersion() =>
        typeof(CommandLineRunner).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private static CommandLineRunner CreateRunner(
        FakeListenerFactory factory,
        IEnvironmentVariableReader environmentVariables,
        Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? composeExtendedProtocolServers,
        Func<string, byte[]>? readStartFile = null) =>
        new(
            _ => factory,
            _ => true,
            _ => DataDirectoryLockOutcome.NoLock,
            TimeProvider.System,
            new UnitTestReadOnlyContentFileSystem(),
            environmentVariables: environmentVariables,
            composeExtendedProtocolServers: composeExtendedProtocolServers,
            readStartFile: readStartFile);
}
