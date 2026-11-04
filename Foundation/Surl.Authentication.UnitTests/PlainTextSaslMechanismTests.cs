using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/>'s SASL <c>PLAIN</c>, <c>LOGIN</c>, <c>XOAUTH2</c> and
/// <c>OAUTHBEARER</c> exchanges against ADR-0049 sections 1, 5 and 7, with the responses
/// upstream curl 8.21.0 sent when ADR-0049 measured it (<c>-u user:secret</c>,
/// <c>--oauth2-bearer tok</c>).
/// </summary>
[TestClass]
public sealed class PlainTextSaslMechanismTests
{
    // ADR-0049, "What curl sends for each mechanism", base64 as recorded.
    private const string CurlPlain = "AHVzZXIAc2VjcmV0";
    private const string CurlPlainWithAuthzidBoss = "Ym9zcwB1c2VyAHNlY3JldA==";
    private const string CurlLoginUser = "dXNlcg==";
    private const string CurlLoginPassword = "c2VjcmV0";
    private const string CurlXOAuth2 = "dXNlcj11c2VyAWF1dGg9QmVhcmVyIHRvawEB";
    private const string CurlOAuthBearer = "bixhPXVzZXIsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgwMjUBYXV0aD1CZWFyZXIgdG9rAQE=";

    private static readonly byte[] OAuthBearerFinalResponse = [0x01];

    private readonly ManualTimeProvider clock = new();
    private readonly SaslExchangeRunner runner;

    public PlainTextSaslMechanismTests()
    {
        runner = new SaslExchangeRunner(clock);
    }

    private AuthenticationPolicy Policy(
        AccountBook? accounts = null,
        bool allowAnonymous = false,
        bool allowPlaintextAuth = false,
        IReadOnlySet<AuthenticationMethod>? acceptedMethods = null) =>
        PolicyFixture.Create(accounts ?? SaslExchangeRunner.UserAndToken, clock, allowAnonymous, allowPlaintextAuth, acceptedMethods);

