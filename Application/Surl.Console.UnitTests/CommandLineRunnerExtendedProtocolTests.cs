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

        var running = runner.RunAsync(["s3://127.0.0.1/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(new ListenUrl("s3", "127.0.0.1", 443), factory.StartedListenUrls.Single());
        Assert.AreEqual($"Listening on s3://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
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

        var exitCode = await runner.RunAsync(["s3://127.0.0.1/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"s3\" not supported" + NewLine, error.ToString());
        Assert.AreEqual(string.Empty, output.ToString());
        Assert.IsEmpty(factory.StartedListenUrls);
        Assert.AreEqual(0, composed);
    }

    [TestMethod]
    public async Task RunAsync_SwitchUnsetWithNoExtendedServer_RefusesAnExtendedSchemeAsAnUnknownScheme()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, composeExtendedProtocolServers: null);

        var exitCode = await runner.RunAsync(["s3://127.0.0.1/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"s3\" not supported" + NewLine, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_SwitchUnsetWithAFakeExtendedServer_KeepsEveryOriginalSchemesDefaultPort()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(factory, FakeEnvironmentVariableReader.Empty, _ => [new FakeExtendedProtocolServer()]);

        var running = runner.RunAsync(["http://127.0.0.1/", "s3://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await running;

        CollectionAssert.AreEqual(
            new[] { new ListenUrl("http", "127.0.0.1", 80), new ListenUrl("s3", "127.0.0.1", 0) },
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
            VersionText.Compose(InformationalVersion(), RuntimeInformation.RuntimeIdentifier, runner.ComposeRegisteredSchemes(), ["s3"]),
            output.ToString());
        StringAssert.EndsWith(output.ToString(), NewLine + "Extended: s3" + NewLine);
        Assert.DoesNotContain(" s3", output.ToString().Split(NewLine)[1]);
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

        CollectionAssert.AreEqual(new[] { "s3" }, schemes.ToArray());
    }

    [TestMethod]
    public void ComposeRegisteredSchemes_WithAFakeExtendedServer_ListsNoExtendedScheme()
    {
        var runner = CreateRunner(new FakeListenerFactory(), FakeEnvironmentVariableReader.Empty, _ => [new FakeExtendedProtocolServer()]);

        var schemes = runner.ComposeRegisteredSchemes();

        CollectionAssert.AreEquivalent(SchemeDefaultPorts.Schemes.ToArray(), schemes.ToArray());
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
