using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy.JudgeKerberosLoginAsync"/> publishing a <see cref="LoginJudgedEvent"/>
/// and refusing an accepted principal a refuse-login rule matches (ADR-0165, decisions 7 to 9).
/// </summary>
[TestClass]
public sealed class KerberosLoginJudgementPolicyTests
{
    private const string Principal = "alice@EXAMPLE.TEST";

    private readonly ManualTimeProvider clock = new();

    private readonly RecordingSurlEventPublisher events = new();

    private AuthenticationPolicy Create(IFaultPlan? faults = null) =>
        PolicyFixture.CreateObserved(
            new AccountBook([new Account("alice", "pw")]), clock, events, faults ?? EmptyFaultPlan.Instance, null, []);

    private async Task<(bool Result, bool WasDelayed)> SettleAsync(ValueTask<bool> pending)
    {
        var task = pending.AsTask();
        var wasDelayed = !task.IsCompleted;
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return (await task, wasDelayed);
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_AcceptedToken_PublishesAcceptedGssapiLoginWithNoDelay()
    {
        var (result, wasDelayed) = await SettleAsync(
            Create().JudgeKerberosLoginAsync(new KerberosLogin(Principal, true, "socks5", null), CancellationToken.None));

        Assert.IsTrue(result);
        Assert.IsFalse(wasDelayed);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5", Principal, "gssapi", true, false) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_AcceptedTokenWithNoPrincipal_PublishesEmptyAccount()
    {
        var (result, _) = await SettleAsync(
            Create().JudgeKerberosLoginAsync(new KerberosLogin(null, true, "socks5", null), CancellationToken.None));

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5", string.Empty, "gssapi", true, false) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_RefusedToken_PublishesEmptyAccountAndWaitsTheRefusalDelay()
    {
        var (result, wasDelayed) = await SettleAsync(
            Create().JudgeKerberosLoginAsync(new KerberosLogin(Principal, false, "socks5h", null), CancellationToken.None));

        Assert.IsFalse(result);
        Assert.IsTrue(wasDelayed);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5h", string.Empty, "gssapi", false, false) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_RefuseLoginRuleForThePrincipal_RefusesAfterTheDelayAndPublishesTheFault()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = Principal });

        var (result, wasDelayed) = await SettleAsync(
            Create(plan).JudgeKerberosLoginAsync(new KerberosLogin(Principal, true, "socks5", null), CancellationToken.None));

        Assert.IsFalse(result);
        Assert.IsTrue(wasDelayed);
        CollectionAssert.AreEqual(
            new SurlEvent[]
            {
                new FaultAppliedEvent("f1", FaultKind.RefuseLogin, FaultStage.Login, null, Principal),
                new LoginJudgedEvent(null, "socks5", Principal, "gssapi", false, true),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_RuleForAnotherPrincipal_LeavesTheLoginAccepted()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "bob@EXAMPLE.TEST" });

        var (result, _) = await SettleAsync(
            Create(plan).JudgeKerberosLoginAsync(new KerberosLogin(Principal, true, "socks5", null), CancellationToken.None));

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_NullLogin_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await Create().JudgeKerberosLoginAsync(null!, CancellationToken.None));

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await Create().JudgeKerberosLoginAsync(new KerberosLogin(Principal, true, "socks5", null), cts.Token));
    }
}