    private static void AssertStep(
        SaslLoginOutcome outcome, string challenge, string? accountName, string? note, SaslLoginStep step)
    {
        Assert.AreEqual(outcome, step.Outcome);
        Assert.AreEqual(challenge, Encoding.Latin1.GetString(step.Challenge.Span));
        Assert.AreEqual(accountName, step.AccountName);
        Assert.AreEqual(note, step.CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow("PLAIN", CurlPlain, null, "user", "Login accepted: PLAIN user", DisplayName = "PLAIN")]
    [DataRow("LOGIN", CurlLoginUser, CurlLoginPassword, "user", "Login accepted: LOGIN user", DisplayName = "LOGIN")]
    [DataRow("XOAUTH2", CurlXOAuth2, null, "user", "Login accepted: XOAUTH2 bearer token", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", CurlOAuthBearer, null, "user", "Login accepted: OAUTHBEARER bearer token", DisplayName = "OAUTHBEARER")]
    public async Task CurlsLoginOverTls_IsAcceptedUndelayedWithTheNote(
        string mechanism, string initialResponse, string? secondResponse, string accountName, string note)
    {
        var exchange = SaslExchangeRunner.Start(Policy(), mechanism, Convert.FromBase64String(initialResponse), PolicyFixture.Tls);

        var first = exchange.BeginAsync(CancellationToken.None);
        Assert.IsTrue(first.IsCompleted);
        var last = secondResponse is null
            ? await first
            : await exchange.ContinueAsync(Convert.FromBase64String(secondResponse), CancellationToken.None);

        AssertStep(SaslLoginOutcome.Accepted, string.Empty, accountName, note, last);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task NoInitialResponse_IsAskedWithOneEmptyChallenge(string mechanism)
    {
        var (response, _) = SaslExchangeRunner.Login(mechanism, "user", mechanism == "PLAIN" ? "secret" : "tok");
        var exchange = SaslExchangeRunner.Start(Policy(), mechanism, null, PolicyFixture.Tls);

        var steps = await runner.RunAsync(exchange, response);

        AssertStep(SaslLoginOutcome.Challenge, string.Empty, null, null, steps[0]);
        Assert.AreEqual(SaslLoginOutcome.Accepted, steps[1].Outcome);
    }

    [TestMethod]
    public async Task Login_WithoutInitialResponse_AsksUsernameThenPassword()
    {
        var exchange = SaslExchangeRunner.Start(Policy(), "LOGIN", null, PolicyFixture.Tls);

        var steps = await runner.RunAsync(
            exchange, Convert.FromBase64String(CurlLoginUser), Convert.FromBase64String(CurlLoginPassword));

        // VXNlcm5hbWU6 and UGFzc3dvcmQ6, the challenges curl was measured answering.
        Assert.AreEqual("VXNlcm5hbWU6", Convert.ToBase64String(steps[0].Challenge.Span));
        Assert.AreEqual("UGFzc3dvcmQ6", Convert.ToBase64String(steps[1].Challenge.Span));
        AssertStep(SaslLoginOutcome.Accepted, string.Empty, "user", "Login accepted: LOGIN user", steps[2]);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("LOGIN", DisplayName = "LOGIN")]
    public async Task WrongPasswordUnknownUserAndNoAccounts_AreRefusedAlike(string mechanism)
    {
        var wrongPassword = await RefuseAsync(Policy(), mechanism, "user", "wrong");
        var unknownUser = await RefuseAsync(Policy(), mechanism, "bob", "secret");
        var noAccounts = await RefuseAsync(Policy(PolicyFixture.NoAccounts), mechanism, "user", "secret");

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, $"Login refused: {mechanism} user", wrongPassword);
        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, $"Login refused: {mechanism} bob", unknownUser);
        Assert.AreEqual(wrongPassword, noAccounts);
    }

    private async Task<SaslLoginStep> RefuseAsync(AuthenticationPolicy policy, string mechanism, string user, string secret)
    {
        var (initialResponse, responses) = SaslExchangeRunner.Login(mechanism, user, secret);
        var steps = await runner.RunAsync(SaslExchangeRunner.Start(policy, mechanism, initialResponse, PolicyFixture.Tls), responses);

        return steps[^1];
    }

    [TestMethod]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task WrongTokenAndNoAccounts_GetTheErrorChallengeThenTheRefusalAlike(string mechanism)
    {
        var (wrongToken, _) = SaslExchangeRunner.Login(mechanism, "user", "wrong");
        var (rightToken, _) = SaslExchangeRunner.Login(mechanism, "user", "tok");

        var refused = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), mechanism, wrongToken, PolicyFixture.Tls), OAuthBearerFinalResponse);
        var noAccounts = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(PolicyFixture.NoAccounts), mechanism, rightToken, PolicyFixture.Tls), OAuthBearerFinalResponse);

        AssertStep(
            SaslLoginOutcome.Challenge, "{\"status\":\"invalid_token\"}", null, $"Login refused: {mechanism} bearer token", refused[0]);
        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, null, refused[1]);
        AssertStep(
            SaslLoginOutcome.Challenge, "{\"status\":\"invalid_token\"}", null, $"Login refused: {mechanism} bearer token", noAccounts[0]);
        Assert.AreEqual(refused[1], noAccounts[1]);
    }

    [TestMethod]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task BearerMechanisms_DoNotMatchTheUserName(string mechanism)
    {
        var (response, _) = SaslExchangeRunner.Login(mechanism, "anyone", "tok");

        var steps = await runner.RunAsync(SaslExchangeRunner.Start(Policy(), mechanism, response, PolicyFixture.Tls));

        Assert.AreEqual(SaslLoginOutcome.Accepted, steps[0].Outcome);
    }

    [TestMethod]
    public async Task ResponseAfterTheErrorChallenge_IsRefusedUndelayed()
    {
        var (wrongToken, _) = SaslExchangeRunner.Login("OAUTHBEARER", "user", "wrong");
        var exchange = SaslExchangeRunner.Start(Policy(), "OAUTHBEARER", wrongToken, PolicyFixture.Tls);
        await runner.RunAsync(exchange);

        var last = exchange.ContinueAsync(OAuthBearerFinalResponse, CancellationToken.None);

        Assert.IsTrue(last.IsCompleted);
        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, (await last).Outcome);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("LOGIN", DisplayName = "LOGIN")]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task RefusedCredentials_WaitTheRefusalDelayOnTheClock(string mechanism)
    {
        var pending = await StartCheckingStepAsync(mechanism, CancellationToken.None);

        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(pending.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.IsFalse((await pending).CheckedLogin!.IsAccepted);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("LOGIN", DisplayName = "LOGIN")]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task RefusalDelay_IsCancelledWithTheToken(string mechanism)
    {
        using var cancellation = new CancellationTokenSource();

        var pending = await StartCheckingStepAsync(mechanism, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
    }

    // The step that checks a wrong secret, started and left waiting out the refusal delay.
    private async Task<Task<SaslLoginStep>> StartCheckingStepAsync(string mechanism, CancellationToken cancellationToken)
    {
        var (initialResponse, responses) = SaslExchangeRunner.Login(mechanism, "user", "wrong");
        var exchange = SaslExchangeRunner.Start(Policy(), mechanism, initialResponse, PolicyFixture.Tls);
        var pending = exchange.BeginAsync(cancellationToken).AsTask();
        foreach (var response in responses)
        {
            await pending;
            pending = exchange.ContinueAsync(response, cancellationToken).AsTask();
        }

        return pending;
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("LOGIN", DisplayName = "LOGIN")]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task PlaintextMechanismWithoutTls_IsRefusedBeforeAnyCredentialIsRead(string mechanism)
    {
        var comparer = new CountingSecretComparer();
        var accounts = new AccountBook([new Account("user", "secret"), new Account(string.Empty, "tok")], comparer);
        var (initialResponse, _) = SaslExchangeRunner.Login(mechanism, "user", mechanism is "PLAIN" or "LOGIN" ? "secret" : "tok");

        var withInitialResponse = SaslExchangeRunner.Start(Policy(accounts), mechanism, initialResponse, null).BeginAsync(CancellationToken.None);
        var withoutInitialResponse = SaslExchangeRunner.Start(Policy(accounts), mechanism, null, null).BeginAsync(CancellationToken.None);

        Assert.IsTrue(withInitialResponse.IsCompleted);
        AssertStep(SaslLoginOutcome.RefusedPlaintext, string.Empty, null, null, await withInitialResponse);
        AssertStep(SaslLoginOutcome.RefusedPlaintext, string.Empty, null, null, await withoutInitialResponse);
        Assert.AreEqual(0, comparer.Comparisons.Count);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("LOGIN", DisplayName = "LOGIN")]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task AllowPlaintextAuth_AcceptsThePlaintextMechanismWithoutTls(string mechanism)
    {
        var (initialResponse, responses) = SaslExchangeRunner.Login(mechanism, "user", mechanism is "PLAIN" or "LOGIN" ? "secret" : "tok");

        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(allowPlaintextAuth: true), mechanism, initialResponse, null), responses);

        Assert.AreEqual(SaslLoginOutcome.Accepted, steps[^1].Outcome);
    }

    [TestMethod]
    [DataRow("PLAIN", 1, DisplayName = "PLAIN")]
    [DataRow("LOGIN", 2, DisplayName = "LOGIN")]
    [DataRow("XOAUTH2", 1, DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", 1, DisplayName = "OAUTHBEARER")]
    public async Task AllowAnonymous_RunsEveryStepAndAcceptsUncheckedWithoutTlsOrAccounts(string mechanism, int responsesRead)
    {
        byte[] nonsense = [0xFF, 0x00];
        var policy = Policy(PolicyFixture.NoAccounts, allowAnonymous: true);

        var withInitialResponse = await runner.RunAsync(SaslExchangeRunner.Start(policy, mechanism, nonsense, null), nonsense, nonsense);
        var withoutInitialResponse = await runner.RunAsync(SaslExchangeRunner.Start(policy, mechanism, null, null), nonsense, nonsense, nonsense);

        Assert.AreEqual(responsesRead, withInitialResponse.Count);
        Assert.AreEqual(responsesRead + 1, withoutInitialResponse.Count);
        AssertStep(SaslLoginOutcome.AcceptedUnchecked, string.Empty, null, null, withInitialResponse[^1]);
        AssertStep(SaslLoginOutcome.AcceptedUnchecked, string.Empty, null, null, withoutInitialResponse[^1]);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("user", DisplayName = "no NUL")]
    [DataRow("\0user", DisplayName = "one NUL")]
    [DataRow("\0user\0sec\0ret", DisplayName = "three NULs")]
    [DataRow("\0\xFF\0secret", DisplayName = "authcid not UTF-8")]
    [DataRow("\xFF\0user\0secret", DisplayName = "authzid not UTF-8")]
    public async Task Plain_MalformedResponse_IsRefusedWithNoUserInTheNote(string latin1Response)
    {
        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), "PLAIN", Encoding.Latin1.GetBytes(latin1Response), PolicyFixture.Tls));

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: PLAIN", steps[0]);
    }

    [TestMethod]
    public async Task Plain_EmptyAuthcid_IsRefusedWithNoUserInTheNote()
    {
        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), "PLAIN", SaslExchangeRunner.Plain(string.Empty, string.Empty, "tok"), PolicyFixture.Tls));

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: PLAIN", steps[0]);
    }

    [TestMethod]
    public async Task Plain_AuthzidOfAnotherUser_IsRefusedAsCurlsBossLogin()
    {
        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), "PLAIN", Convert.FromBase64String(CurlPlainWithAuthzidBoss), PolicyFixture.Tls));

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: PLAIN user", steps[0]);
    }

    [TestMethod]
    public async Task Plain_AuthzidEqualToAuthcid_IsAccepted()
    {
        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), "PLAIN", SaslExchangeRunner.Plain("user", "user", "secret"), PolicyFixture.Tls));

        AssertStep(SaslLoginOutcome.Accepted, string.Empty, "user", "Login accepted: PLAIN user", steps[0]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xFF }, DisplayName = "user name not UTF-8")]
    [DataRow(new byte[0], DisplayName = "empty user name")]
    public async Task Login_UnreadableUserName_IsAskedThePasswordThenRefusedWithNoUserInTheNote(byte[] userName)
    {
        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), "LOGIN", userName, PolicyFixture.Tls), SaslExchangeRunner.Utf8("secret"));

        AssertStep(SaslLoginOutcome.Challenge, "Password:", null, null, steps[0]);
        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: LOGIN", steps[1]);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("user=user\u0001auth=Bearer tok\u0001", DisplayName = "no final \\x01")]
    [DataRow("auth=Bearer tok\u0001\u0001", DisplayName = "no user pair")]
    [DataRow("user=user\u0001\u0001", DisplayName = "no auth pair")]
    [DataRow("user=user\u0001auth=Basic dXNlcg==\u0001\u0001", DisplayName = "not Bearer")]
    [DataRow("user=user\u0001auth=Bearer \u0001\u0001", DisplayName = "empty token")]
    [DataRow("user=user\u0001auth\u0001\u0001", DisplayName = "pair without =")]
    [DataRow("user=user\u0001=x\u0001auth=Bearer tok\u0001\u0001", DisplayName = "empty key")]
    [DataRow("user=user\u0001auth=Bearer tok\u0001auth=Bearer tok\u0001\u0001", DisplayName = "key twice")]
    public async Task XOAuth2_MalformedResponse_GetsTheErrorChallengeWithNoUserInTheNote(string latin1Response)
    {
        await AssertMalformedBearerResponseAsync("XOAUTH2", latin1Response);
    }

    [TestMethod]
    [DataRow("n,,", DisplayName = "no \\x01")]
    [DataRow("p=tls-unique,,\u0001auth=Bearer tok\u0001\u0001", DisplayName = "channel binding")]
    [DataRow("n,\u0001auth=Bearer tok\u0001\u0001", DisplayName = "one comma")]
    [DataRow("n,b=user,\u0001auth=Bearer tok\u0001\u0001", DisplayName = "not a=")]
    [DataRow("n,a=user\u0001auth=Bearer tok\u0001\u0001", DisplayName = "no closing comma")]
    [DataRow("n,,\u0001host=h\u0001\u0001", DisplayName = "no auth pair")]
    [DataRow("n,,\u0001auth=Bearer tok\u0001", DisplayName = "no final \\x01")]
    public async Task OAuthBearer_MalformedResponse_GetsTheErrorChallengeWithNoUserInTheNote(string latin1Response)
    {
        await AssertMalformedBearerResponseAsync("OAUTHBEARER", latin1Response);
    }

    [TestMethod]
    [DataRow("n,,\u0001auth=Bearer tok\u0001\u0001", "", DisplayName = "no authzid")]
    [DataRow("n,a=,\u0001auth=Bearer tok\u0001\u0001", "", DisplayName = "empty authzid")]
    [DataRow("y,a=someone,\u0001auth=bearer tok\u0001\u0001", "someone", DisplayName = "y flag, lower-case scheme")]
    [DataRow("n,a=a=3Db=2Cc,\u0001auth=Bearer tok\u0001\u0001", "a=b,c", DisplayName = "escaped authzid")]
    public async Task OAuthBearer_OtherWellFormedHeaders_AreAcceptedAsTheAuthzid(string latin1Response, string accountName)
    {
        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), "OAUTHBEARER", Encoding.Latin1.GetBytes(latin1Response), PolicyFixture.Tls));

        AssertStep(SaslLoginOutcome.Accepted, string.Empty, accountName, "Login accepted: OAUTHBEARER bearer token", steps[0]);
    }

    private async Task AssertMalformedBearerResponseAsync(string mechanism, string latin1Response)
    {
        var steps = await runner.RunAsync(
            SaslExchangeRunner.Start(Policy(), mechanism, Encoding.Latin1.GetBytes(latin1Response), PolicyFixture.Tls));

        AssertStep(SaslLoginOutcome.Challenge, "{\"status\":\"invalid_token\"}", null, $"Login refused: {mechanism}", steps[0]);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("LOGIN", DisplayName = "LOGIN")]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task LoginNotes_NeverHoldTheSecret(string mechanism)
    {
        // Secrets no note word contains: "tok" would be found inside "bearer token".
        const string secret = "Qz7secret";
        var accounts = new AccountBook([new Account("user", secret), new Account(string.Empty, secret)]);
        var (accepted, acceptedResponses) = SaslExchangeRunner.Login(mechanism, "user", secret);
        var (refused, refusedResponses) = SaslExchangeRunner.Login(mechanism, "user", "wrongsecret");

        var notes = (await runner.RunAsync(SaslExchangeRunner.Start(Policy(accounts), mechanism, accepted, PolicyFixture.Tls), acceptedResponses))
            .Concat(await runner.RunAsync(SaslExchangeRunner.Start(Policy(accounts), mechanism, refused, PolicyFixture.Tls), refusedResponses))
            .Select(step => step.CheckedLogin?.Note)
            .OfType<string>()
            .ToList();

        Assert.HasCount(2, notes);
        Assert.IsTrue(notes[0].StartsWith("Login accepted", StringComparison.Ordinal));
        Assert.IsTrue(notes.All(note => note.Contains(mechanism, StringComparison.Ordinal)));
        Assert.IsFalse(notes.Any(note => note.Contains(secret, StringComparison.Ordinal) || note.Contains("wrongsecret", StringComparison.Ordinal)));
    }
}
