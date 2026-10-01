using System.Text;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the SMB server (BL-299): <c>smb</c> and, TLS from
/// the first byte, <c>smbs</c>, which the server answers itself and which needs <c>--cert</c> or
/// <c>--self-signed</c> (ADR-0073 decision 6), serving the one content store. No test here
/// touches the disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerSmbTests
{
    private static readonly string NewLine = Environment.NewLine;

    // A NetBIOS session message holding an SMB_COM_NEGOTIATE offering only NT LM 0.12, with the
    // flags, flags2 and process ID upstream curl sends ([MS-CIFS] 2.2.3.1, 2.2.4.52.1).
    private static readonly byte[] NegotiateRequest = Convert.FromHexString(
        "0000002F"
        + "FF534D42" + "72" + "00000000" + "18" + "4100" + "BA00" + "0000000000000000" + "0000" + "0000" + "1DD7" + "0000" + "0000"
        + "00" + "0C00" + "02" + Convert.ToHexString(Encoding.ASCII.GetBytes("NT LM 0.12")) + "00");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_SmbListenUrl_StartsAListenerAnsweredByTheSmbServer()
    {
        var run = await ServeOneConnectionAsync(NegotiateRequest, "smb://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("smb", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on smb://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        Assert.IsNull(run.TlsSettings);
        AssertNegotiateAnswered(run.Written);
    }

    [TestMethod]
    public async Task RunAsync_SmbsListenUrlWithSelfSigned_StartsASecuredListenerAnsweredByTheSmbServer()
    {
        var run = await ServeOneConnectionAsync(NegotiateRequest, "-s", "--self-signed", "smbs://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("smbs", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        AssertNegotiateAnswered(run.Written);
    }

    [TestMethod]
    public async Task RunAsync_SmbsWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblem()
    {
        var run = new Run(new FakeListenerFactory());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(run).RunAsync(["smbs://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) smbs://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    // The negotiate response: a NetBIOS session message whose SMB header names SMB_COM_NEGOTIATE
    // with status 0.
    private static void AssertNegotiateAnswered(byte[] written)
    {
        Assert.IsGreaterThan(13, written.Length);
        Assert.AreEqual(0x00, written[0]);
        CollectionAssert.AreEqual(new byte[] { 0xFF, (byte)'S', (byte)'M', (byte)'B', 0x72, 0, 0, 0, 0 }, written[4..13]);
    }

    // Serves until the connection has ended, then stops.
    private async Task<Run> ServeOneConnectionAsync(byte[] request, params string[] args)
    {
        var connection = new FakeConnection(request);
        var run = new Run(new FakeListenerFactory { Connection = connection });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(run).RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        run.Output = output.ToString();
        run.Written = connection.WrittenBytes;
        return run;
    }

    private static CommandLineRunner CreateRunner(Run run) =>
        new(run.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, null);

    private sealed class Run(FakeListenerFactory factory)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public ServerTlsSettings? TlsSettings { get; private set; }

        public string Output { get; set; } = string.Empty;

        public byte[] Written { get; set; } = [];

        public IListenerFactory Create(ServerTlsSettings? tlsSettings)
        {
            TlsSettings = tlsSettings;
            return Factory;
        }
    }
}
