using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The <see cref="CheckedLogin"/> each HTTP verdict carries for ADR-0032 section 8's
/// verbose-log note: the <c>Authorization</c> scheme, the user as sent (<c>bearer token</c> for
/// Bearer), accepted or refused - and never a password, a token or an <c>Authorization</c> value.
/// </summary>
[TestClass]
public sealed class CheckedLoginNoteTests
{
    private static readonly AccountBook Accounts = new(
    [
        new Account("tester", "secret"),
        new Account(string.Empty, "Zq7-access"),
    ]);

    private readonly ManualTimeProvider clock = new();

    private async Task<HttpAuthenticationVerdict> JudgeAsync(string authorization, params IHttpAuthenticationMethod[] httpMethods)
    {
        var policy = PolicyFixture.Create(Accounts, clock, httpMethods: httpMethods);
        var judgement = policy.StartHttpConnection(PolicyFixture.Tls)
            .JudgeAsync(PolicyFixture.Get(authorization), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await judgement;
    }

    private static string Basic(string userPass) => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userPass));

    private static void AssertNoSecretIn(HttpAuthenticationVerdict verdict, string authorization, string secret)
    {
        var note = verdict.CheckedLogin!.Note;
        Assert.DoesNotContain(secret, note);
        Assert.DoesNotContain(authorization, note);
        Assert.DoesNotContain(authorization[(authorization.IndexOf(' ', StringComparison.Ordinal) + 1)..], note);
    }

    [TestMethod]
    [DataRow("tester:secret", "Login accepted: Basic tester", DisplayName = "right password")]
    [DataRow("tester:wrong", "Login refused: Basic tester", DisplayName = "wrong password")]
    [DataRow("nobody:secret", "Login refused: Basic nobody", DisplayName = "unknown user")]
    public async Task Basic_Checked_CarriesTheUserAsSent(string userPass, string note)
    {
        var authorization = Basic(userPass);

        var verdict = await JudgeAsync(authorization, new BasicAuthenticationMethod(Accounts));

        Assert.AreEqual(note, verdict.CheckedLogin!.Note);
        AssertNoSecretIn(verdict, authorization, userPass[(userPass.IndexOf(':', StringComparison.Ordinal) + 1)..]);
    }

    [TestMethod]
    [DataRow("Basic !!!", DisplayName = "not base64")]
    [DataRow("Basic dGVzdGVy", DisplayName = "no colon")]
    public async Task Basic_NoUserReadable_IsRefusedWithoutAUser(string authorization)
    {
        var verdict = await JudgeAsync(authorization, new BasicAuthenticationMethod(Accounts));

        Assert.AreEqual("Login refused: Basic", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public async Task Basic_UserNotUtf8_IsRefusedWithoutAUser()
    {
        var authorization = "Basic " + Convert.ToBase64String([0xFF, (byte)':', (byte)'x']);

        var verdict = await JudgeAsync(authorization, new BasicAuthenticationMethod(Accounts));

        Assert.AreEqual("Login refused: Basic", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    [DataRow("Zq7-access", "Login accepted: Bearer bearer token", DisplayName = "right token")]
    [DataRow("wrong", "Login refused: Bearer bearer token", DisplayName = "wrong token")]
    public async Task Bearer_Checked_SaysBearerTokenNotTheToken(string token, string note)
    {
        var authorization = "Bearer " + token;

        var verdict = await JudgeAsync(authorization, new BearerAuthenticationMethod(Accounts));

        Assert.AreEqual(note, verdict.CheckedLogin!.Note);
        AssertNoSecretIn(verdict, authorization, token);
    }

    [TestMethod]
    public async Task Digest_Accepted_CarriesTheUserAsSent()
    {
        var nonces = new DigestNonceBook(clock);
        var digest = new DigestAuthenticationMethod(Accounts, nonces);
        var authorization = DigestAnswer(nonces.Issue(), "secret");

        var verdict = await JudgeAsync(authorization, digest);

        Assert.AreEqual("Login accepted: Digest tester", verdict.CheckedLogin!.Note);
        AssertNoSecretIn(verdict, authorization, "secret");
    }

    [TestMethod]
    public async Task Digest_WrongPassword_IsRefusedWithTheUserAsSent()
    {
        var nonces = new DigestNonceBook(clock);
        var authorization = DigestAnswer(nonces.Issue(), "wrong");

        var verdict = await JudgeAsync(authorization, new DigestAuthenticationMethod(Accounts, nonces));

        Assert.AreEqual("Login refused: Digest tester", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public async Task Digest_AnotherTarget_IsRefusedWithTheUserAsSent()
    {
        var nonces = new DigestNonceBook(clock);
        var authorization = DigestAnswer(nonces.Issue(), "secret", uri: "/elsewhere");

        var verdict = await JudgeAsync(authorization, new DigestAuthenticationMethod(Accounts, nonces));

        Assert.AreEqual("Login refused: Digest tester", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public async Task Digest_Malformed_IsRefusedWithoutAUser()
    {
        var verdict = await JudgeAsync("Digest username=\"tester\"", new DigestAuthenticationMethod(Accounts, new DigestNonceBook(clock)));

        Assert.AreEqual("Login refused: Digest", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public async Task ContinuationStep_CarriesNoCheckedLogin()
    {
        var ntlm = new ScriptedHttpAuthenticationMethod(
            AuthenticationMethod.Ntlm, ["NTLM"], new HttpCredentialCheck(HttpCredentialOutcome.Continue, null, ["NTLM type2"], "alice"));
        var policy = PolicyFixture.Create(
            Accounts, clock, acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Ntlm }, httpMethods: ntlm);

        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get("NTLM type1"), CancellationToken.None);

        Assert.IsNull(verdict.CheckedLogin);
    }

    [TestMethod]
    public async Task NoCredentials_CarriesNoCheckedLogin()
    {
        var verdict = await JudgeAsync("Unknown x", new BasicAuthenticationMethod(Accounts));

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        Assert.IsNull(verdict.CheckedLogin);
    }

    [TestMethod]
    [DataRow(AuthenticationMethod.Negotiate, "Negotiate")]
    [DataRow(AuthenticationMethod.Ntlm, "NTLM")]
    [DataRow(AuthenticationMethod.Digest, "Digest")]
    [DataRow(AuthenticationMethod.Basic, "Basic")]
    [DataRow(AuthenticationMethod.Bearer, "Bearer")]
    [DataRow(AuthenticationMethod.AwsSigV4, "AWS4-HMAC-SHA256")]
    public void AuthorizationSchemeOf_EachMethod_IsTheSchemeThatNamesIt(AuthenticationMethod method, string scheme)
    {
        Assert.AreEqual(scheme, AuthenticationMethods.AuthorizationSchemeOf(method));
        Assert.IsTrue(AuthenticationMethods.TryFromAuthorizationScheme(scheme, out var named));
        Assert.AreEqual(method, named);
    }

    private static string DigestAnswer(string nonce, string password, string uri = "/x")
    {
        var name = DigestAlgorithmName.Parse("MD5")!;
        var userHash = DigestCalculation.ComputeUserHash(name.Algorithm, Encoding.UTF8, "tester", "surl", password);
        var inputs = new DigestResponseInputs("GET", uri, nonce, "00000001", "0a1b2c", "auth");
        var response = DigestCalculation.ComputeResponse(
            name.Algorithm, DigestCalculation.ComputeA1Hash(name, userHash, nonce, "0a1b2c"), inputs);

        return $"Digest username=\"tester\",realm=\"surl\",nonce=\"{nonce}\",uri=\"{uri}\",cnonce=\"0a1b2c\","
            + $"nc=00000001,algorithm=MD5,response=\"{response}\",qop=\"auth\"";
    }
}
