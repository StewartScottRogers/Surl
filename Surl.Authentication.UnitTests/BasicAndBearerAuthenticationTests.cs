using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="BasicAuthenticationMethod"/> and <see cref="BearerAuthenticationMethod"/> behind
/// <see cref="AuthenticationPolicy"/>, replaying the <c>Authorization</c> values pinned upstream
/// curl 8.21.0 sent (Fixtures/README.md) against ADR-0032 sections 4 and 8 and ADR-0035.
/// </summary>
[TestClass]
public sealed class BasicAndBearerAuthenticationTests
{
    private const string NonAsciiUserName = "tëster";

    private static readonly string[] BothChallenges =
        ["Basic realm=\"surl\", charset=\"UTF-8\"", "Bearer realm=\"surl\""];

    private static readonly AccountBook Accounts = new(
    [
        new Account("tester", "secret"),
        new Account(NonAsciiUserName, "sé:cr€t"),
        new Account(string.Empty, "tok"),
    ]);

    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy CreatePolicy(bool allowPlaintextAuth = false) =>
        PolicyFixture.Create(
            Accounts,
            clock,
            allowPlaintextAuth: allowPlaintextAuth,
            httpMethods: [new BearerAuthenticationMethod(Accounts), new BasicAuthenticationMethod(Accounts)]);

