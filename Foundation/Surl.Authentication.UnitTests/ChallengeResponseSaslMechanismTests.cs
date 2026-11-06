using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/>'s SASL <c>CRAM-MD5</c> and <c>DIGEST-MD5</c> exchanges and
/// POP3 <c>APOP</c> against ADR-0049 sections 5 and 7: the RFCs' own examples, and the responses
/// upstream curl 8.21.0 sent when ADR-0049 measured it (<c>-u user:secret</c>, challenge
/// <c>&lt;0123456789abcdef.1790640000@surl&gt;</c>, nonce <c>MDEyMzQ1Njc4OWFiY2RlZg==</c>).
/// </summary>
[TestClass]
public sealed class ChallengeResponseSaslMechanismTests
{
    // ADR-0049, "What upstream curl 8.21.0 does (measured)".
    private const string CurlTimestamp = "<0123456789abcdef.1790640000@surl>";
    private const string CurlCramMd5 = "user 79a4ce2457c420f9dd830de72f78a7d2";
    private const string CurlApopDigest = "32d4437494fda0ae78d0559952474e34";
    private const string CurlDigestMd5Challenge =
        "realm=\"surl\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\",qop=\"auth\",charset=utf-8,algorithm=md5-sess";
    private const string CurlDigestMd5 =
        "username=\"user\",realm=\"\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\",digest-uri=\"smtp/127.0.0.1\",cnonce=\"e495b889b7b854405de324ede5849ea3\",nc=00000001,response=9ff8685c4f01832cd4bed741bdc439b7,qop=auth,charset=utf-8";

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private readonly ManualTimeProvider clock = new(DateTimeOffset.FromUnixTimeSeconds(1790640000));

    private AuthenticationPolicy Policy(AccountBook? accounts = null, bool allowAnonymous = false) =>
        PolicyFixture.CreateWithFixedNonces(accounts ?? SaslExchangeRunner.UserAndToken, clock, allowAnonymous, EveryMethod);

    private static ISaslExchange Start(AuthenticationPolicy policy, string mechanism, string? initialResponse = null) =>
        SaslExchangeRunner.Start(policy, mechanism, initialResponse is null ? null : Latin1(initialResponse), null);

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static void AssertStep(
        SaslLoginOutcome outcome, string challenge, string? accountName, string? note, SaslLoginStep step)
    {
        Assert.AreEqual(outcome, step.Outcome);
        Assert.AreEqual(challenge, Encoding.Latin1.GetString(step.Challenge.Span));
        Assert.AreEqual(accountName, step.AccountName);
        Assert.AreEqual(note, step.CheckedLogin?.Note);
    }

    private static async Task<SaslLoginStep> Undelayed(ValueTask<SaslLoginStep> pending)
    {
        Assert.IsTrue(pending.IsCompleted);

        return await pending;
    }

    // A refusal waits the refusal delay on the injected clock, and only then is answered.
    private async Task<SaslLoginStep> AfterTheRefusalDelay(ValueTask<SaslLoginStep> pending)
    {
        var step = pending.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(step.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        return await step;
    }

    private static string DigestMd5(
        string userName = "user",
        string password = "secret",
        string realm = "",
        string nonce = "MDEyMzQ1Njc4OWFiY2RlZg==",
        string nonceCount = "00000001",
        string qop = "auth",
        string? authorizationId = null)
    {
        var response = new DigestMd5Response(
            userName, realm, nonce, "e495b889b7b854405de324ede5849ea3", nonceCount, qop, "imap/localhost", string.Empty, authorizationId);
        var digest = Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponse(response, Encoding.UTF8.GetBytes(password)));
        var authzid = authorizationId is null ? string.Empty : $",authzid=\"{authorizationId}\"";

        return $"username=\"{userName}\",realm=\"{realm}\",nonce=\"{nonce}\",cnonce=\"e495b889b7b854405de324ede5849ea3\",nc={nonceCount},qop={qop},digest-uri=\"imap/localhost\",response={digest}{authzid}";
    }

    // ---- CRAM-MD5 ----

    [TestMethod]
    public void CramMd5_Rfc2195Section2Example_GivesItsDigest()
    {
        // RFC 2195, section 2: tim, secret tanstaaftanstaaf.
        var digest = CramMd5SaslExchange.ComputeDigest(
            "tanstaaftanstaaf"u8, "<1896.697170952@postoffice.reston.mci.net>"u8);

        Assert.AreEqual("b913a602c7eda7a495b4e6e7334d3890", Convert.ToHexStringLower(digest));
    }

