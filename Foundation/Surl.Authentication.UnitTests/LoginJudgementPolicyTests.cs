using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/> publishing each judged login as a <see cref="LoginJudgedEvent"/>
/// and refusing a login a <see cref="FaultKind.RefuseLogin"/> rule matches as a wrong credential is
/// refused (ADR-0124, decisions 1 and 4).
/// </summary>
[TestClass]
public sealed class LoginJudgementPolicyTests
{
    private const string AlicePassword = "alice-s3cret";

    private const string BobPassword = "bob-hunter2";

    private const string WrongPassword = "wrong-pw";

    private static readonly AccountBook AliceAndBob =
        new([new Account("alice", AlicePassword), new Account("bob", BobPassword)]);

    // [MS-NLMP] section 4.2.2's NTLMv1 example: user User, password Password.
    private static readonly byte[] SmbServerChallenge = Convert.FromHexString("0123456789ABCDEF");

    private static readonly byte[] SmbLmResponse = Convert.FromHexString("98DEF7B87F88AA5DAFE2DF779688A172DEF11C7D5CCDEF13");

    private static readonly byte[] SmbNtResponse = Convert.FromHexString("67C43011F30298A2AD35ECE64F16331C44BDBED927841F94");

    private static readonly AccountBook SmbUser = new([new Account("User", "Password")]);

    private static readonly IReadOnlySet<AuthenticationMethod> NtlmV1 = new HashSet<AuthenticationMethod> { AuthenticationMethod.NtlmV1 };

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private static readonly FaultRule RefuseAlice = new("f1", FaultKind.RefuseLogin) { Account = "alice" };

    private readonly ManualTimeProvider clock = new();

    private readonly RecordingSurlEventPublisher events = new();

    private AuthenticationPolicy Create(
        IFaultPlan? faults = null,
        AccountBook? accounts = null,
        IReadOnlySet<AuthenticationMethod>? acceptedMethods = null,
        params IHttpAuthenticationMethod[] httpMethods) =>
        PolicyFixture.CreateObserved(
            accounts ?? AliceAndBob, clock, events, faults ?? EmptyFaultPlan.Instance, acceptedMethods, httpMethods);

    private static PasswordLogin Password(string userName, string password) =>
        new("mqtt", userName, Encoding.UTF8.GetBytes(password), PolicyFixture.Tls);

    private static SmbNtlmV1Login Smb(byte[]? ntResponse = null) =>
        new("User", "Domain", SmbServerChallenge, SmbLmResponse, ntResponse ?? SmbNtResponse, null);

