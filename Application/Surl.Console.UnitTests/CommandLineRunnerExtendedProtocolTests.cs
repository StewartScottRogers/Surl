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
    public void ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_RegistersNatsAndNatss()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var schemes = runner.ComposeExtendedSchemes();

        CollectionAssert.IsSubsetOf(new[] { "nats", "natss" }, schemes.ToArray());
    }

    [TestMethod]
    [DataRow("nats")]
    [DataRow("natss")]
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

        CollectionAssert.AreEqual(new[] { "dns", "nats", "natss", "s3" }, schemes.ToArray());
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
        Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? composeExtendedProtocolServers) =>
        new(
            _ => factory,
            _ => true,
            _ => DataDirectoryLockOutcome.NoLock,
            TimeProvider.System,
            new UnitTestReadOnlyContentFileSystem(),
            environmentVariables: environmentVariables,
            composeExtendedProtocolServers: composeExtendedProtocolServers);
}
