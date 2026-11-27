using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// What abandoning a body check publishes (ADR-0045 amendment 1, decision 2), how a directory
/// account source changes the answer, and the challenges a session names on its own.
/// </summary>
[TestClass]
public sealed class HttpAuthenticationSessionTests
{
    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy BasicDigestAndBearerPolicy() =>
        PolicyFixture.Create(
            PolicyFixture.AliceAndToken,
            clock,
            httpMethods:
            [
                new BasicAuthenticationMethod(PolicyFixture.AliceAndToken),
                new DigestAuthenticationMethod(PolicyFixture.AliceAndToken, new FixedDigestNonceBook(FixedDigestNonceBook.FixtureNonce)),
                new BearerAuthenticationMethod(PolicyFixture.AliceAndToken),
            ]);

    [TestMethod]
    public async Task CreateChallenges_OverTls_MatchesTheChallengeVerdict()
    {
        var session = BasicDigestAndBearerPolicy().StartHttpConnection(PolicyFixture.Tls);
        var verdict = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        var challenges = session.CreateChallenges();

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(verdict.WwwAuthenticateValues.ToArray(), challenges.ToArray());
        Assert.IsTrue(challenges.Any(value => value.StartsWith("Basic ", StringComparison.Ordinal)));
        Assert.IsTrue(challenges.Any(value => value.StartsWith("Digest ", StringComparison.Ordinal)));
        Assert.IsTrue(challenges.Any(value => value.StartsWith("Bearer ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CreateChallenges_OverPlainHttp_OmitsBasicAsTheChallengeVerdictDoes()
    {
        var session = BasicDigestAndBearerPolicy().StartHttpConnection(null);
        var verdict = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        var challenges = session.CreateChallenges();

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(verdict.WwwAuthenticateValues.ToArray(), challenges.ToArray());
        Assert.IsFalse(challenges.Any(value => value.StartsWith("Basic ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateChallenges_WithNoHttpMethod_IsEmpty()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        var challenges = policy.StartHttpConnection(PolicyFixture.Tls).CreateChallenges();

        Assert.IsEmpty(challenges);
    }

    [TestMethod]
    public async Task CreateChallenges_AfterAnAcceptedBearerLogin_StillOffersBearer()
    {
        var session = BasicDigestAndBearerPolicy().StartHttpConnection(PolicyFixture.Tls);
        var verdict = await session.JudgeAsync(PolicyFixture.Get("Bearer tok"), CancellationToken.None);

        var challenges = session.CreateChallenges();

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        CollectionAssert.Contains(challenges.ToArray(), "Bearer realm=\"surl\"");
    }

    private static HttpCredentialCheck Awaiting(string? verifiedAccountName) =>
        new(HttpCredentialOutcome.AwaitingBody, verifiedAccountName, [], "alice", _ => new HttpCredentialCheck(HttpCredentialOutcome.Accepted, "alice", []));

    private async Task<IHttpRequestBodyCheck> StartAsync(HttpCredentialCheck check, RecordingSurlEventPublisher events, IFaultPlan? faults = null)
    {
        var policy = PolicyFixture.CreateObserved(
            PolicyFixture.AliceAndToken,
            clock,
            events,
            faults ?? new ScriptedFaultPlan(),
            null,
            new ScriptedHttpAuthenticationMethod(AuthenticationMethod.AwsSigV4, [], check));
        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Put("AWS4-HMAC-SHA256 x"), CancellationToken.None);

        return verdict.BodyCheck!;
    }

    [TestMethod]
    public async Task AbandonAsync_WithASentContentHash_PublishesAnAcceptedLogin()
    {
        var events = new RecordingSurlEventPublisher();
        var bodyCheck = await StartAsync(Awaiting("alice"), events);

        await bodyCheck.AbandonAsync(CancellationToken.None);

        Assert.AreEqual(new LoginJudgedEvent(null, "http", "alice", "AWS4-HMAC-SHA256", true, false), events.Events.Single());
    }

    [TestMethod]
    public async Task AbandonAsync_WithoutASentContentHash_PublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var bodyCheck = await StartAsync(Awaiting(null), events);

        await bodyCheck.AbandonAsync(CancellationToken.None);

        Assert.AreEqual(0, events.Events.OfType<LoginJudgedEvent>().Count());
    }

    [TestMethod]
    public async Task AbandonAsync_ForAFaultedAccount_PublishesARefusedLogin()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "alice" });
        var bodyCheck = await StartAsync(Awaiting("alice"), events, plan);

        await bodyCheck.AbandonAsync(CancellationToken.None);

        Assert.AreEqual(new LoginJudgedEvent(null, "http", "alice", "AWS4-HMAC-SHA256", false, true), events.Events.OfType<LoginJudgedEvent>().Single());
    }
    private AuthenticationPolicy DirectoryOnlyPolicy(string[]? protocols, bool allowAnonymous = false)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldap://dir.test/ou=people,dc=example,dc=com", out var url, out _));
        var source = new LdapAccountSource(new LdapAccountSourceSettings(url!) { Protocols = protocols }, new ScriptedLdapDirectory(), clock);
        var settings = new AuthenticationSettings(PolicyFixture.NoAccounts, allowAnonymous, false, AuthenticationMethods.DefaultAccepted)
        {
            LdapAccountSource = source,
        };

        return new AuthenticationPolicy(settings, [new BasicAuthenticationMethod(PolicyFixture.NoAccounts)], clock);
    }

    private static HttpAuthenticationRequest Head() =>
        new("HEAD", "/x", false, [new KeyValuePair<string, string>("Host", "localhost")]);

    [TestMethod]
    [DataRow("GET")]
    [DataRow("HEAD")]
    public async Task JudgeAsync_DirectoryOnlyWithoutCredentials_ChallengesReads(string method)
    {
        var policy = DirectoryOnlyPolicy(null);
        var request = method == "GET" ? PolicyFixture.Get() : Head();

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
    }

    [TestMethod]
    public async Task JudgeAsync_DirectoryNotServingHttp_StillServesReadsAnonymously()
    {
        var policy = DirectoryOnlyPolicy(["ftp"]);

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
    }

    [TestMethod]
    public async Task JudgeAsync_DirectoryNamingHttpsOnly_ChallengesReads()
    {
        var policy = DirectoryOnlyPolicy(["https"]);

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
    }

    [TestMethod]
    [DataRow("ws", "ws", DisplayName = "protocols=ws on the ws view")]
    [DataRow("wss", "ws", DisplayName = "protocols=wss on the ws view")]
    [DataRow("rtsp", "rtsp", DisplayName = "protocols=rtsp on the rtsp view")]
    public async Task JudgeAsync_DirectoryNamingTheViewsScheme_ChallengesReads(string protocol, string viewScheme)
    {
        var policy = DirectoryOnlyViewPolicy([protocol], viewScheme, new ScriptedLdapDirectory());

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
    }

    [TestMethod]
    [DataRow("https", "ws", 0, DisplayName = "protocols=https leaves ws out")]
    [DataRow("http", "rtsp", 0, DisplayName = "protocols=http leaves rtsp out")]
    [DataRow("ws", "ws", 1, DisplayName = "protocols=ws admits ws")]
    [DataRow("https", "http", 1, DisplayName = "protocols=https admits http's twin")]
    public async Task JudgeAsync_BasicLogin_AsksTheDirectoryOnlyOnASchemeItServes(string protocol, string viewScheme, int connections)
    {
        var directory = new ScriptedLdapDirectory();
        var policy = DirectoryOnlyViewPolicy([protocol], viewScheme, directory);
        var basic = "Basic " + Convert.ToBase64String("alice:wonderland"u8);

        var judging = policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(basic), CancellationToken.None);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        await judging;

        Assert.AreEqual(connections, directory.ConnectCount);
    }

