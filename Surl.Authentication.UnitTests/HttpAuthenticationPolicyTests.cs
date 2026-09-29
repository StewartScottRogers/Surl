using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The HTTP sessions <see cref="AuthenticationPolicy.StartHttpConnection"/> starts, against
/// ADR-0032 section 4's steps, case by case, with scripted methods standing in for the
/// verifiers BL-111 to BL-122 build.
/// </summary>
[TestClass]
public sealed class HttpAuthenticationPolicyTests
{
    private static readonly HttpCredentialCheck Accepted = new(HttpCredentialOutcome.Accepted, "alice", []);
    private static readonly HttpCredentialCheck Refused = new(HttpCredentialOutcome.Refused, null, []);

    private readonly ManualTimeProvider clock = new();

    private static ScriptedHttpAuthenticationMethod Method(AuthenticationMethod method, HttpCredentialCheck check) =>
        new(method, [$"{method} realm=\"surl\""], check);

    private async Task<HttpAuthenticationVerdict> JudgeAsync(
        AuthenticationPolicy policy, TlsSession? tlsSession, HttpAuthenticationRequest request)
    {
        var judgement = policy.StartHttpConnection(tlsSession).JudgeAsync(request, CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await judgement;
    }

    private static void AssertVerdict(
        HttpAuthenticationOutcome outcome, string[] values, string? accountName, HttpAuthenticationVerdict verdict)
    {
        Assert.AreEqual(outcome, verdict.Outcome);
        CollectionAssert.AreEqual(values, verdict.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(accountName, verdict.AccountName);
    }

    [TestMethod]
    public async Task NoAccounts_AnonymousRead_Proceeds()
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock, httpMethods: Method(AuthenticationMethod.Digest, Refused));

        AssertVerdict(HttpAuthenticationOutcome.Proceed, [], null, await JudgeAsync(policy, null, PolicyFixture.Get()));
    }

