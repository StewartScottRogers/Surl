using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy.CheckPasswordLoginAsync"/> with an LDAP account source (ADR-0149,
/// decisions 2 and 6): the directory judges names the account book does not hold, a refusal costs what an
/// unknown <c>--user-file</c> account costs, and the methods the directory cannot serve never reach it.
/// </summary>
[TestClass]
public sealed class LdapPasswordLoginPolicyTests
{
    private const string Dn = "uid=carol,ou=people,dc=example,dc=com";

    private readonly ManualTimeProvider clock = new();
    private readonly ScriptedLdapDirectory directory = new();
    private readonly List<string> notes = [];

    private AuthenticationPolicy Policy(
        AccountBook? accounts = null, ISurlEventPublisher? events = null, IFaultPlan? faults = null, params IHttpAuthenticationMethod[] httpMethods)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldaps://dir.test/ou=people,dc=example,dc=com", out var url, out _));
        var source = new LdapAccountSource(new LdapAccountSourceSettings(url!), directory, clock, notes.Add);
        var settings = new AuthenticationSettings(
            accounts ?? PolicyFixture.AliceAndToken, false, false, AuthenticationMethods.DefaultAccepted)
        {
            LdapAccountSource = source,
        };

        return new AuthenticationPolicy(settings, httpMethods, clock, events, faults);
    }

    private static PasswordLogin Login(string? user, string? password, string scheme = "imap", TlsSession? tls = null) =>
        new(scheme, user, password is null ? null : Encoding.UTF8.GetBytes(password), tls ?? PolicyFixture.Tls);

    // Search (id 1) finds carol, then her bind (id 2) answers with the code.
    private void DirectoryAnswersTo(int bindCode)
    {
        directory.Answers.Clear();
        directory.Answers.Add(LdapTestMessages.SearchEntry(1, Dn));
        directory.Answers.Add(LdapTestMessages.SearchDone(1, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(2, bindCode));
    }

    [TestMethod]
    public async Task NameTheBookDoesNotHold_IsAcceptedWhenTheDirectoryAcceptsIt_WithoutDelay()
    {
        DirectoryAnswersTo(0);

        var check = Policy().CheckPasswordLoginAsync(Login("carol", "dirpw"), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        Assert.AreEqual(PasswordLoginVerdict.Accepted, await check);
        Assert.AreEqual(1, directory.ConnectCount);
        Assert.IsEmpty(notes);
    }

    [TestMethod]
    public async Task NameTheBookDoesNotHold_IsRefusedWhenTheDirectoryRefusesIt_AfterTheSameDelayAsAnUnknownAccount()
    {
        DirectoryAnswersTo(49);
        var withDirectory = Policy().CheckPasswordLoginAsync(Login("carol", "wrong"), CancellationToken.None).AsTask();
        var withoutDirectory = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock)
            .CheckPasswordLoginAsync(Login("carol", "wrong"), CancellationToken.None).AsTask();

        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(withDirectory.IsCompleted);
        Assert.IsFalse(withoutDirectory.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await withoutDirectory);
        Assert.AreEqual(await withoutDirectory, await withDirectory);
        Assert.AreEqual("Login refused: password carol (directory: wrong password)", notes.Single());
    }

    [TestMethod]
    public async Task DirectoryThatTimesOut_IsRefusedTheSameWay_AndNoted_WithNoDelayOnTop()
    {
        directory.Hangs = true;
        var check = Policy().CheckPasswordLoginAsync(Login("carol", "pw"), CancellationToken.None).AsTask();

        clock.Advance(TimeSpan.FromSeconds(LdapAccountSourceSettings.DefaultTimeoutSeconds));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await check);
        Assert.AreEqual("Login refused: password carol (directory: timed out after 5 s)", notes.Single());
    }

    [TestMethod]
    public async Task QuickDirectoryRefusal_WaitsOnlyTheRestOfTheSecond_CountedFromTheCheckStart()
    {
        DirectoryAnswersTo(49);
        directory.Gate = new TaskCompletionSource();
        var check = Policy().CheckPasswordLoginAsync(Login("carol", "wrong"), CancellationToken.None).AsTask();

        clock.Advance(TimeSpan.FromMilliseconds(400));
        directory.Gate.SetResult();
        await WaitForRefusalDelayAsync();
        clock.Advance(TimeSpan.FromMilliseconds(599));
        Assert.IsFalse(check.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await check);
    }

    [TestMethod]
    public async Task SlowDirectoryRefusal_IsAnsweredAtOnce_WhenASecondHasPassedSinceTheStart()
    {
        DirectoryAnswersTo(49);
        directory.Gate = new TaskCompletionSource();
        var check = Policy().CheckPasswordLoginAsync(Login("carol", "wrong"), CancellationToken.None).AsTask();

        clock.Advance(TimeSpan.FromSeconds(2));
        directory.Gate.SetResult();

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await check);
    }

    [TestMethod]
    public async Task NameTheBookHolds_IsJudgedByTheBookAlone_AndNeverSentToTheDirectory()
    {
        DirectoryAnswersTo(0);
        var check = Policy().CheckPasswordLoginAsync(Login("alice", "not-her-password"), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await check);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    [TestMethod]
    public async Task NameTheBookHoldsForOtherSchemesOnly_IsStillNeverSentToTheDirectory()
    {
        DirectoryAnswersTo(0);
        var accounts = new AccountBook([new Account("alice", "secret") { Schemes = new HashSet<string> { "imap" } }]);
        var view = accounts.ForScheme("mqtt");
        var check = Policy(view).CheckPasswordLoginAsync(Login("alice", "x", "mqtt"), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await check);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    [TestMethod]
    [DataRow(null, "pw", DisplayName = "no user name")]
    [DataRow("", "pw", DisplayName = "the empty name of a bearer token")]
    [DataRow("carol", null, DisplayName = "no password")]
    public async Task LoginWithoutANameOrPassword_IsNeverSentToTheDirectory(string? user, string? password)
    {
        DirectoryAnswersTo(0);
        var check = Policy().CheckPasswordLoginAsync(Login(user, password), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreNotEqual(PasswordLoginVerdict.Accepted, await check);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    [TestMethod]
    public async Task PlaintextSecretWithoutTls_IsRefusedBeforeTheDirectoryIsAsked()
    {
        DirectoryAnswersTo(0);
        var login = new PasswordLogin("imap", "carol", Encoding.UTF8.GetBytes("pw"), null);

        var verdict = await Policy().CheckPasswordLoginAsync(login, CancellationToken.None);

        Assert.AreEqual(PasswordLoginVerdict.RefusedPlaintext, verdict);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    [TestMethod]
    public async Task AcceptedDirectoryLogin_IsPublishedAndOfferedToTheFaultPlan_AsAnyAccountIs()
    {
        DirectoryAnswersTo(0);
        var events = new RecordingSurlEventPublisher();
        var policy = Policy(events: events, faults: new SpendingFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "carol" }));

        var check = policy.CheckPasswordLoginAsync(Login("carol", "dirpw"), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await check);
        var judged = events.Events.OfType<LoginJudgedEvent>().Single();
        Assert.AreEqual("carol", judged.Account);
        Assert.IsTrue(judged.IsRefusedByFault);
        Assert.IsFalse(judged.IsAccepted);
    }

    [TestMethod]
    public async Task BearerToken_IsNeverSentToTheDirectory()
    {
        DirectoryAnswersTo(0);
        var method = new BearerAuthenticationMethod(PolicyFixture.AliceAndToken);
        var verdict = await HttpVerdictAsync("Bearer carol-token", method);

        Assert.AreEqual(0, directory.ConnectCount);
        Assert.IsNotNull(verdict);
    }

    [TestMethod]
    public async Task Digest_IsNeverSentToTheDirectory()
    {
        DirectoryAnswersTo(0);
        var method = new DigestAuthenticationMethod(PolicyFixture.AliceAndToken, clock);
        var authorization = "Digest username=\"carol\", realm=\"surl\", nonce=\"x\", uri=\"/x\", response=\"00\"";
        var verdict = await HttpVerdictAsync(authorization, method);

        Assert.AreEqual(0, directory.ConnectCount);
        Assert.IsNotNull(verdict);
    }

    [TestMethod]
    public async Task CramMd5_IsNeverSentToTheDirectory()
    {
        DirectoryAnswersTo(0);
        var policy = Policy();
        var runner = new SaslExchangeRunner(clock);
        var exchange = SaslExchangeRunner.Start(policy, "CRAM-MD5", null, PolicyFixture.Tls);

        var steps = await runner.RunAsync(exchange, Encoding.ASCII.GetBytes("carol 00000000000000000000000000000000"));

        Assert.AreNotEqual(SaslLoginOutcome.Accepted, steps[^1].Outcome);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    private async Task<HttpAuthenticationVerdict> HttpVerdictAsync(string authorization, IHttpAuthenticationMethod method)
    {
        var judgement = Policy(httpMethods: method).StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(authorization), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await judgement;
    }

    // The directory has refused (its timeout timer is gone and the refusal noted) once only the refusal delay's timer is pending.
    private async Task WaitForRefusalDelayAsync()
    {
        while (notes.Count == 0 || clock.PendingTimerCount != 1)
        {
            await Task.Yield();
        }
    }
}