    private AuthenticationPolicy DirectoryOnlyViewPolicy(string[] protocols, string viewScheme, ScriptedLdapDirectory directory)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldap://dir.test/ou=people,dc=example,dc=com", out var url, out _));
        var source = new LdapAccountSource(new LdapAccountSourceSettings(url!) { Protocols = protocols }, directory, clock);
        var settings = new AuthenticationSettings(PolicyFixture.NoAccounts, false, false, AuthenticationMethods.DefaultAccepted)
        {
            LdapAccountSource = source,
        }.ForScheme(viewScheme);

        return new AuthenticationPolicy(settings, [new BasicAuthenticationMethod(settings.Accounts)], clock);
    }

    [TestMethod]
    public async Task JudgeAsync_DirectoryWithAllowAnonymous_StillProceeds()
    {
        var policy = DirectoryOnlyPolicy(null, allowAnonymous: true);

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
    }

    private AuthenticationPolicy NoAccountPolicy(
        bool requiresLogin, bool allowAnonymous, params AuthenticationMethod[] offered)
    {
        var settings = new AuthenticationSettings(PolicyFixture.NoAccounts, allowAnonymous, false, AuthenticationMethods.DefaultAccepted)
        {
            RequiresLoginWithoutAccounts = requiresLogin,
        };
        var methods = offered
            .Select(method => new ScriptedHttpAuthenticationMethod(
                method, [$"{method} realm=\"surl\""], new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, [])))
            .ToArray<IHttpAuthenticationMethod>();

        return new AuthenticationPolicy(settings, methods, clock);
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("HEAD")]
    public async Task JudgeAsync_RequiringLoginWithNoAccount_ChallengesAReadWithoutCredentials(string method)
    {
        var policy = NoAccountPolicy(true, false, AuthenticationMethod.Basic, AuthenticationMethod.Digest);
        var request = method == "GET" ? PolicyFixture.Get() : Head();

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(
            new[] { "Digest realm=\"surl\"", "Basic realm=\"surl\"" }, verdict.WwwAuthenticateValues.ToArray());
        Assert.IsNull(verdict.CheckedLogin);
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("HEAD")]
    public async Task JudgeAsync_RequiringLoginWithNoMethodToOffer_ForbidsAReadWithoutCredentials(string method)
    {
        var policy = NoAccountPolicy(true, false, AuthenticationMethod.Basic);
        var request = method == "GET" ? PolicyFixture.Get() : Head();

        var verdict = await policy.StartHttpConnection(null).JudgeAsync(request, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Forbidden, verdict.Outcome);
        Assert.IsEmpty(verdict.WwwAuthenticateValues);
    }

    [TestMethod]
    public async Task JudgeAsync_RequiringLoginWithAllowAnonymous_StillProceeds()
    {
        var policy = NoAccountPolicy(true, true, AuthenticationMethod.Digest);

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsNull(verdict.AccountName);
    }

    [TestMethod]
    public async Task JudgeAsync_RequiringLoginOnAConnectionNtlmLoggedIn_StillProceedsAsTheAccount()
    {
        var accounts = new AccountBook([new Account("tester", "secret")]);
        var settings = new AuthenticationSettings(accounts, false, false, new HashSet<AuthenticationMethod> { AuthenticationMethod.Ntlm })
        {
            RequiresLoginWithoutAccounts = true,
        };
        var session = new AuthenticationPolicy(
            settings, [new NtlmAuthenticationMethod(accounts, new FixedNtlmServerChallengeSource())], clock)
            .StartHttpConnection(null);
        await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm", 1), CancellationToken.None);
        await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm", 2), CancellationToken.None);
        accounts.Remove("tester");

        var verdict = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.IsFalse(accounts.HasAccounts);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual("tester", verdict.AccountName);
    }

    [TestMethod]
    public async Task JudgeAsync_NotRequiringLoginWithNoAccount_ProceedsOnAReadWithoutCredentials()
    {
        var policy = NoAccountPolicy(false, false, AuthenticationMethod.Digest);

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsNull(verdict.AccountName);
    }
}