    [TestMethod]
    public async Task NoAccounts_AnonymousWrite_IsChallenged()
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock, httpMethods: Method(AuthenticationMethod.Digest, Refused));

        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, ["Digest realm=\"surl\""], null,
            await JudgeAsync(policy, null, PolicyFixture.Put()));
    }

    [TestMethod]
    public async Task Accounts_AnonymousRead_IsChallenged()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: Method(AuthenticationMethod.Digest, Refused));

        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, ["Digest realm=\"surl\""], null,
            await JudgeAsync(policy, PolicyFixture.Tls, PolicyFixture.Get()));
    }

    [TestMethod]
    public async Task NoAccounts_Credentials_AreRefusedWithAChallenge()
    {
        var digest = Method(AuthenticationMethod.Digest, Refused);
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock, httpMethods: digest);

        var verdict = await JudgeAsync(policy, null, PolicyFixture.Get("Digest username=\"alice\""));

        AssertVerdict(HttpAuthenticationOutcome.Challenge, ["Digest realm=\"surl\""], null, verdict);
        CollectionAssert.AreEqual(new[] { "username=\"alice\"" }, digest.CredentialsVerified);
    }

    [TestMethod]
    public async Task AcceptedCredentials_ProceedAsTheAccountWithTheVerifiersValues()
    {
        var negotiate = new ScriptedHttpAuthenticationMethod(
            AuthenticationMethod.Negotiate, ["Negotiate"], new HttpCredentialCheck(HttpCredentialOutcome.Accepted, "alice", ["Negotiate final"]));
        var policy = PolicyFixture.Create(
            PolicyFixture.AliceAndToken, clock, acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Negotiate }, httpMethods: negotiate);

        var judgement = policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get("Negotiate abc"), CancellationToken.None);

        Assert.IsTrue(judgement.IsCompleted);
        AssertVerdict(HttpAuthenticationOutcome.Proceed, ["Negotiate final"], "alice", await judgement);
    }

    [TestMethod]
    public async Task ContinuationStep_IsChallengedWithItsOneValueUndelayed()
    {
        var ntlm = new ScriptedHttpAuthenticationMethod(
            AuthenticationMethod.Ntlm, ["NTLM"], new HttpCredentialCheck(HttpCredentialOutcome.Continue, null, ["NTLM type2"]));
        var policy = PolicyFixture.Create(
            PolicyFixture.AliceAndToken, clock, acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Ntlm }, httpMethods: ntlm);

        var judgement = policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get("NTLM type1"), CancellationToken.None);

        Assert.IsTrue(judgement.IsCompleted);
        AssertVerdict(HttpAuthenticationOutcome.Challenge, ["NTLM type2"], null, await judgement);
    }

    [TestMethod]
    public async Task ContinuationStepWithoutValue_IsAnsweredAsARefusal()
    {
        var ntlm = new ScriptedHttpAuthenticationMethod(
            AuthenticationMethod.Ntlm, ["NTLM"], new HttpCredentialCheck(HttpCredentialOutcome.Continue, null, []));
        var policy = PolicyFixture.Create(
            PolicyFixture.AliceAndToken, clock, acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Ntlm }, httpMethods: ntlm);

        AssertVerdict(HttpAuthenticationOutcome.Challenge, ["NTLM"], null, await JudgeAsync(policy, null, PolicyFixture.Get("NTLM type1")));
    }

    [TestMethod]
    public void Constructor_SameMethodTwice_Throws()
    {
        var settings = new AuthenticationSettings(PolicyFixture.NoAccounts, false, false, AuthenticationMethods.DefaultAccepted);

        Assert.ThrowsExactly<ArgumentException>(
            () => new AuthenticationPolicy(
                settings, [Method(AuthenticationMethod.Basic, Refused), Method(AuthenticationMethod.Basic, Accepted)], clock));
    }

    [TestMethod]
    public void Constructor_NullAcceptedMethods_Throws()
    {
        var settings = new AuthenticationSettings(PolicyFixture.NoAccounts, false, false, null!);

        Assert.ThrowsExactly<ArgumentNullException>(() => new AuthenticationPolicy(settings, [], clock));
    }

    [TestMethod]
    public async Task RefusedCredentials_WaitTheRefusalDelayThenChallenge()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: Method(AuthenticationMethod.Digest, Refused));

        var judgement = policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get("Digest x"), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(judgement.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        AssertVerdict(HttpAuthenticationOutcome.Challenge, ["Digest realm=\"surl\""], null, await judgement);
    }

    [TestMethod]
    [DataRow("Basic YWxpY2U6c2VjcmV0", DisplayName = "Basic")]
    [DataRow("bearer tok", DisplayName = "Bearer, scheme in lower case")]
    public async Task PlaintextSecretWithoutTls_IsForbiddenUnchecked(string authorization)
    {
        var basic = Method(AuthenticationMethod.Basic, Accepted);
        var bearer = Method(AuthenticationMethod.Bearer, Accepted);
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: [basic, bearer]);

        var judgement = policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get(authorization), CancellationToken.None);

        Assert.IsTrue(judgement.IsCompleted);
        AssertVerdict(HttpAuthenticationOutcome.Forbidden, [], null, await judgement);
        Assert.IsEmpty(basic.CredentialsVerified);
        Assert.IsEmpty(bearer.CredentialsVerified);
    }

    [TestMethod]
    public async Task PlaintextSecretWithoutTls_NoAccountsAndMethodNotAccepted_IsStillForbidden()
    {
        var policy = PolicyFixture.Create(
            PolicyFixture.NoAccounts, clock, acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Digest });

        AssertVerdict(
            HttpAuthenticationOutcome.Forbidden, [], null,
            await JudgeAsync(policy, null, PolicyFixture.Get("Basic YWxpY2U6c2VjcmV0")));
    }

    [TestMethod]
    public async Task PlaintextSecretWithoutTls_AllowPlaintextAuth_IsChecked()
    {
        var basic = Method(AuthenticationMethod.Basic, Accepted);
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, allowPlaintextAuth: true, httpMethods: basic);

        var verdict = await JudgeAsync(policy, null, PolicyFixture.Get("Basic YWxpY2U6c2VjcmV0"));

        AssertVerdict(HttpAuthenticationOutcome.Proceed, [], "alice", verdict);
        CollectionAssert.AreEqual(new[] { "YWxpY2U6c2VjcmV0" }, basic.CredentialsVerified);
    }

    [TestMethod]
    public async Task PlaintextSecretOverTls_IsChecked()
    {
        var basic = Method(AuthenticationMethod.Basic, Accepted);
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: basic);

        AssertVerdict(
            HttpAuthenticationOutcome.Proceed, [], "alice",
            await JudgeAsync(policy, PolicyFixture.Tls, PolicyFixture.Get("Basic YWxpY2U6c2VjcmV0")));
    }

    [TestMethod]
    public async Task Challenge_ListsAcceptedMethodsInAdrOrder_OfferingPlaintextOnlyOverTls()
    {
        var all = new HashSet<AuthenticationMethod>(Enum.GetValues<AuthenticationMethod>());
        IHttpAuthenticationMethod[] methods =
        [
            Method(AuthenticationMethod.Bearer, Refused),
            new ScriptedHttpAuthenticationMethod(AuthenticationMethod.AwsSigV4, [], Refused),
            Method(AuthenticationMethod.Basic, Refused),
            new ScriptedHttpAuthenticationMethod(AuthenticationMethod.Digest, ["Digest MD5", "Digest SHA-256", "Digest SHA-512-256"], Refused),
            Method(AuthenticationMethod.Ntlm, Refused),
            Method(AuthenticationMethod.Negotiate, Refused),
        ];
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, acceptedMethods: all, httpMethods: methods);

        var overTls = await JudgeAsync(policy, PolicyFixture.Tls, PolicyFixture.Get());
        var withoutTls = await JudgeAsync(policy, null, PolicyFixture.Get());

        string[] unencrypted =
            ["Negotiate realm=\"surl\"", "Ntlm realm=\"surl\"", "Digest MD5", "Digest SHA-256", "Digest SHA-512-256"];
        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, [.. unencrypted, "Basic realm=\"surl\"", "Bearer realm=\"surl\""], null, overTls);
        AssertVerdict(HttpAuthenticationOutcome.Challenge, unencrypted, null, withoutTls);
    }

    [TestMethod]
    public async Task Challenge_AllowPlaintextAuth_OffersBasicWithoutTls()
    {
        var policy = PolicyFixture.Create(
            PolicyFixture.AliceAndToken, clock, allowPlaintextAuth: true, httpMethods: Method(AuthenticationMethod.Basic, Refused));

        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, ["Basic realm=\"surl\""], null,
            await JudgeAsync(policy, null, PolicyFixture.Get()));
    }

    [TestMethod]
    public async Task Challenge_OnlyPlaintextMethodsWithoutTls_IsForbidden()
    {
        var policy = PolicyFixture.Create(
            PolicyFixture.AliceAndToken, clock,
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Basic },
            httpMethods: Method(AuthenticationMethod.Basic, Refused));

        AssertVerdict(HttpAuthenticationOutcome.Forbidden, [], null, await JudgeAsync(policy, null, PolicyFixture.Get()));
    }

    [TestMethod]
    public async Task Challenge_LeavesOutMethodsNotAccepted()
    {
        var ntlm = Method(AuthenticationMethod.Ntlm, Accepted);
        var policy = PolicyFixture.Create(
            PolicyFixture.AliceAndToken, clock, httpMethods: [ntlm, Method(AuthenticationMethod.Digest, Refused)]);

        var verdict = await JudgeAsync(policy, null, PolicyFixture.Get("NTLM type1"));

        AssertVerdict(HttpAuthenticationOutcome.Challenge, ["Digest realm=\"surl\""], null, verdict);
        Assert.AreEqual(0, ntlm.ConnectionsStarted);
    }

    [TestMethod]
    [DataRow("Foo bar", DisplayName = "unknown scheme")]
    [DataRow("Digest x", DisplayName = "accepted method not implemented in this build")]
    public async Task AuthorizationNoVerifierTakes_IsTreatedAsMissing(string authorization)
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock, httpMethods: Method(AuthenticationMethod.Bearer, Refused));

        AssertVerdict(HttpAuthenticationOutcome.Proceed, [], null, await JudgeAsync(policy, PolicyFixture.Tls, PolicyFixture.Get(authorization)));
        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, ["Bearer realm=\"surl\""], null,
            await JudgeAsync(policy, PolicyFixture.Tls, PolicyFixture.Put(authorization)));
    }

    [TestMethod]
    public async Task NoMethodsAtAll_LoginNeeded_IsForbidden()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        AssertVerdict(HttpAuthenticationOutcome.Forbidden, [], null, await JudgeAsync(policy, PolicyFixture.Tls, PolicyFixture.Get()));
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no credentials")]
    [DataRow("Basic d3Jvbmc6d3Jvbmc=", DisplayName = "Basic without TLS")]
    public async Task AllowAnonymous_ProceedsUnchecked(string? authorization)
    {
        var basic = Method(AuthenticationMethod.Basic, Refused);
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, allowAnonymous: true, httpMethods: basic);
        var request = authorization is null ? PolicyFixture.Put() : PolicyFixture.Put(authorization);

        var judgement = policy.StartHttpConnection(null).JudgeAsync(request, CancellationToken.None);

        Assert.IsTrue(judgement.IsCompleted);
        AssertVerdict(HttpAuthenticationOutcome.Proceed, [], null, await judgement);
        Assert.IsEmpty(basic.CredentialsVerified);
    }

    [TestMethod]
    public async Task EachConnection_StartsItsOwnVerifiers()
    {
        var digest = Method(AuthenticationMethod.Digest, Accepted);
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: digest);

        var session = policy.StartHttpConnection(null);
        await session.JudgeAsync(PolicyFixture.Get("Digest a"), CancellationToken.None);
        await session.JudgeAsync(PolicyFixture.Get("Digest b"), CancellationToken.None);
        policy.StartHttpConnection(PolicyFixture.Tls);

        Assert.AreEqual(2, digest.ConnectionsStarted);
    }

    [TestMethod]
    public async Task Authorization_FieldNameAndSchemeAnyCase_CredentialsAfterSpaces()
    {
        var digest = Method(AuthenticationMethod.Digest, Accepted);
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: digest);
        var request = new HttpAuthenticationRequest(
            "GET", "/", false,
            [
                new KeyValuePair<string, string>("authorization", "dIgEsT   a=1, b=2"),
                new KeyValuePair<string, string>("Authorization", "Digest second"),
            ]);

        await JudgeAsync(policy, null, request);
        await JudgeAsync(policy, null, PolicyFixture.Get("Digest"));

        CollectionAssert.AreEqual(new[] { "a=1, b=2", string.Empty }, digest.CredentialsVerified);
    }

    [TestMethod]
    public async Task RefusedCredentials_DelayIsCancelledWithTheToken()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: Method(AuthenticationMethod.Digest, Refused));
        using var cancellation = new CancellationTokenSource();

        var judgement = policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get("Digest x"), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await judgement);
    }

    private static HttpCredentialCheck AwaitingBody(Func<ReadOnlyMemory<byte>, HttpCredentialCheck>? checkBody) =>
        new(HttpCredentialOutcome.AwaitingBody, null, [], "alice", checkBody);

    private AuthenticationPolicy BodyBindingPolicy(HttpCredentialCheck check) => PolicyFixture.Create(
        PolicyFixture.AliceAndToken,
        clock,
        httpMethods: [new ScriptedHttpAuthenticationMethod(AuthenticationMethod.AwsSigV4, [], check), Method(AuthenticationMethod.Digest, Refused)]);

    [TestMethod]
    public async Task AwaitingBody_ProceedsWithABodyCheckAndNoLoginNoteYet()
    {
        var policy = BodyBindingPolicy(AwaitingBody(_ => Accepted with { UserAsSent = "alice" }));

        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Put("AWS4-HMAC-SHA256 x"), CancellationToken.None);

        AssertVerdict(HttpAuthenticationOutcome.Proceed, [], null, verdict);
        Assert.IsNull(verdict.CheckedLogin);
        Assert.IsNotNull(verdict.BodyCheck);
    }

    [TestMethod]
    public async Task AwaitingBody_BodyAccepted_ProceedsAsTheAccountWithTheLoginNote()
    {
        byte[]? checkedHash = null;
        var policy = BodyBindingPolicy(AwaitingBody(hash =>
        {
            checkedHash = hash.ToArray();

            return Accepted with { UserAsSent = "alice" };
        }));
        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Put("AWS4-HMAC-SHA256 x"), CancellationToken.None);

        var bodyVerdict = await verdict.BodyCheck!.JudgeBodyAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        AssertVerdict(HttpAuthenticationOutcome.Proceed, [], "alice", bodyVerdict);
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 alice", bodyVerdict.CheckedLogin?.Note);
        Assert.IsNull(bodyVerdict.BodyCheck);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, checkedHash);
    }

    [TestMethod]
    public async Task AwaitingBody_BodyRefused_IsChallengedAfterTheRefusalDelayWithTheLoginNote()
    {
        var policy = BodyBindingPolicy(AwaitingBody(_ => Refused with { UserAsSent = "alice" }));
        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Put("AWS4-HMAC-SHA256 x"), CancellationToken.None);

        var judgement = verdict.BodyCheck!.JudgeBodyAsync(new byte[32], CancellationToken.None).AsTask();
        Assert.IsFalse(judgement.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var bodyVerdict = await judgement;

        AssertVerdict(HttpAuthenticationOutcome.Challenge, ["Digest realm=\"surl\""], null, bodyVerdict);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 alice", bodyVerdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task AwaitingBody_BodyCheckCancelled_Throws()
    {
        var policy = BodyBindingPolicy(AwaitingBody(_ => Accepted));
        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Put("AWS4-HMAC-SHA256 x"), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await verdict.BodyCheck!.JudgeBodyAsync(new byte[32], new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task AwaitingBody_WithNothingToCheckTheBody_IsRefused()
    {
        var policy = BodyBindingPolicy(AwaitingBody(null));

        var verdict = await JudgeAsync(policy, null, PolicyFixture.Put("AWS4-HMAC-SHA256 x"));

        AssertVerdict(HttpAuthenticationOutcome.Challenge, ["Digest realm=\"surl\""], null, verdict);
        Assert.IsNull(verdict.BodyCheck);
    }

    [TestMethod]
    public async Task NullRequest_Throws()
    {
        var session = PolicyFixture.Create(PolicyFixture.NoAccounts, clock).StartHttpConnection(null);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await session.JudgeAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task CancelledToken_Throws()
    {
        var session = PolicyFixture.Create(PolicyFixture.NoAccounts, clock).StartHttpConnection(null);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await session.JudgeAsync(PolicyFixture.Get(), new CancellationToken(canceled: true)));
    }
}
