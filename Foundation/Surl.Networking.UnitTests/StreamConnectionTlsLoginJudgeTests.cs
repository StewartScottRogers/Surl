using System.Net;
using System.Net.Security;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// The <see cref="ITlsLoginJudge"/> a <see cref="StreamConnection"/> keeps reaches the TLS-SRP
/// handshake (ADR-0165, decision 2), over the recorded upstream curl 8.21.0 client bytes.
/// </summary>
[TestClass]
public sealed class StreamConnectionTlsLoginJudgeTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 443);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UseTlsLoginJudge_Null_Throws()
    {
        var (connection, _) = CreateSrp();

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.UseTlsLoginJudge(null!));
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_JudgeSetAndLoginAccepted_AsksThenJudgesAccepted()
    {
        var (connection, client) = CreateSrp();
        await using var _ = connection;
        var judge = new RecordingJudge(refuse: false);
        connection.UseTlsLoginJudge(judge);
        await client.WriteAsync(Recording("CurlTlsSrpJsmithAbc"), TestContext.CancellationToken);

        var session = await connection.UpgradeToTlsAsync(TestContext.CancellationToken);

        Assert.AreEqual("jsmith", session.SrpUserName);
        CollectionAssert.AreEqual(new[] { "ask:jsmith", "judged:jsmith:True" }, judge.Calls);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_JudgeRefusesAMatchingLogin_FailsTheHandshakeAndJudgesRefused()
    {
        var (connection, client) = CreateSrp();
        await using var _ = connection;
        var judge = new RecordingJudge(refuse: true);
        connection.UseTlsLoginJudge(judge);
        await client.WriteAsync(Recording("CurlTlsSrpJsmithAbc"), TestContext.CancellationToken);
        client.CompleteWrites();

        var failure = await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        StringAssert.Contains(failure.Message, "login refused for 'jsmith'");
        Assert.IsNull(connection.TlsSession);
        CollectionAssert.AreEqual(new[] { "ask:jsmith", "judged:jsmith:False" }, judge.Calls);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_JudgeSetAndWrongPassword_JudgesRefusedWithoutAsking()
    {
        var (connection, client) = CreateSrp();
        await using var _ = connection;
        var judge = new RecordingJudge(refuse: false);
        connection.UseTlsLoginJudge(judge);
        await client.WriteAsync(Recording("CurlTlsSrpJsmithBadpass"), TestContext.CancellationToken);
        client.CompleteWrites();

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        CollectionAssert.AreEqual(new[] { "judged:jsmith:False" }, judge.Calls);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_NoJudgeSet_StillCompletesTheSrpLogin()
    {
        var (connection, client) = CreateSrp();
        await using var _ = connection;
        await client.WriteAsync(Recording("CurlTlsSrpJsmithAbc"), TestContext.CancellationToken);

        var session = await connection.UpgradeToTlsAsync(TestContext.CancellationToken);

        Assert.AreEqual("jsmith", session.SrpUserName);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_JudgeSetAndSslStreamHandshake_NeverCallsTheJudge()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var (server, clientStream) = InMemoryDuplexStream.CreatePair();
        var handshake = ServerTlsHandshake.ForListener(settings, new ListenUrl("https", "127.0.0.1", 443));
        await using var connection = new StreamConnection(server, Local, Remote, new ShutdownControl(server), handshake);
        var judge = new RecordingJudge(refuse: true);
        connection.UseTlsLoginJudge(judge);

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        await using var client = new SslStream(clientStream, leaveInnerStreamOpen: true);
        await client.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
            TestContext.CancellationToken);
        var session = await upgrade;

        Assert.IsNull(session.SrpUserName);
        Assert.IsEmpty(judge.Calls);
    }

    private static (StreamConnection Connection, InMemoryDuplexStream Client) CreateSrp()
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        var handshake = ServerTlsHandshake.ForListener(StreamConnectionTlsSrpTests.RecordedSettings(), new ListenUrl("https", "127.0.0.1", 443));

        return (new StreamConnection(server, Local, Remote, new ShutdownControl(server), handshake), client);
    }

    private static byte[] Recording(string caseName)
    {
        using var resource = typeof(StreamConnectionTlsLoginJudgeTests).Assembly.GetManifestResourceStream($"TlsSrp/{caseName}/client.bin")
            ?? throw new InvalidOperationException($"No recording {caseName}.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);

        return copy.ToArray();
    }

    private sealed class RecordingJudge(bool refuse) : ITlsLoginJudge
    {
        public List<string> Calls { get; } = [];

        public ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken cancellationToken)
        {
            Calls.Add($"ask:{userName}");

            return ValueTask.FromResult(refuse);
        }

        public void Judged(string userName, bool isAccepted) => Calls.Add($"judged:{userName}:{isAccepted}");
    }

    private sealed class ShutdownControl(InMemoryDuplexStream stream) : IConnectionTransportControl
    {
        public void ShutdownSend() => stream.CompleteWrites();

        public void ResetAndClose() => stream.Dispose();
    }
}
