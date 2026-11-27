using Surl.Cli;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The proxy policy of a start with no proxy account (BL-1826): no login is asked for, and a SOCKS5
/// GSS-API login is still published and refused by a <c>refuse-login</c> rule (ADR-0165, decisions 7 to 9).
/// </summary>
[TestClass]
public sealed class NoAccountProxyAuthenticationPolicyTests
{
    private const string Principal = "user@EXAMPLE.COM";

    private static readonly SurlCommandLine KeytabOnly = new() { KeytabFile = "socks.keytab" };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ComposeProxyPolicy_KeytabAndNoProxyAccount_PublishesOneLoginJudgedEventForAnAcceptedToken()
    {
        var events = new RecordingSurlEventPublisher();
        var policy = ComposePolicy(events, EmptyFaultPlan.Instance);

        var isStanding = await policy.JudgeKerberosLoginAsync(AcceptedLogin, TestContext.CancellationToken);

        Assert.IsTrue(isStanding);
        var judged = (LoginJudgedEvent)events.Events.Single();
        Assert.AreEqual(Principal, judged.Account);
        Assert.AreEqual("gssapi", judged.Mechanism);
        Assert.AreEqual("socks5", judged.Scheme);
        Assert.IsTrue(judged.IsAccepted);
        Assert.IsFalse(judged.IsRefusedByFault);
    }

    [TestMethod]
    public async Task ComposeProxyPolicy_KeytabNoProxyAccountAndARefuseLoginRule_RefusesTheMatchedLogin()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = Principal });
        // Each read of the clock moves it past the refusal delay, so the refusal is answered at once.
        var policy = ComposePolicy(events, plan, new SteppingTimeProvider { Step = TimeSpan.FromSeconds(2) });

        var isStanding = await policy.JudgeKerberosLoginAsync(AcceptedLogin, TestContext.CancellationToken);

        Assert.IsFalse(isStanding);
        Assert.AreEqual("f1", events.Events.OfType<FaultAppliedEvent>().Single().RuleId);
        var judged = events.Events.OfType<LoginJudgedEvent>().Single();
        Assert.IsFalse(judged.IsAccepted);
        Assert.IsTrue(judged.IsRefusedByFault);
    }

    [TestMethod]
    public async Task ComposeProxyPolicy_NoProxyAccount_ProceedsEveryHttpRequestWithNoChallenge()
    {
        var events = new RecordingSurlEventPublisher();
        var policy = ComposePolicy(events, EmptyFaultPlan.Instance);

        foreach (var session in new[] { policy.StartHttpConnection(null), policy.StartHttpConnection(null, null) })
        {
            var verdict = await session.JudgeAsync(
                new HttpAuthenticationRequest("CONNECT", "origin.test:443", true, []), TestContext.CancellationToken);

            Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
            Assert.IsEmpty(verdict.WwwAuthenticateValues);
        }

        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ComposeProxyPolicy_NoProxyAccount_AcceptsEveryPasswordLoginUncheckedAndNamesNoAccount()
    {
        var events = new RecordingSurlEventPublisher();
        var policy = ComposePolicy(events, EmptyFaultPlan.Instance);

        var verdict = await policy.CheckPasswordLoginAsync(
            new PasswordLogin("socks5", null, null, null), TestContext.CancellationToken);

        Assert.AreEqual(PasswordLoginVerdict.AcceptedUnchecked, verdict);
        Assert.IsFalse(policy.NamesAccount("alice"));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ComposeAsync_KeytabAndNoProxyAccount_OffersSocks5MethodZeroZeroToAClientOfferingIt()
    {
        var (written, events) = await ServeSocks5Async([0x05, 0x01, 0x00]);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x00 }, written);
        Assert.IsEmpty(events.OfType<LoginJudgedEvent>());
    }

    [TestMethod]
    public async Task ComposeAsync_KeytabAndNoProxyAccount_PublishesARefusedSocks5GssApiToken()
    {
        // The greeting offering GSS-API alone, then a context message whose one-byte token is no AP-REQ.
        var (written, events) = await ServeSocks5Async([0x05, 0x01, 0x01, 0x01, 0x01, 0x00, 0x01, 0x00]);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x01, 0xFF }, written);
        var judged = events.OfType<LoginJudgedEvent>().Single();
        Assert.AreEqual("socks5", judged.Scheme);
        Assert.IsFalse(judged.IsAccepted);
        Assert.IsFalse(judged.IsRefusedByFault);
    }

    private static KerberosLogin AcceptedLogin => new(Principal, true, "socks5", null);

    // Serves one SOCKS5 connection sending the bytes given, on a start with --keytab and no proxy
    // account, and returns what the server wrote and every event published.
    private async Task<(byte[] Written, List<SurlEvent> Events)> ServeSocks5Async(byte[] sent)
    {
        var client = new FakeConnection(sent);
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["socks5"] = Task.FromResult(client) },
        }).Seams with
        {
            ReadStartFile = _ => SocksGssApiLoginCompositionTests.AesKeytab(),
            // Moves past the refusal delay between two reads, so a refused token is answered at once.
            TimeProvider = new SteppingTimeProvider { Step = TimeSpan.FromSeconds(2) },
        };
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--keytab", "socks.keytab", "socks5://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(' ', start.Failure!.Lines));
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await client.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        List<SurlEvent> events = [];
        while (subscription.TryRead(out var surlEvent))
        {
            events.Add(surlEvent!);
        }

        return (client.WrittenBytes, events);
    }

    private static IAuthenticationPolicy ComposePolicy(
        ISurlEventPublisher events, IFaultPlan faults, TimeProvider? timeProvider = null)
    {
        var (policy, exitCode, failureMessage) = AuthenticationComposition.ComposeProxyPolicy(
            KeytabOnly, _ => throw new AssertFailedException("No proxy user file is read."), timeProvider ?? TimeProvider.System, events, faults);
        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsNull(failureMessage);
        return policy!;
    }
}