    private async Task<HttpAuthenticationVerdict> JudgeAsync(
        TlsSession? tlsSession, HttpAuthenticationRequest request, bool allowPlaintextAuth = false)
    {
        var judgement = CreatePolicy(allowPlaintextAuth).StartHttpConnection(tlsSession)
            .JudgeAsync(request, CancellationToken.None).AsTask();
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

    private static string Basic(string userPass) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userPass));

    [TestMethod]
    [DataRow("basic", "tester", DisplayName = "--basic -u tester:secret")]
    [DataRow("user-default", "tester", DisplayName = "-u tester:secret")]
    [DataRow("basic-utf8-config", NonAsciiUserName, DisplayName = "UTF-8 user in a config file, : in the password")]
    public async Task RecordedBasic_OverTls_ProceedsAsTheAccount(string caseName, string userName)
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Proceed, [], userName,
            await JudgeAsync(PolicyFixture.Tls, RecordedFixture.ReadRequest(caseName)));
    }

    [TestMethod]
    public async Task RecordedBearer_OverTls_ProceedsAsTheTokensAccount()
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Proceed, [], string.Empty,
            await JudgeAsync(PolicyFixture.Tls, RecordedFixture.ReadRequest("bearer")));
    }

    [TestMethod]
    public async Task RecordedBasic_AnsiCodePageCommandLine_IsRefused()
    {
        // The Windows reference build sent Windows-1252 bytes; Surl reads Basic as UTF-8 (ADR-0035).
        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, BothChallenges, null,
            await JudgeAsync(PolicyFixture.Tls, RecordedFixture.ReadRequest("basic-non-ascii")));
    }

    [TestMethod]
    [DataRow("tester:wrong", DisplayName = "wrong password")]
    [DataRow("nobody:secret", DisplayName = "unknown user")]
    [DataRow("testersecret", DisplayName = "no colon")]
    [DataRow(":tok", DisplayName = "empty user name with the Bearer token")]
    [DataRow("tester:", DisplayName = "empty password")]
    public async Task BasicCredentials_ThatMatchNoAccount_AreRefusedWithBothChallenges(string userPass)
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, BothChallenges, null,
            await JudgeAsync(PolicyFixture.Tls, PolicyFixture.Get(Basic(userPass))));
    }

    [TestMethod]
    [DataRow("Basic !!!not-base64", DisplayName = "not base64")]
    [DataRow("Basic dGVzdGVyOnNlY3JldA", DisplayName = "missing padding")]
    [DataRow("Basic", DisplayName = "no credentials")]
    [DataRow("Bearer", DisplayName = "empty token")]
    [DataRow("Bearer wrong", DisplayName = "wrong token")]
    [DataRow("Bearer tok ", DisplayName = "token with a trailing space")]
    [DataRow("Bearer secret", DisplayName = "a password as the token")]
    public async Task MalformedOrWrongCredentials_AreRefusedWithBothChallenges(string authorization)
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, BothChallenges, null,
            await JudgeAsync(PolicyFixture.Tls, PolicyFixture.Get(authorization)));
    }

    [TestMethod]
    public async Task RefusedCredentials_WaitTheRefusalDelay()
    {
        var judgement = CreatePolicy().StartHttpConnection(PolicyFixture.Tls)
            .JudgeAsync(PolicyFixture.Get(Basic("tester:wrong")), CancellationToken.None);

        Assert.IsFalse(judgement.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, (await judgement).Outcome);
    }

    [TestMethod]
    [DataRow("basic")]
    [DataRow("user-default")]
    [DataRow("bearer")]
    public async Task RecordedPlaintextSecret_Unencrypted_IsForbiddenUnchecked(string caseName)
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Forbidden, [], null,
            await JudgeAsync(null, RecordedFixture.ReadRequest(caseName)));
    }

    [TestMethod]
    [DataRow("basic", "tester")]
    [DataRow("bearer", "")]
    public async Task RecordedPlaintextSecret_UnencryptedWithAllowPlaintextAuth_Proceeds(string caseName, string accountName)
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Proceed, [], accountName,
            await JudgeAsync(null, RecordedFixture.ReadRequest(caseName), allowPlaintextAuth: true));
    }

    [TestMethod]
    public async Task NoCredentials_OverTls_IsChallengedWithBasicThenBearer()
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Challenge,
            ["Basic realm=\"surl\", charset=\"UTF-8\"", "Bearer realm=\"surl\""],
            null,
            await JudgeAsync(PolicyFixture.Tls, PolicyFixture.Get()));
    }

    [TestMethod]
    public async Task NoCredentials_UnencryptedWithAllowPlaintextAuth_IsChallengedWithBoth()
    {
        AssertVerdict(
            HttpAuthenticationOutcome.Challenge, BothChallenges, null,
            await JudgeAsync(null, PolicyFixture.Get(), allowPlaintextAuth: true));
    }

    [TestMethod]
    public async Task NoCredentials_Unencrypted_IsForbiddenBecauseNeitherCanBeOffered()
    {
        AssertVerdict(HttpAuthenticationOutcome.Forbidden, [], null, await JudgeAsync(null, PolicyFixture.Get()));
    }

    [TestMethod]
    public void Challenges_AreAdr0032Section4s()
    {
        CollectionAssert.AreEqual(
            new[] { "Basic realm=\"surl\", charset=\"UTF-8\"" },
            new BasicAuthenticationMethod(Accounts).CreateChallenges().ToArray());
        CollectionAssert.AreEqual(
            new[] { "Bearer realm=\"surl\"" },
            new BearerAuthenticationMethod(Accounts).CreateChallenges().ToArray());
    }

    [TestMethod]
    public void Methods_NameThemselvesAndShareOneVerifierPerMethod()
    {
        var basic = new BasicAuthenticationMethod(Accounts);
        var bearer = new BearerAuthenticationMethod(Accounts);

        Assert.AreEqual(AuthenticationMethod.Basic, basic.Method);
        Assert.AreEqual(AuthenticationMethod.Bearer, bearer.Method);
        Assert.AreSame(basic, basic.StartConnection());
        Assert.AreSame(bearer, bearer.StartConnection());
    }

    [TestMethod]
    public async Task EmptyToken_IsRefusedEvenWhenAnEmptyTokenIsConfigured()
    {
        var bearer = new BearerAuthenticationMethod(new AccountBook([new Account(string.Empty, string.Empty)]));

        var check = await bearer.VerifyAsync(string.Empty, PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpCredentialOutcome.Refused, check.Outcome);
        Assert.IsNull(check.AccountName);
    }

    [TestMethod]
    public void Constructors_NullAccounts_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new BasicAuthenticationMethod(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BearerAuthenticationMethod(null!));
    }

    [TestMethod]
    public async Task VerifyAsync_NullCredentials_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => new BasicAuthenticationMethod(Accounts).VerifyAsync(null!, PolicyFixture.Get(), CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => new BearerAuthenticationMethod(Accounts).VerifyAsync(null!, PolicyFixture.Get(), CancellationToken.None).AsTask());
    }
}
