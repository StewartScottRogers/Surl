using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy.JudgeKerberosLoginAsync"/> publishing a <see cref="LoginJudgedEvent"/>,
/// accepting an accepted token only when an account has the principal's name unless
/// <c>--allow-anonymous</c> (ADR-0165, amendment 1), and refusing an accepted principal a refuse-login
/// rule matches (ADR-0165, decisions 7 to 9).
/// </summary>
[TestClass]
public sealed class KerberosLoginJudgementPolicyTests
{
    private const string Principal = "alice@EXAMPLE.TEST";

    private const string PrincipalWithoutAccount = "mallory@EXAMPLE.TEST";

    private readonly ManualTimeProvider clock = new();

    private readonly RecordingSurlEventPublisher events = new();

    private static AccountBook PrincipalAccount() => new([new Account(Principal, "unused")]);

    private AuthenticationPolicy Create(IFaultPlan? faults = null, AccountBook? accounts = null) =>
        PolicyFixture.CreateObserved(
            accounts ?? PrincipalAccount(), clock, events, faults ?? EmptyFaultPlan.Instance, null, []);

    private AuthenticationPolicy CreateAllowingAnonymous(IFaultPlan? faults = null) =>
        new(
            new AuthenticationSettings(new AccountBook([]), true, false, AuthenticationMethods.DefaultAccepted),
            [],
            clock,
            events,
            faults ?? EmptyFaultPlan.Instance);

    private async Task<(bool Result, bool WasDelayed)> SettleAsync(ValueTask<bool> pending)
    {
        var task = pending.AsTask();
        var wasDelayed = !task.IsCompleted;
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return (await task, wasDelayed);
    }

    private Task<(bool Result, bool WasDelayed)> JudgeAcceptedTokenAsync(AuthenticationPolicy policy, string? principal = Principal) =>
        SettleAsync(policy.JudgeKerberosLoginAsync(new KerberosLogin(principal, true, "socks5", null), CancellationToken.None));

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_AcceptedTokenWithAnAccountOfThePrincipalsName_PublishesAcceptedGssapiLoginWithNoDelay()
    {
        var (result, wasDelayed) = await JudgeAcceptedTokenAsync(Create());

        Assert.IsTrue(result);
        Assert.IsFalse(wasDelayed);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5", Principal, "gssapi", true, false) }, events.Events.ToArray());
    }

    [TestMethod]
    [DataRow(PrincipalWithoutAccount)]
    [DataRow("Alice@EXAMPLE.TEST")]
    [DataRow("alice@example.test")]
    [DataRow("alice@OTHER.TEST")]
    [DataRow("alice")]
    public async Task JudgeKerberosLoginAsync_AcceptedTokenWithNoAccountOfExactlyThePrincipalsName_RefusesAfterTheDelay(string principal)
    {
        var (result, wasDelayed) = await JudgeAcceptedTokenAsync(Create(), principal);

        Assert.IsFalse(result);
        Assert.IsTrue(wasDelayed);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5", principal, "gssapi", false, false) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_AcceptedTokenWithNoPrincipal_RefusesAndPublishesEmptyAccount()
    {
        var (result, wasDelayed) = await JudgeAcceptedTokenAsync(Create(), null);

        Assert.IsFalse(result);
        Assert.IsTrue(wasDelayed);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5", string.Empty, "gssapi", false, false) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_RefuseLoginRuleWithTimes_IsNotSpentByAPrincipalWithNoAccount()
    {
        var policy = Create(new SpendingFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Times = 1 }));

        var (withoutAccount, _) = await JudgeAcceptedTokenAsync(policy, PrincipalWithoutAccount);
        var (firstWithAccount, _) = await JudgeAcceptedTokenAsync(policy);
        var (secondWithAccount, _) = await JudgeAcceptedTokenAsync(policy);

        Assert.IsFalse(withoutAccount);
        Assert.IsFalse(firstWithAccount);
        Assert.IsTrue(secondWithAccount);
        CollectionAssert.AreEqual(
            new SurlEvent[]
            {
                new LoginJudgedEvent(null, "socks5", PrincipalWithoutAccount, "gssapi", false, false),
                new FaultAppliedEvent("f1", FaultKind.RefuseLogin, FaultStage.Login, null, Principal),
                new LoginJudgedEvent(null, "socks5", Principal, "gssapi", false, true),
                new LoginJudgedEvent(null, "socks5", Principal, "gssapi", true, false),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_AllowAnonymousAndNoAccount_AcceptsAndPublishes()
    {
        var (result, wasDelayed) = await JudgeAcceptedTokenAsync(CreateAllowingAnonymous());

        Assert.IsTrue(result);
        Assert.IsFalse(wasDelayed);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5", Principal, "gssapi", true, false) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_AllowAnonymousAndNoPrincipal_AcceptsAndPublishesEmptyAccount()
    {
        var (result, _) = await JudgeAcceptedTokenAsync(CreateAllowingAnonymous(), null);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new LoginJudgedEvent(null, "socks5", string.Empty, "gssapi", true, false) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_AllowAnonymousAndMatchingRefuseLoginRule_StillRefuses()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = Principal });

        var (result, wasDelayed) = await JudgeAcceptedTokenAsync(CreateAllowingAnonymous(plan));

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
    public async Task JudgeKerberosLoginAsync_EmptyAccountBookThenAccountAdded_RefusesThenAccepts()
    {
        var accounts = new AccountBook([]);
        var policy = Create(accounts: accounts);

        var (beforeAdd, _) = await JudgeAcceptedTokenAsync(policy);
        accounts.Add(new Account(Principal, "unused"));
        var (afterAdd, _) = await JudgeAcceptedTokenAsync(policy);

        Assert.IsFalse(beforeAdd);
        Assert.IsTrue(afterAdd);
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

        var (result, wasDelayed) = await JudgeAcceptedTokenAsync(Create(plan));

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

        var (result, _) = await JudgeAcceptedTokenAsync(Create(plan));

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