    [TestMethod]
    [DataRow(CurlCramMd5, DisplayName = "as curl sent it")]
    [DataRow("user 79A4CE2457C420F9DD830DE72F78A7D2", DisplayName = "upper-case hex")]
    public async Task CramMd5_CurlsLoginWithoutTls_IsChallengedThenAcceptedUndelayed(string response)
    {
        var exchange = Start(Policy(), "cram-md5");

        AssertStep(SaslLoginOutcome.Challenge, CurlTimestamp, null, null, await Undelayed(exchange.BeginAsync(CancellationToken.None)));
        AssertStep(
            SaslLoginOutcome.Accepted, string.Empty, "user", "Login accepted: CRAM-MD5 user",
            await Undelayed(exchange.ContinueAsync(Latin1(response), CancellationToken.None)));
    }

    [TestMethod]
    [DataRow("user 00000000000000000000000000000000", "Login refused: CRAM-MD5 user", DisplayName = "wrong digest")]
    [DataRow("nobody 79a4ce2457c420f9dd830de72f78a7d2", "Login refused: CRAM-MD5 nobody", DisplayName = "unknown user")]
    [DataRow("us er 79a4ce2457c420f9dd830de72f78a7d2", "Login refused: CRAM-MD5 us er", DisplayName = "user with a space")]
    [DataRow("user 79a4ce2457c420f9dd830de72f78a7", "Login refused: CRAM-MD5 user", DisplayName = "short digest")]
    [DataRow("user 79a4ce2457c420f9dd830de72f78a7zz", "Login refused: CRAM-MD5 user", DisplayName = "digest not hex")]
    [DataRow("79a4ce2457c420f9dd830de72f78a7d2", "Login refused: CRAM-MD5", DisplayName = "no user")]
    [DataRow(" 79a4ce2457c420f9dd830de72f78a7d2", "Login refused: CRAM-MD5", DisplayName = "empty user")]
    [DataRow("ÿ 79a4ce2457c420f9dd830de72f78a7d2", "Login refused: CRAM-MD5", DisplayName = "user not UTF-8")]
    [DataRow("", "Login refused: CRAM-MD5", DisplayName = "empty response")]
    public async Task CramMd5_WrongOrMalformedResponse_IsRefusedAfterTheDelay(string response, string note)
    {
        var exchange = Start(Policy(), "CRAM-MD5");
        await exchange.BeginAsync(CancellationToken.None);

        AssertStep(
            SaslLoginOutcome.RefusedCredentials, string.Empty, null, note,
            await AfterTheRefusalDelay(exchange.ContinueAsync(Latin1(response), CancellationToken.None)));
    }

    [TestMethod]
    public async Task CramMd5_NoAccounts_IsRefusedWithTheSameVerdict()
    {
        var exchange = Start(Policy(PolicyFixture.NoAccounts), "CRAM-MD5");
        await exchange.BeginAsync(CancellationToken.None);

        AssertStep(
            SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: CRAM-MD5 user",
            await AfterTheRefusalDelay(exchange.ContinueAsync(Latin1(CurlCramMd5), CancellationToken.None)));
    }

    [TestMethod]
    public async Task CramMd5_TheBearerTokensAccount_IsNeverMatched()
    {
        var digest = Convert.ToHexStringLower(CramMd5SaslExchange.ComputeDigest("tok"u8, Latin1(CurlTimestamp)));
        var exchange = Start(Policy(), "CRAM-MD5");
        await exchange.BeginAsync(CancellationToken.None);

        var step = await AfterTheRefusalDelay(exchange.ContinueAsync(Latin1(" " + digest), CancellationToken.None));

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
    }

