using System.Text;
using Surl.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="DigestAuthenticationMethod"/> alone and behind <see cref="AuthenticationPolicy"/>:
/// ADR-0032 section 4's challenges, the answers pinned upstream curl 8.21.0 sent
/// (Fixtures/README.md) replayed and tampered with, answers under every algorithm, and
/// ADR-0036's nonce lifetime, <c>stale=true</c> and replay refusal.
/// </summary>
[TestClass]
public sealed class DigestAuthenticationMethodTests
{
    private const string Nonce = FixedDigestNonceBook.FixtureNonce;

    private static readonly AccountBook Accounts = new(
    [
        new Account("tester", "secret"),
        new Account("tëster", "secret"),
        new Account("Mufasa", "Circle of Life"),
        new Account(string.Empty, "tok"),
    ]);

    private static readonly string[] FixtureChallenges =
    [
        $"Digest realm=\"surl\", qop=\"auth\", algorithm=MD5, nonce=\"{Nonce}\"",
        $"Digest realm=\"surl\", qop=\"auth\", algorithm=SHA-256, nonce=\"{Nonce}\"",
        $"Digest realm=\"surl\", qop=\"auth\", algorithm=SHA-512-256, nonce=\"{Nonce}\"",
    ];

    private static readonly HttpAuthenticationRequest GetX = PolicyFixture.Get();

    private readonly ManualTimeProvider clock = new();

    private static async Task<HttpCredentialCheck> VerifyAsync(
        IHttpCredentialVerifier verifier, string authorization, HttpAuthenticationRequest request) =>
        await verifier.VerifyAsync(authorization["Digest ".Length..], request, CancellationToken.None);

    private static Task<HttpCredentialCheck> VerifyFixtureAsync(
        string authorization, HttpAuthenticationRequest request, DigestNonceState state = DigestNonceState.Fresh) =>
        VerifyAsync(new DigestAuthenticationMethod(Accounts, new FixedDigestNonceBook(Nonce, state)), authorization, request);

    private static string Answer(
        string nonce,
        string userName = "tester",
        string password = "secret",
        string algorithm = "MD5",
        string method = "GET",
        string uri = "/x",
        string nc = "00000001")
    {
        var name = DigestAlgorithmName.Parse(algorithm)!;
        var userHash = DigestCalculation.ComputeUserHash(name.Algorithm, Encoding.UTF8, userName, "surl", password);
        var inputs = new DigestResponseInputs(method, uri, nonce, nc, "0a1b2c", "auth");
        var response = DigestCalculation.ComputeResponse(
            name.Algorithm, DigestCalculation.ComputeA1Hash(name, userHash, nonce, "0a1b2c"), inputs);

        return $"Digest username=\"{userName}\",realm=\"surl\",nonce=\"{nonce}\",uri=\"{uri}\",cnonce=\"0a1b2c\","
            + $"nc={nc},algorithm={algorithm},response=\"{response}\",qop=\"auth\"";
    }

    private static void AssertAccepted(string accountName, HttpCredentialCheck check)
    {
        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual(accountName, check.AccountName);
        Assert.IsEmpty(check.WwwAuthenticateValues);
    }