    private static string Basic(string user, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));

    // Lets the refusal delay pass when the judgement waits on it, and says whether it did.
    private async Task<(T Result, bool WasDelayed)> SettleAsync<T>(ValueTask<T> pending)
    {
        var task = pending.AsTask();
        var wasDelayed = !task.IsCompleted;
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return (await task, wasDelayed);
    }

    private async Task<SaslLoginStep> PlainAsync(AuthenticationPolicy policy, string user, string password)
    {
        var exchange = policy.StartSaslExchange(
            new SaslExchangeStart("smtp", "PLAIN", SaslExchangeRunner.Plain(string.Empty, user, password), PolicyFixture.Tls));

        return (await SettleAsync(exchange.BeginAsync(CancellationToken.None))).Result;
    }

    private static LoginJudgedEvent Judged(string scheme, string account, string mechanism, bool isAccepted, bool isRefusedByFault = false) =>
        new(null, scheme, account, mechanism, isAccepted, isRefusedByFault);

    private static FaultAppliedEvent AppliedToAlice => new("f1", FaultKind.RefuseLogin, FaultStage.Login, null, "alice");

    // ---- Published logins ----

    [TestMethod]
    public async Task CheckPasswordLoginAsync_AcceptedAndRefused_PublishEachWithUserMethodAndVerdict()
    {
        var policy = Create();

        await SettleAsync(policy.CheckPasswordLoginAsync(Password("alice", AlicePassword), CancellationToken.None));
        await SettleAsync(policy.CheckPasswordLoginAsync(Password("alice", WrongPassword), CancellationToken.None));

        CollectionAssert.AreEqual(
            new SurlEvent[] { Judged("mqtt", "alice", "password", true), Judged("mqtt", "alice", "password", false) },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_UncheckedRefusals_PublishNothing()
    {
        var policy = Create();

        await policy.CheckPasswordLoginAsync(new PasswordLogin("mqtt", null, null, PolicyFixture.Tls), CancellationToken.None);
        await policy.CheckPasswordLoginAsync(new PasswordLogin("mqtt", "alice", Encoding.UTF8.GetBytes(AlicePassword), null), CancellationToken.None);

        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task JudgeAsync_HttpBasicLogin_PublishesItUnderHttp()
    {
        var policy = Create(httpMethods: [new BasicAuthenticationMethod(AliceAndBob)]);

        await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(Basic("alice", AlicePassword)), CancellationToken.None);

        CollectionAssert.AreEqual(new SurlEvent[] { Judged("http", "alice", "Basic", true) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task StartSaslExchange_PlainLogins_PublishEachUnderTheExchangesScheme()
    {
        var policy = Create();

        await PlainAsync(policy, "alice", AlicePassword);
        await PlainAsync(policy, "bob", WrongPassword);

        CollectionAssert.AreEqual(
            new SurlEvent[] { Judged("smtp", "alice", "PLAIN", true), Judged("smtp", "bob", "PLAIN", false) },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task CheckSshPasswordLoginAsync_PasswordLogin_PublishesItUnderSsh()
    {
        var policy = Create();

        await policy.CheckSshPasswordLoginAsync(new SshPasswordLogin("password", "alice", Encoding.UTF8.GetBytes(AlicePassword)), CancellationToken.None);

        CollectionAssert.AreEqual(new SurlEvent[] { Judged("ssh", "alice", "password", true) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_SpecificationLogin_PublishesItUnderSmb()
    {
        var policy = Create(accounts: SmbUser, acceptedMethods: NtlmV1);

        await policy.CheckSmbNtlmV1LoginAsync(Smb(), CancellationToken.None);

        CollectionAssert.AreEqual(new SurlEvent[] { Judged("smb", "User", "ntlmv1", true) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task EveryPublishedEvent_StringProperties_HoldNoPassword()
    {
        var policy = Create(new SpendingFaultPlan(RefuseAlice), httpMethods: [new BasicAuthenticationMethod(AliceAndBob)]);
        var session = policy.StartHttpConnection(PolicyFixture.Tls);

        foreach (var password in new[] { AlicePassword, WrongPassword })
        {
            await SettleAsync(policy.CheckPasswordLoginAsync(Password("bob", password), CancellationToken.None));
            await SettleAsync(policy.CheckPasswordLoginAsync(Password("alice", password), CancellationToken.None));
            await SettleAsync(session.JudgeAsync(PolicyFixture.Get(Basic("bob", password)), CancellationToken.None));
            await PlainAsync(policy, "bob", password);
            await SettleAsync(policy.CheckSshPasswordLoginAsync(new SshPasswordLogin("password", "bob", Encoding.UTF8.GetBytes(password)), CancellationToken.None));
        }

        Assert.HasCount(11, events.Events);
        var secrets = new[] { AlicePassword, BobPassword, WrongPassword };
        foreach (var surlEvent in events.Events)
        {
            foreach (var property in surlEvent.GetType().GetProperties().Where(property => property.PropertyType == typeof(string)))
            {
                var value = (string)property.GetValue(surlEvent)!;
                Assert.IsFalse(secrets.Any(secret => value.Contains(secret, StringComparison.Ordinal)), $"{surlEvent.GetType().Name}.{property.Name}");
            }
        }
    }

    // ---- refuse-login ----

    [TestMethod]
    public async Task CheckPasswordLoginAsync_RefuseLoginForAlice_RefusesHerRightPasswordAsAWrongOneWhileBobLogsIn()
    {
        var policy = Create(new SpendingFaultPlan(RefuseAlice));

        var wrong = await SettleAsync(policy.CheckPasswordLoginAsync(Password("alice", WrongPassword), CancellationToken.None));
        var refused = await SettleAsync(policy.CheckPasswordLoginAsync(Password("alice", AlicePassword), CancellationToken.None));
        var bob = await SettleAsync(policy.CheckPasswordLoginAsync(Password("bob", BobPassword), CancellationToken.None));

        Assert.AreEqual((PasswordLoginVerdict.RefusedCredentials, true), wrong);
        Assert.AreEqual(wrong, refused);
        Assert.AreEqual((PasswordLoginVerdict.Accepted, false), bob);
        CollectionAssert.AreEqual(
            new SurlEvent[]
            {
                Judged("mqtt", "alice", "password", false),
                AppliedToAlice,
                Judged("mqtt", "alice", "password", false, true),
                Judged("mqtt", "bob", "password", true),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_RefuseLoginTimesOne_FiresOnce()
    {
        var policy = Create(new SpendingFaultPlan(RefuseAlice with { Times = 1 }));

        var first = await SettleAsync(policy.CheckPasswordLoginAsync(Password("alice", AlicePassword), CancellationToken.None));
        var second = await SettleAsync(policy.CheckPasswordLoginAsync(Password("alice", AlicePassword), CancellationToken.None));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, first.Result);
        Assert.AreEqual(PasswordLoginVerdict.Accepted, second.Result);
        Assert.AreEqual(1, events.Events.OfType<FaultAppliedEvent>().Count());
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_RuleOfAnotherKind_IsNotApplied()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Account = "alice" });
        var policy = Create(plan);

        var verdict = await policy.CheckPasswordLoginAsync(Password("alice", AlicePassword), CancellationToken.None);

        Assert.AreEqual(PasswordLoginVerdict.Accepted, verdict);
        Assert.AreEqual(new FaultPoint(FaultStage.Login) { Scheme = "mqtt", Account = "alice" }, plan.Points.Single());
        CollectionAssert.AreEqual(new SurlEvent[] { Judged("mqtt", "alice", "password", true) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task CheckSshPasswordLoginAsync_RefuseLogin_IsRefusedAsAWrongPassword()
    {
        var policy = Create(new SpendingFaultPlan(RefuseAlice));

        var wrong = await SettleAsync(policy.CheckSshPasswordLoginAsync(new SshPasswordLogin("password", "alice", Encoding.UTF8.GetBytes(WrongPassword)), CancellationToken.None));
        var refused = await SettleAsync(policy.CheckSshPasswordLoginAsync(new SshPasswordLogin("password", "alice", Encoding.UTF8.GetBytes(AlicePassword)), CancellationToken.None));

        Assert.AreEqual((new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin("password", "alice", false)), true), wrong);
        Assert.AreEqual(wrong, refused);
        Assert.AreEqual(Judged("ssh", "alice", "password", false, true), events.Events[^1]);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_RefuseLogin_IsRefusedAsAWrongResponse()
    {
        var policy = Create(new SpendingFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "User" }), SmbUser, NtlmV1);
        var wrongResponse = (byte[])SmbNtResponse.Clone();
        wrongResponse[0] ^= 0x01;

        var wrong = await SettleAsync(policy.CheckSmbNtlmV1LoginAsync(Smb(wrongResponse), CancellationToken.None));
        var refused = await SettleAsync(policy.CheckSmbNtlmV1LoginAsync(Smb(), CancellationToken.None));

        Assert.AreEqual((new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "User", false)), true), wrong);
        Assert.AreEqual(wrong, refused);
        Assert.AreEqual(Judged("smb", "User", "ntlmv1", false, true), events.Events[^1]);
    }

    [TestMethod]
    public async Task CheckApopLoginAsync_RefuseLogin_IsRefusedAsAWrongDigest()
    {
        // RFC 1939, section 7: mrose, secret tanstaaf.
        var accounts = new AccountBook([new Account("mrose", "tanstaaf")]);
        var policy = Create(new SpendingFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "mrose" }), accounts, AuthenticationMethods.DefaultAccepted.Append(AuthenticationMethod.Apop).ToHashSet());
        const string Timestamp = "<1896.697170952@dbc.mtview.ca.us>";

        var wrong = await SettleAsync(policy.CheckApopLoginAsync(new ApopLogin("pop3", "mrose", Timestamp, "00000000000000000000000000000000", null), CancellationToken.None));
        var refused = await SettleAsync(policy.CheckApopLoginAsync(new ApopLogin("pop3", "mrose", Timestamp, "c4c9334bac560ecc979e58001b3e22fb", null), CancellationToken.None));

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, wrong.Result.Outcome);
        Assert.IsTrue(wrong.WasDelayed);
        Assert.AreEqual(wrong, refused);
        Assert.AreEqual(Judged("pop3", "mrose", "APOP", false, true), events.Events[^1]);
    }

    [TestMethod]
    public async Task JudgeAsync_HttpBasicRefuseLogin_IsChallengedAsAWrongPassword()
    {
        var policy = Create(new SpendingFaultPlan(RefuseAlice), httpMethods: [new BasicAuthenticationMethod(AliceAndBob)]);
        var session = policy.StartHttpConnection(PolicyFixture.Tls);

        var wrong = await SettleAsync(session.JudgeAsync(PolicyFixture.Get(Basic("alice", WrongPassword)), CancellationToken.None));
        var refused = await SettleAsync(session.JudgeAsync(PolicyFixture.Get(Basic("alice", AlicePassword)), CancellationToken.None));

        AssertSameVerdict(wrong, refused);
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, refused.Result.Outcome);
        Assert.IsNull(refused.Result.Refusal);
        CollectionAssert.AreEqual(
            new SurlEvent[] { Judged("http", "alice", "Basic", false), AppliedToAlice, Judged("http", "alice", "Basic", false, true) },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeAsync_SignatureVersion4RefuseLogin_IsRefusedAsABadSignature()
    {
        var accepted = new HttpCredentialCheck(HttpCredentialOutcome.Accepted, "alice", [], "alice");
        var policy = Create(new SpendingFaultPlan(RefuseAlice), acceptedMethods: EveryMethod, httpMethods: [new ScriptedHttpAuthenticationMethod(AuthenticationMethod.AwsSigV4, [], accepted)]);

        var (verdict, wasDelayed) = await SettleAsync(
            policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get("AWS4-HMAC-SHA256 x"), CancellationToken.None));

        Assert.IsTrue(wasDelayed);
        Assert.AreEqual(HttpAuthenticationRefusal.BadSignature, verdict.Refusal);
        Assert.IsNull(verdict.AccountName);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 alice", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task JudgeBodyAsync_RefuseLogin_RefusesTheSignedBodyAsABadSignature()
    {
        var accepted = new HttpCredentialCheck(HttpCredentialOutcome.Accepted, "alice", [], "alice");
        var awaitingBody = new HttpCredentialCheck(HttpCredentialOutcome.AwaitingBody, null, [], "alice", _ => accepted);
        var policy = Create(new SpendingFaultPlan(RefuseAlice), acceptedMethods: EveryMethod, httpMethods: [new ScriptedHttpAuthenticationMethod(AuthenticationMethod.AwsSigV4, [], awaitingBody)]);
        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Put("AWS4-HMAC-SHA256 x"), CancellationToken.None);

        var (bodyVerdict, wasDelayed) = await SettleAsync(verdict.BodyCheck!.JudgeBodyAsync(new byte[32], CancellationToken.None));

        Assert.IsTrue(wasDelayed);
        Assert.AreEqual(HttpAuthenticationRefusal.BadSignature, bodyVerdict.Refusal);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 alice", bodyVerdict.CheckedLogin?.Note);
        CollectionAssert.AreEqual(
            new SurlEvent[] { AppliedToAlice, Judged("http", "alice", "AWS4-HMAC-SHA256", false, true) },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task JudgeAsync_NtlmRefuseLogin_LeavesTheConnectionLoggedOut()
    {
        var accepted = new HttpCredentialCheck(HttpCredentialOutcome.Accepted, "alice", [], "alice");
        var policy = Create(
            new SpendingFaultPlan(RefuseAlice),
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Ntlm },
            httpMethods:
            [new ScriptedHttpAuthenticationMethod(AuthenticationMethod.Ntlm, ["NTLM"], accepted)]);
        var session = policy.StartHttpConnection(PolicyFixture.Tls);

        var (refused, _) = await SettleAsync(session.JudgeAsync(PolicyFixture.Get("NTLM x"), CancellationToken.None));
        var later = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, refused.Outcome);
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, later.Outcome);
        Assert.IsNull(later.AccountName);
    }

    [TestMethod]
    public async Task StartSaslExchange_PlainRefuseLogin_IsRefusedAsAWrongPassword()
    {
        var policy = Create(new SpendingFaultPlan(RefuseAlice));

        var wrong = await PlainAsync(policy, "alice", WrongPassword);
        var refused = await PlainAsync(policy, "alice", AlicePassword);

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, refused.Outcome);
        Assert.AreEqual(wrong, refused);
        CollectionAssert.AreEqual(
            new SurlEvent[] { Judged("smtp", "alice", "PLAIN", false), AppliedToAlice, Judged("smtp", "alice", "PLAIN", false, true) },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task StartSaslExchange_XOAuth2RefuseLogin_AnswersTheErrorChallengeAsAWrongToken()
    {
        var accounts = new AccountBook([new Account(string.Empty, "tok")]);
        var policy = Create(new SpendingFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "user" }), accounts, EveryMethod);
        var runner = new SaslExchangeRunner(clock);

        var wrong = await runner.RunAsync(policy.StartSaslExchange(new SaslExchangeStart("imap", "XOAUTH2", SaslExchangeRunner.XOAuth2("user", "bad"), PolicyFixture.Tls)), [0x01]);
        var refused = await runner.RunAsync(policy.StartSaslExchange(new SaslExchangeStart("imap", "XOAUTH2", SaslExchangeRunner.XOAuth2("user", "tok"), PolicyFixture.Tls)), [0x01]);

        Assert.AreEqual(SaslLoginOutcome.Challenge, refused[0].Outcome);
        Assert.AreEqual("{\"status\":\"invalid_token\"}", Encoding.ASCII.GetString(refused[0].Challenge.Span));
        Assert.AreEqual(wrong[0].CheckedLogin, refused[0].CheckedLogin);
        Assert.AreEqual(wrong[1], refused[1]);
        Assert.AreEqual(Judged("imap", "bearer token", "XOAUTH2", false, true), events.Events[^1]);
    }

    private static void AssertSameVerdict(
        (HttpAuthenticationVerdict Result, bool WasDelayed) expected, (HttpAuthenticationVerdict Result, bool WasDelayed) actual)
    {
        Assert.IsTrue(expected.WasDelayed);
        Assert.AreEqual(expected.WasDelayed, actual.WasDelayed);
        Assert.AreEqual(expected.Result.Outcome, actual.Result.Outcome);
        CollectionAssert.AreEqual(expected.Result.WwwAuthenticateValues.ToArray(), actual.Result.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(expected.Result.AccountName, actual.Result.AccountName);
        Assert.AreEqual(expected.Result.CheckedLogin, actual.Result.CheckedLogin);
        Assert.AreEqual(expected.Result.Refusal, actual.Result.Refusal);
    }
}