    [TestMethod]
    public async Task CramMd5_InitialResponse_IsRefusedAsABadCredential()
    {
        var step = await AfterTheRefusalDelay(Start(Policy(), "CRAM-MD5", CurlCramMd5).BeginAsync(CancellationToken.None));

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: CRAM-MD5", step);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "after the challenge")]
    [DataRow("anything", DisplayName = "initial response")]
    public async Task CramMd5_AllowAnonymous_AcceptsAnyResponseUncheckedWithNoNote(string? initialResponse)
    {
        var exchange = Start(Policy(allowAnonymous: true), "CRAM-MD5", initialResponse);

        var step = await exchange.BeginAsync(CancellationToken.None);
        if (initialResponse is null)
        {
            AssertStep(SaslLoginOutcome.Challenge, CurlTimestamp, null, null, step);
            step = await Undelayed(exchange.ContinueAsync(Latin1("nobody 00"), CancellationToken.None));
        }

        AssertStep(SaslLoginOutcome.AcceptedUnchecked, string.Empty, null, null, step);
    }

    // ---- DIGEST-MD5 ----

    [TestMethod]
    public void DigestMd5_Rfc2831Section4Example_GivesItsResponseAndRspauth()
    {
        // RFC 2831, section 4: chris, secret "secret", IMAP to elwood.innosoft.com.
        var response = new DigestMd5Response(
            "chris", "elwood.innosoft.com", "OA6MG9tEQGm2hh", "OA6MHXh6VqTrRk", "00000001", "auth",
            "imap/elwood.innosoft.com", "d388dad90d4bbd760a152321f2143af7", null);

        Assert.AreEqual(
            "d388dad90d4bbd760a152321f2143af7",
            Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponse(response, "secret"u8)));
        Assert.AreEqual(
            "ea40f60335c427b5527b84dbabcdfffd",
            Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponseAuth(response, "secret"u8)));
    }

    [TestMethod]
    public async Task DigestMd5_CurlsLogin_IsChallengedAnsweredWithRspauthThenAccepted()
    {
        var expectedResponseAuth = Convert.ToHexStringLower(
            DigestMd5Calculation.ComputeResponseAuth(DigestMd5Response.Read(CurlDigestMd5)!, "secret"u8));
        var exchange = Start(Policy(), "DIGEST-MD5");

        AssertStep(SaslLoginOutcome.Challenge, CurlDigestMd5Challenge, null, null, await Undelayed(exchange.BeginAsync(CancellationToken.None)));
        AssertStep(
            SaslLoginOutcome.Challenge, "rspauth=" + expectedResponseAuth, null, null,
            await Undelayed(exchange.ContinueAsync(Latin1(CurlDigestMd5), CancellationToken.None)));
        AssertStep(
            SaslLoginOutcome.Accepted, string.Empty, "user", "Login accepted: DIGEST-MD5 user",
            await Undelayed(exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)));
    }

    [TestMethod]
    [DataRow("surl", null, DisplayName = "the offered realm")]
    [DataRow("", "user", DisplayName = "authzid equal to username")]
    public async Task DigestMd5_OtherAcceptedShapes_AreAccepted(string realm, string? authorizationId)
    {
        var exchange = Start(Policy(), "DIGEST-MD5");
        await exchange.BeginAsync(CancellationToken.None);

        var rspauth = await exchange.ContinueAsync(Latin1(DigestMd5(realm: realm, authorizationId: authorizationId)), CancellationToken.None);
        var step = await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Challenge, rspauth.Outcome);
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
    }

    [TestMethod]
    [DataRow("wrong password", "Login refused: DIGEST-MD5 user", DisplayName = "wrong password")]
    [DataRow("unknown user", "Login refused: DIGEST-MD5 nobody", DisplayName = "unknown user")]
    [DataRow("other nonce", "Login refused: DIGEST-MD5 user", DisplayName = "not the nonce issued")]
    [DataRow("nc 2", "Login refused: DIGEST-MD5 user", DisplayName = "nc not 00000001")]
    [DataRow("auth-int", "Login refused: DIGEST-MD5 user", DisplayName = "qop not auth")]
    [DataRow("other realm", "Login refused: DIGEST-MD5 user", DisplayName = "realm neither empty nor surl")]
    [DataRow("authzid", "Login refused: DIGEST-MD5 user", DisplayName = "authzid not the username")]
    [DataRow("empty user", "Login refused: DIGEST-MD5", DisplayName = "empty username")]
    [DataRow("not UTF-8", "Login refused: DIGEST-MD5", DisplayName = "username not UTF-8")]
    [DataRow("not hex", "Login refused: DIGEST-MD5 user", DisplayName = "response not hex")]
    [DataRow("missing cnonce", "Login refused: DIGEST-MD5", DisplayName = "a required directive missing")]
    [DataRow("unreadable", "Login refused: DIGEST-MD5", DisplayName = "directives unreadable")]
    public async Task DigestMd5_WrongOrMalformedResponse_IsRefusedAfterTheDelay(string kind, string note)
    {
        var response = kind switch
        {
            "wrong password" => DigestMd5(password: "wrong"),
            "unknown user" => DigestMd5(userName: "nobody"),
            "other nonce" => DigestMd5(nonce: "AAAAAAAAAAAAAAAAAAAAAA=="),
            "nc 2" => DigestMd5(nonceCount: "00000002"),
            "auth-int" => DigestMd5(qop: "auth-int"),
            "other realm" => DigestMd5(realm: "elwood.innosoft.com"),
            "authzid" => DigestMd5(authorizationId: "boss"),
            "empty user" => DigestMd5(userName: string.Empty),
            "not UTF-8" => DigestMd5(userName: "ÿ"),
            "not hex" => CurlDigestMd5.Replace("response=9ff8685c4f01832cd4bed741bdc439b7", "response=zz", StringComparison.Ordinal),
            "missing cnonce" => CurlDigestMd5.Replace("cnonce=\"e495b889b7b854405de324ede5849ea3\",", string.Empty, StringComparison.Ordinal),
            _ => "username=\"user",
        };
        var exchange = Start(Policy(), "DIGEST-MD5");
        await exchange.BeginAsync(CancellationToken.None);

        AssertStep(
            SaslLoginOutcome.RefusedCredentials, string.Empty, null, note,
            await AfterTheRefusalDelay(exchange.ContinueAsync(Latin1(response), CancellationToken.None)));
    }

    [TestMethod]
    public async Task DigestMd5_NoAccounts_IsRefusedWithTheSameVerdict()
    {
        var exchange = Start(Policy(PolicyFixture.NoAccounts), "DIGEST-MD5");
        await exchange.BeginAsync(CancellationToken.None);

        AssertStep(
            SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: DIGEST-MD5 user",
            await AfterTheRefusalDelay(exchange.ContinueAsync(Latin1(CurlDigestMd5), CancellationToken.None)));
    }

    [TestMethod]
    public async Task DigestMd5_NonEmptyAnswerToRspauth_IsRefusedAfterTheDelay()
    {
        var exchange = Start(Policy(), "DIGEST-MD5");
        await exchange.BeginAsync(CancellationToken.None);
        await exchange.ContinueAsync(Latin1(CurlDigestMd5), CancellationToken.None);

        AssertStep(
            SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: DIGEST-MD5 user",
            await AfterTheRefusalDelay(exchange.ContinueAsync(Latin1("x"), CancellationToken.None)));
    }

    [TestMethod]
    public async Task DigestMd5_InitialResponse_IsRefusedAsABadCredential()
    {
        var step = await AfterTheRefusalDelay(Start(Policy(), "DIGEST-MD5", CurlDigestMd5).BeginAsync(CancellationToken.None));

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: DIGEST-MD5", step);
    }

    [TestMethod]
    public async Task DigestMd5_AllowAnonymous_RunsEveryStepAndAcceptsUncheckedWithNoNote()
    {
        var exchange = Start(Policy(allowAnonymous: true), "DIGEST-MD5");

        AssertStep(SaslLoginOutcome.Challenge, CurlDigestMd5Challenge, null, null, await exchange.BeginAsync(CancellationToken.None));
        AssertStep(
            SaslLoginOutcome.Challenge, "rspauth=d41d8cd98f00b204e9800998ecf8427e", null, null,
            await Undelayed(exchange.ContinueAsync(Latin1("nonsense"), CancellationToken.None)));
        AssertStep(
            SaslLoginOutcome.AcceptedUnchecked, string.Empty, null, null,
            await Undelayed(exchange.ContinueAsync(Latin1("x"), CancellationToken.None)));
    }

    [TestMethod]
    public async Task DigestMd5_AllowAnonymousWithAnInitialResponse_AcceptsItUnchecked()
    {
        var step = await Start(Policy(allowAnonymous: true), "DIGEST-MD5", "x").BeginAsync(CancellationToken.None);

        AssertStep(SaslLoginOutcome.AcceptedUnchecked, string.Empty, null, null, step);
    }

    // ---- APOP ----

    private static ApopLogin Apop(string userName, string timestamp, string digest) =>
        new("pop3", userName, timestamp, digest, null);

    [TestMethod]
    public async Task Apop_Rfc1939Section7Example_IsAccepted()
    {
        // RFC 1939, section 7: mrose, secret tanstaaf.
        var policy = Policy(new AccountBook([new Account("mrose", "tanstaaf")]));

        var step = await Undelayed(policy.CheckApopLoginAsync(
            Apop("mrose", "<1896.697170952@dbc.mtview.ca.us>", "c4c9334bac560ecc979e58001b3e22fb"), CancellationToken.None));

        AssertStep(SaslLoginOutcome.Accepted, string.Empty, "mrose", "Login accepted: APOP mrose", step);
    }

    [TestMethod]
    [DataRow(CurlApopDigest, DisplayName = "as curl sent it")]
    [DataRow("32D4437494FDA0AE78D0559952474E34", DisplayName = "upper-case hex")]
    public async Task Apop_CurlsLogin_IsAccepted(string digest)
    {
        var step = await Undelayed(Policy().CheckApopLoginAsync(Apop("user", CurlTimestamp, digest), CancellationToken.None));

        AssertStep(SaslLoginOutcome.Accepted, string.Empty, "user", "Login accepted: APOP user", step);
    }

    [TestMethod]
    [DataRow("user", "00000000000000000000000000000000", "Login refused: APOP user", DisplayName = "wrong digest")]
    [DataRow("nobody", CurlApopDigest, "Login refused: APOP nobody", DisplayName = "unknown user")]
    [DataRow("user", "32d4437494fda0ae78d0559952474e", "Login refused: APOP user", DisplayName = "short digest")]
    [DataRow("user", "32d4437494fda0ae78d0559952474ezz", "Login refused: APOP user", DisplayName = "digest not hex")]
    [DataRow("", CurlApopDigest, "Login refused: APOP", DisplayName = "empty user")]
    public async Task Apop_WrongOrMalformed_IsRefusedAfterTheDelay(string userName, string digest, string note)
    {
        var step = await AfterTheRefusalDelay(Policy().CheckApopLoginAsync(Apop(userName, CurlTimestamp, digest), CancellationToken.None));

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, note, step);
    }

    [TestMethod]
    public async Task Apop_NoAccounts_IsRefusedWithTheSameVerdict()
    {
        var step = await AfterTheRefusalDelay(
            Policy(PolicyFixture.NoAccounts).CheckApopLoginAsync(Apop("user", CurlTimestamp, CurlApopDigest), CancellationToken.None));

        AssertStep(SaslLoginOutcome.RefusedCredentials, string.Empty, null, "Login refused: APOP user", step);
    }

    [TestMethod]
    public async Task Apop_TheBearerTokensAccount_IsNeverMatched()
    {
        var digest = Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(Latin1(CurlTimestamp + "tok")));

        var step = await AfterTheRefusalDelay(Policy().CheckApopLoginAsync(Apop(string.Empty, CurlTimestamp, digest), CancellationToken.None));

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
    }

    [TestMethod]
    public async Task Apop_AllowAnonymous_IsAcceptedUncheckedWithNoNote()
    {
        var step = await Undelayed(
            Policy(allowAnonymous: true).CheckApopLoginAsync(Apop("nobody", CurlTimestamp, "x"), CancellationToken.None));

        AssertStep(SaslLoginOutcome.AcceptedUnchecked, string.Empty, null, null, step);
    }

    // ---- The login note ----

    [TestMethod]
    public async Task EveryNote_NeverHoldsTheSecretOrTheDigest()
    {
        var notes = new List<string?>();
        foreach (var (mechanism, response) in new[] { ("CRAM-MD5", CurlCramMd5), ("DIGEST-MD5", CurlDigestMd5) })
        {
            foreach (var accounts in new[] { SaslExchangeRunner.UserAndToken, PolicyFixture.NoAccounts })
            {
                var exchange = Start(Policy(accounts), mechanism);
                await exchange.BeginAsync(CancellationToken.None);
                var pending = exchange.ContinueAsync(Latin1(response), CancellationToken.None).AsTask();
                clock.Advance(AuthenticationPolicy.RefusalDelay);
                var step = await pending;
                step = step.Outcome == SaslLoginOutcome.Challenge
                    ? await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)
                    : step;
                notes.Add(step.CheckedLogin?.Note);
            }
        }

        foreach (var accounts in new[] { SaslExchangeRunner.UserAndToken, PolicyFixture.NoAccounts })
        {
            var pending = Policy(accounts).CheckApopLoginAsync(Apop("user", CurlTimestamp, CurlApopDigest), CancellationToken.None).AsTask();
            clock.Advance(AuthenticationPolicy.RefusalDelay);
            notes.Add((await pending).CheckedLogin?.Note);
        }

        Assert.AreEqual(6, notes.Count(note => note is not null));
        foreach (var note in notes)
        {
            Assert.IsFalse(note!.Contains("secret", StringComparison.Ordinal), note);
            Assert.IsFalse(note.Contains("79a4ce", StringComparison.Ordinal), note);
            Assert.IsFalse(note.Contains("9ff868", StringComparison.Ordinal), note);
            Assert.IsFalse(note.Contains("32d443", StringComparison.Ordinal), note);
        }
    }
}