    private static void AssertRefused(HttpCredentialCheck check)
    {
        Assert.AreEqual(new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, []).Outcome, check.Outcome);
        Assert.IsNull(check.AccountName);
        Assert.IsEmpty(check.WwwAuthenticateValues);
    }

    [TestMethod]
    public void CreateChallenges_Always_IsMd5ThenSha256ThenSha512Slash256WithOneNonce()
    {
        var method = new DigestAuthenticationMethod(Accounts, new FixedDigestNonceBook(Nonce));

        CollectionAssert.AreEqual(FixtureChallenges, method.CreateChallenges().ToArray());
        Assert.AreEqual(AuthenticationMethod.Digest, method.Method);
        Assert.AreSame(method, method.StartConnection());
    }

    [TestMethod]
    public void CreateChallenges_RealNonceBook_SharesOneFreshNoncePerCall()
    {
        var method = new DigestAuthenticationMethod(Accounts, clock);

        var first = method.CreateChallenges();
        var second = method.CreateChallenges();

        var nonce = NonceOf(first);
        Assert.AreEqual(80, nonce.Length);
        CollectionAssert.AreEqual(
            FixtureChallenges.Select(challenge => challenge.Replace(Nonce, nonce, StringComparison.Ordinal)).ToArray(),
            first.ToArray());
        Assert.DoesNotContain(nonce, second[0]);
    }

    [TestMethod]
    public async Task Challenge_BehindThePolicyOverHttp_IsTheThreeDigestFieldsAlone()
    {
        var policy = PolicyFixture.Create(
            Accounts, clock, httpMethods:
            [
                new BasicAuthenticationMethod(Accounts),
                new DigestAuthenticationMethod(Accounts, new FixedDigestNonceBook(Nonce)),
            ]);

        var verdict = await policy.StartHttpConnection(null).JudgeAsync(GetX, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(FixtureChallenges, verdict.WwwAuthenticateValues.ToArray());
    }

    [TestMethod]
    public async Task Challenge_BehindThePolicyOverTls_IsDigestBeforeBasic()
    {
        var policy = PolicyFixture.Create(
            Accounts, clock, httpMethods:
            [
                new BasicAuthenticationMethod(Accounts),
                new DigestAuthenticationMethod(Accounts, new FixedDigestNonceBook(Nonce)),
            ]);

        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(GetX, CancellationToken.None);

        CollectionAssert.AreEqual(
            (string[])[.. FixtureChallenges, BasicAuthenticationMethod.Challenge], verdict.WwwAuthenticateValues.ToArray());
    }

    [TestMethod]
    [DataRow("digest-md5", "tester", DisplayName = "--digest -u tester:secret")]
    [DataRow("digest-md5-sess", "tester", DisplayName = "algorithm=MD5-sess")]
    [DataRow("digest-query", "tester", DisplayName = "a target with a query")]
    [DataRow("digest-post", "tester", DisplayName = "-X POST -d x")]
    [DataRow("digest-non-ascii-argument", "tëster", DisplayName = "an ISO-8859-1 user name")]
    public async Task RecordedAnswer_BehindThePolicyOverHttp_ProceedsAsTheAccount(string caseName, string accountName)
    {
        var policy = PolicyFixture.Create(
            Accounts, clock, httpMethods: new DigestAuthenticationMethod(Accounts, new FixedDigestNonceBook(Nonce)));

        var verdict = await policy.StartHttpConnection(null)
            .JudgeAsync(RecordedFixture.ReadLastRequest(caseName), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual(accountName, verdict.AccountName);
        Assert.IsEmpty(verdict.WwwAuthenticateValues);
    }

    [TestMethod]
    [DataRow("response=\"882f3ddbc931b996212b4cba1543945f\"", "response=\"882f3ddbc931b996212b4cba1543945e\"", DisplayName = "response")]
    [DataRow("uri=\"/x\"", "uri=\"/y\"", DisplayName = "uri")]
    [DataRow("nc=00000002", "nc=00000003", DisplayName = "nc")]
    [DataRow("realm=\"surl\"", "realm=\"r\"", DisplayName = "realm")]
    [DataRow("username=\"tester\"", "username=\"Tester\"", DisplayName = "user")]
    [DataRow("cnonce=\"dda3", "cnonce=\"dda4", DisplayName = "cnonce")]
    [DataRow("algorithm=MD5", "algorithm=SHA-256", DisplayName = "algorithm")]
    [DataRow("nonce=\"fixturenonce\"", "nonce=\"othernonce\"", DisplayName = "nonce")]
    public async Task RecordedAnswer_OneParameterChanged_IsRefused(string recorded, string changed)
    {
        var request = RecordedFixture.ReadLastRequest("digest-md5");
        var authorization = request.Fields.Single(field => field.Key == "Authorization").Value;
        Assert.Contains(recorded, authorization);
        var changedRequest = request with { Target = changed.StartsWith("uri", StringComparison.Ordinal) ? "/y" : "/x" };

        var check = await VerifyFixtureAsync(authorization.Replace(recorded, changed, StringComparison.Ordinal), changedRequest);

        AssertRefused(check);
    }

    [TestMethod]
    public async Task RecordedAnswer_OnAnotherTarget_IsRefused()
    {
        var request = RecordedFixture.ReadLastRequest("digest-md5");
        var authorization = request.Fields.Single(field => field.Key == "Authorization").Value;

        AssertRefused(await VerifyFixtureAsync(authorization, request with { Target = "/y" }));
    }

    [TestMethod]
    public async Task RecordedAnswer_WithAnotherMethod_IsRefused()
    {
        var request = RecordedFixture.ReadLastRequest("digest-md5");
        var authorization = request.Fields.Single(field => field.Key == "Authorization").Value;

        AssertRefused(await VerifyFixtureAsync(authorization, request with { Method = "PUT" }));
    }

    [TestMethod]
    public async Task RecordedAnswer_Utf8SpellingOfTheNonAsciiUser_DoesNotMatchTheIso88591Answer()
    {
        // The recorded answer hashed the ISO-8859-1 byte EB; the same name spelled in UTF-8 is
        // another user name, and an account whose password is outside ISO-8859-1 has no
        // ISO-8859-1 spelling at all (ADR-0036).
        var accounts = new AccountBook([new Account("tëster", "s€cret")]);
        var request = RecordedFixture.ReadLastRequest("digest-non-ascii-argument");
        var authorization = request.Fields.Single(field => field.Key == "Authorization").Value;
        var method = new DigestAuthenticationMethod(accounts, new FixedDigestNonceBook(Nonce));

        AssertRefused(await VerifyAsync(method, authorization, request));
    }

    [TestMethod]
    [DataRow("iso-8859-1")]
    [DataRow("utf-8")]
    public async Task Answer_AsciiNameWithANonAsciiPasswordInEitherEncoding_IsAccepted(string encodingName)
    {
        // An ASCII name is its own ISO-8859-1 spelling, so both password encodings must match (ADR-0036).
        var method = new DigestAuthenticationMethod(new AccountBook([new Account("tester", "sé")]), new FixedDigestNonceBook(Nonce));
        var userHash = DigestCalculation.ComputeUserHash(
            DigestAlgorithm.Md5, Encoding.GetEncoding(encodingName), "tester", "surl", "sé");
        var response = DigestCalculation.ComputeResponse(
            DigestAlgorithm.Md5, userHash, new DigestResponseInputs("GET", "/x", Nonce, "00000001", "c", "auth"));
        var authorization = $"Digest username=\"tester\",realm=\"surl\",nonce=\"{Nonce}\",uri=\"/x\",cnonce=\"c\","
            + $"nc=00000001,response=\"{response}\",qop=\"auth\"";

        AssertAccepted("tester", await VerifyAsync(method, authorization, GetX));
    }

    [TestMethod]
    [DataRow("MD5")]
    [DataRow("MD5-sess")]
    [DataRow("SHA-256")]
    [DataRow("SHA-256-sess")]
    [DataRow("SHA-512-256")]
    [DataRow("SHA-512-256-sess")]
    public async Task Answer_UnderEachAlgorithm_IsAccepted(string algorithm)
    {
        var check = await VerifyFixtureAsync(Answer(Nonce, algorithm: algorithm), GetX);

        AssertAccepted("tester", check);
    }

    [TestMethod]
    public async Task Answer_Sha512Slash256BuiltOnBl112sHash_IsAccepted()
    {
        static string Hash(string text) => Convert.ToHexStringLower(Sha512Slash256.HashData(Encoding.UTF8.GetBytes(text)));
        var response = Hash($"{Hash("Mufasa:surl:Circle of Life")}:{Nonce}:00000001:f2/wE4q7:auth:{Hash("GET:/dir/index.html")}");
        var authorization = $"Digest username=\"Mufasa\", realm=\"surl\", uri=\"/dir/index.html\", algorithm=SHA-512-256, "
            + $"nonce=\"{Nonce}\", nc=00000001, cnonce=\"f2/wE4q7\", qop=auth, response=\"{response}\"";

        var check = await VerifyFixtureAsync(authorization, GetX with { Target = "/dir/index.html" });

        AssertAccepted("Mufasa", check);
    }

    [TestMethod]
    public async Task Answer_UpperCaseResponseAndNoAlgorithm_IsAcceptedAsMd5()
    {
        var answer = Answer(Nonce).Replace(",algorithm=MD5", string.Empty, StringComparison.Ordinal);
        var response = answer[(answer.IndexOf("response=\"", StringComparison.Ordinal) + 10)..][..32];

        var check = await VerifyFixtureAsync(answer.Replace(response, response.ToUpperInvariant(), StringComparison.Ordinal), GetX);

        AssertAccepted("tester", check);
    }

    [TestMethod]
    public async Task Answer_UserhashFalse_IsAccepted()
    {
        AssertAccepted("tester", await VerifyFixtureAsync(Answer(Nonce) + ",userhash=false", GetX));
    }

    [TestMethod]
    [DataRow(",userhash=true", "", DisplayName = "userhash=true, never offered")]
    [DataRow("", "qop=\"auth\"|qop=\"auth-int\"", DisplayName = "qop=auth-int")]
    [DataRow("", ",qop=\"auth\"|", DisplayName = "no qop")]
    [DataRow("", "nc=00000001|nc=0000001", DisplayName = "nc of seven digits")]
    [DataRow("", "nc=00000001|nc=0000000g", DisplayName = "nc not hex")]
    [DataRow("", "algorithm=MD5|algorithm=SHA-1", DisplayName = "an unknown algorithm")]
    [DataRow("", ",cnonce=\"0a1b2c\"|", DisplayName = "no cnonce")]
    [DataRow(",nc=00000009", "", DisplayName = "nc twice")]
    [DataRow(",x", "", DisplayName = "malformed")]
    public async Task Answer_NotOneSurlCanCheck_IsRefused(string appended, string replacement)
    {
        var parts = replacement.Split('|');
        var answer = Answer(Nonce) + appended;
        answer = parts.Length == 2 ? answer.Replace(parts[0], parts[1], StringComparison.Ordinal) : answer;

        AssertRefused(await VerifyFixtureAsync(answer, GetX));
    }

    [TestMethod]
    [DataRow("stranger", "secret", DisplayName = "an unknown user")]
    [DataRow("tester", "wrong", DisplayName = "a wrong password")]
    [DataRow("", "tok", DisplayName = "the Bearer token's empty name")]
    public async Task Answer_NoAccountMatches_IsRefusedAfterOneFixedTimeComparison(string userName, string password)
    {
        var comparer = new CountingSecretComparer();
        var accounts = new AccountBook([new Account("tester", "secret"), new Account(string.Empty, "tok")], comparer);
        var method = new DigestAuthenticationMethod(accounts, new FixedDigestNonceBook(Nonce));

        var check = await VerifyAsync(method, Answer(Nonce, userName, password), GetX);

        AssertRefused(check);
        CollectionAssert.AreEqual(new[] { (32, 32), (32, 32) }, comparer.Comparisons);
    }

    [TestMethod]
    public async Task Answer_UnknownNonce_IsRefusedWithoutChecking()
    {
        var comparer = new CountingSecretComparer();
        var method = new DigestAuthenticationMethod(
            new AccountBook([new Account("tester", "secret")], comparer), new FixedDigestNonceBook(Nonce));

        AssertRefused(await VerifyAsync(method, Answer("forged"), GetX));
        Assert.IsEmpty(comparer.Comparisons);
    }

    [TestMethod]
    public async Task Answer_Accepted_RecordsItsNonceCount()
    {
        var nonces = new FixedDigestNonceBook(Nonce);
        var method = new DigestAuthenticationMethod(Accounts, nonces);

        await VerifyAsync(method, Answer(Nonce, nc: "0000000a"), GetX);

        CollectionAssert.AreEqual(new uint[] { 10 }, nonces.RecordedUses);
    }

    [TestMethod]
    public async Task Answer_ReplayedWithARealNonce_IsRefused()
    {
        var method = new DigestAuthenticationMethod(Accounts, clock);
        var nonce = NonceOf(method.CreateChallenges());

        AssertAccepted("tester", await VerifyAsync(method, Answer(nonce), GetX));
        AssertRefused(await VerifyAsync(method, Answer(nonce), GetX));
        AssertAccepted("tester", await VerifyAsync(method, Answer(nonce, nc: "00000002"), GetX));
    }

    [TestMethod]
    public async Task Answer_RightButOnAnExpiredNonce_GetsTheStaleChallenges()
    {
        var method = new DigestAuthenticationMethod(Accounts, clock);
        var nonce = NonceOf(method.CreateChallenges());
        clock.Advance(DigestNonceBook.Lifetime + TimeSpan.FromSeconds(1));

        var check = await VerifyAsync(method, Answer(nonce), GetX);

        Assert.AreEqual(HttpCredentialOutcome.Continue, check.Outcome);
        Assert.IsNull(check.AccountName);
        var freshNonce = NonceOf(check.WwwAuthenticateValues);
        Assert.AreNotEqual(nonce, freshNonce);
        CollectionAssert.AreEqual(
            FixtureChallenges.Select(challenge => challenge.Replace(Nonce, freshNonce, StringComparison.Ordinal) + ", stale=true").ToArray(),
            check.WwwAuthenticateValues.ToArray());
        AssertAccepted("tester", await VerifyAsync(method, Answer(freshNonce), GetX));
    }

    [TestMethod]
    public async Task Answer_WrongOnAnExpiredNonce_IsRefused()
    {
        var method = new DigestAuthenticationMethod(Accounts, clock);
        var nonce = NonceOf(method.CreateChallenges());
        clock.Advance(DigestNonceBook.Lifetime + TimeSpan.FromSeconds(1));

        AssertRefused(await VerifyAsync(method, Answer(nonce, password: "wrong"), GetX));
    }

    [TestMethod]
    public async Task Answer_StaleBehindThePolicy_IsAnUndelayed401WithTheStaleChallenges()
    {
        var policy = PolicyFixture.Create(
            Accounts, clock, httpMethods: new DigestAuthenticationMethod(Accounts, new FixedDigestNonceBook(Nonce, DigestNonceState.Expired)));

        var judgement = policy.StartHttpConnection(null)
            .JudgeAsync(PolicyFixture.Get(Answer(Nonce)), CancellationToken.None).AsTask();

        Assert.IsTrue(judgement.IsCompleted, "a stale answer is not delayed");
        var verdict = await judgement;
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(
            FixtureChallenges.Select(challenge => challenge + ", stale=true").ToArray(), verdict.WwwAuthenticateValues.ToArray());
    }

    [TestMethod]
    public async Task VerifyAsync_Cancelled_Throws()
    {
        var method = new DigestAuthenticationMethod(Accounts, clock);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await method.VerifyAsync(string.Empty, GetX, new CancellationToken(true)));
    }

    [TestMethod]
    public void Arguments_Null_Throw()
    {
        var method = new DigestAuthenticationMethod(Accounts, clock);

        Assert.ThrowsExactly<ArgumentNullException>(() => new DigestAuthenticationMethod(null!, clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DigestAuthenticationMethod(Accounts, (TimeProvider)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => method.VerifyAsync(null!, GetX, CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => method.VerifyAsync(string.Empty, null!, CancellationToken.None));
    }

    private static string NonceOf(IReadOnlyList<string> challenges)
    {
        var start = challenges[0].IndexOf("nonce=\"", StringComparison.Ordinal) + 7;

        return challenges[0][start..challenges[0].IndexOf('"', start)];
    }
}
