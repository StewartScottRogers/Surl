namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/>'s <see cref="IDtlsLoginJudge"/> (ADR-0170 decisions 6 to 8, RFC 4279).
/// </summary>
[TestClass]
public sealed class DtlsServerSessionLoginJudgeTests
{
    private static readonly byte[] AliceKey = "secret"u8.ToArray();

    [TestMethod]
    public async Task HandshakeAsync_VerifiedLoginNotRefused_IsAskedThenJudgedAccepted()
    {
        var judge = new RecordingJudge();
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8] };

        var (outcome, _) = await RunAsync(client, judge);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        CollectionAssert.AreEqual(new[] { "asked alice", "judged alice True" }, judge.Calls);
    }

    [TestMethod]
    public async Task HandshakeAsync_VerifiedLoginRefused_SendsDecryptErrorWithNoServerFinishedAndJudgesRefused()
    {
        var judge = new RecordingJudge { Refuses = true };
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8] };

        var (outcome, flow) = await RunAsync(client, judge);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertSent, DtlsAlertDescription.DecryptError), outcome);
        Assert.IsFalse(client.IsEstablished);
        Assert.AreEqual(new DtlsAlert(true, DtlsAlertDescription.DecryptError), client.ReceivedAlerts.Single());
        CollectionAssert.AreEqual(new[] { "asked alice", "judged alice False" }, judge.Calls);
        Assert.IsNotNull(flow);
    }

    [TestMethod]
    public async Task HandshakeAsync_WrongKey_IsJudgedRefusedWithoutBeingAsked()
    {
        var judge = new RecordingJudge();
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], PskKey = "guess"u8.ToArray() };

        var (outcome, _) = await RunAsync(client, judge);

        Assert.AreEqual(DtlsAlertDescription.DecryptError, outcome.Alert);
        CollectionAssert.AreEqual(new[] { "judged alice False" }, judge.Calls);
    }

    [TestMethod]
    public async Task HandshakeAsync_UnknownIdentity_IsJudgedRefusedWithoutBeingAsked()
    {
        var judge = new RecordingJudge();
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], PskIdentity = "mallory"u8.ToArray() };

        await RunAsync(client, judge);

        CollectionAssert.AreEqual(new[] { "judged mallory False" }, judge.Calls);
    }

    [TestMethod]
    public async Task HandshakeAsync_FailedBeforeFinished_IsNeverJudged()
    {
        var judge = new RecordingJudge();
        var client = new UnitTestDtlsClient { CipherSuites = [0xC02B] };

        await RunAsync(client, judge);

        Assert.AreEqual(0, judge.Calls.Count);
    }

    [TestMethod]
    public async Task HandshakeAsync_CertificateHandshake_IsNeverJudged()
    {
        var judge = new RecordingJudge();
        var client = new UnitTestDtlsClient { CipherSuites = [0xC02B] };
        var (session, flow) = DtlsServerSessionTests.Start(client, configure: options => options with { LoginJudge = judge });
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual(0, judge.Calls.Count);
    }

    private static async Task<(DtlsHandshakeOutcome Outcome, UnitTestDatagramFlow Flow)> RunAsync(UnitTestDtlsClient client, RecordingJudge judge)
    {
        var (session, flow) = DtlsServerSessionTests.Start(
            client,
            configure: options => options with
            {
                Certificate = null,
                PreSharedKeys = identity => identity.SequenceEqual("alice"u8) ? AliceKey : null,
                LoginJudge = judge,
            });
        using var _ = session;
        return (await session.HandshakeAsync(flow.Exhausted.Token), flow);
    }

    private sealed class RecordingJudge : IDtlsLoginJudge
    {
        public bool Refuses { get; init; }

        public List<string> Calls { get; } = [];

        public ValueTask<bool> ShouldRefuseAsync(string identity, CancellationToken cancellationToken)
        {
            Calls.Add($"asked {identity}");
            return ValueTask.FromResult(Refuses);
        }

        public void Judged(string identity, bool isAccepted) => Calls.Add($"judged {identity} {isAccepted}");
    }
}
