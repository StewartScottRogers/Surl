using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="ScramSaslExchange"/>, SASL <c>SCRAM-SHA-1</c> (RFC 5802) and <c>SCRAM-SHA-256</c>
/// (RFC 7677) without channel binding: the RFCs' own examples (user <c>user</c>, password
/// <c>pencil</c>, their nonces and salts) byte for byte, and every refusal.
/// </summary>
[TestClass]
public sealed class ScramSaslMechanismTests
{
    // RFC 5802 section 5.
    private const string Sha1ClientFirst = "n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL";
    private const string Sha1ServerNonce = "3rfcNHYJY1ZVvWVs7j";
    private const string Sha1ServerFirst = "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096";
    private const string Sha1ClientFinal = "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=";
    private const string Sha1ServerFinal = "v=rmF9pqV8S7suAoZWja4dJRkFsKQ=";

    // RFC 7677 section 3.
    private const string Sha256ClientFirst = "n,,n=user,r=rOprNGfwEbeRWgbNEkqO";
    private const string Sha256ServerNonce = "%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0";
    private const string Sha256ServerFirst = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096";
    private const string Sha256ClientFinal = "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=";
    private const string Sha256ServerFinal = "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=";

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private static readonly AccountBook RfcAccounts = new(
        [new Account("user", "pencil"), new Account("a,b=c", "pencil"), new Account(string.Empty, "tok")],
        CryptographicSecretComparer.Instance,
        hash => Convert.FromBase64String(hash == ScramHash.Sha1 ? "QSXCR+Q6sek8bf92" : "W22ZaJ0SNY7soEsUEjb6gQ=="));

    private readonly ManualTimeProvider clock = new(DateTimeOffset.FromUnixTimeSeconds(1790640000));

    private AuthenticationPolicy Policy(AccountBook? accounts = null, bool allowAnonymous = false) =>
        PolicyFixture.CreateWithFixedNonces(accounts ?? RfcAccounts, clock, allowAnonymous, EveryMethod);

    private ScramSaslExchange Exchange(
        ScramHash hash, string? initialResponse, string serverNonce, bool allowAnonymous = false, bool canCarrySecurityLayer = false) =>
        new(
            new SaslExchangeContext(
                Policy(allowAnonymous: allowAnonymous),
                "xmpp",
                hash.MechanismName,
                initialResponse is null ? null : Ascii(initialResponse),
                null,
                canCarrySecurityLayer),
            hash,
            () => serverNonce);

    private ScramSaslExchange Sha1Exchange(string clientFirst = Sha1ClientFirst, bool allowAnonymous = false) =>
        Exchange(ScramHash.Sha1, clientFirst, Sha1ServerNonce, allowAnonymous);

    private static byte[] Ascii(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.UTF8.GetString(bytes.Span);

    private static async Task<SaslLoginStep> Undelayed(ValueTask<SaslLoginStep> pending)
    {
        Assert.IsTrue(pending.IsCompleted);

        return await pending;
    }

    private async Task<SaslLoginStep> AfterTheRefusalDelay(ValueTask<SaslLoginStep> pending)
    {
        var step = pending.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(step.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        return await step;
    }

    private async Task AssertRefusedAsync(ValueTask<SaslLoginStep> pending, string serverFinal, string? note)
    {
        var step = await AfterTheRefusalDelay(pending);

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.AreEqual(serverFinal, step.RefusalNote);
        Assert.AreEqual(note, step.CheckedLogin?.Note);
        Assert.IsNull(step.AccountName);
    }

    // What a client sends after serverFirst: c=<gs2 header>, the nonce as given, the proof of password.
    private static string ClientFinal(
        ScramHash hash, string password, string clientFirst, string serverFirst, string? nonce = null, string channelBinding = "biws")
    {
        var attributes = serverFirst.Split(',');
        var salt = Convert.FromBase64String(attributes[1][2..]);
        var withoutProof = $"c={channelBinding},r={nonce ?? attributes[0][2..]}";
        var bare = clientFirst[(clientFirst.IndexOf(",n=", StringComparison.Ordinal) + 1)..];
        var saltedPassword = hash.Hi(Encoding.UTF8.GetBytes(password), salt, int.Parse(attributes[2][2..], System.Globalization.CultureInfo.InvariantCulture));
        var clientKey = hash.Hmac(saltedPassword, "Client Key"u8);
        var signature = hash.Hmac(hash.Hash(clientKey), Encoding.UTF8.GetBytes($"{bare},{serverFirst},{withoutProof}"));
        for (var index = 0; index < signature.Length; index++)
        {
            signature[index] ^= clientKey[index];
        }

        return $"{withoutProof},p={Convert.ToBase64String(signature)}";
    }

    // ---- The RFCs' examples ----

    [TestMethod]
    public async Task Exchange_Rfc5802Section5Example_SendsTheRfcsServerMessagesAndAccepts()
    {
        var exchange = Sha1Exchange();

        var serverFirst = await Undelayed(exchange.BeginAsync(CancellationToken.None));
        Assert.AreEqual(SaslLoginOutcome.Challenge, serverFirst.Outcome);
        Assert.AreEqual(Sha1ServerFirst, Text(serverFirst.Challenge));

        var serverFinal = await Undelayed(exchange.ContinueAsync(Ascii(Sha1ClientFinal), CancellationToken.None));
        Assert.AreEqual(SaslLoginOutcome.Challenge, serverFinal.Outcome);
        Assert.AreEqual(Sha1ServerFinal, Text(serverFinal.Challenge));

        var accepted = await Undelayed(exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
        Assert.AreEqual(SaslLoginOutcome.Accepted, accepted.Outcome);
        Assert.AreEqual("user", accepted.AccountName);
        Assert.AreEqual("Login accepted: SCRAM-SHA-1 user", accepted.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task Exchange_Rfc7677Section3Example_SendsTheRfcsServerMessagesAndAccepts()
    {
        var exchange = Exchange(ScramHash.Sha256, Sha256ClientFirst, Sha256ServerNonce);

        var serverFirst = await Undelayed(exchange.BeginAsync(CancellationToken.None));
        Assert.AreEqual(Sha256ServerFirst, Text(serverFirst.Challenge));

        var serverFinal = await Undelayed(exchange.ContinueAsync(Ascii(Sha256ClientFinal), CancellationToken.None));
        Assert.AreEqual(Sha256ServerFinal, Text(serverFinal.Challenge));

        var accepted = await Undelayed(exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
        Assert.AreEqual(SaslLoginOutcome.Accepted, accepted.Outcome);
        Assert.AreEqual("Login accepted: SCRAM-SHA-256 user", accepted.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task Exchange_WhereSuccessCarriesData_AcceptsWithTheServerFinalMessageAsAdditionalSuccessData()
    {
        var exchange = Exchange(ScramHash.Sha256, Sha256ClientFirst, Sha256ServerNonce, canCarrySecurityLayer: true);
        await exchange.BeginAsync(CancellationToken.None);

        var accepted = await Undelayed(exchange.ContinueAsync(Ascii(Sha256ClientFinal), CancellationToken.None));

        Assert.AreEqual(SaslLoginOutcome.Accepted, accepted.Outcome);
        Assert.AreEqual("user", accepted.AccountName);
        Assert.AreEqual(Sha256ServerFinal, Text(accepted.AdditionalSuccessData));
        Assert.IsNull(accepted.SecurityLayer);
    }

    // ---- Through the policy ----

    [TestMethod]
    [DataRow("SCRAM-SHA-1", "QSXCR+Q6sek8bf92")]
    [DataRow("scram-sha-256", "W22ZaJ0SNY7soEsUEjb6gQ==")]
    public async Task StartSaslExchange_NoInitialResponse_AsksForTheClientFirstMessageThenIssuesTheNonceSourcesNonce(
        string mechanism, string salt)
    {
        var exchange = SaslExchangeRunner.Start(Policy(), mechanism, null, null);

        var empty = await Undelayed(exchange.BeginAsync(CancellationToken.None));
        Assert.AreEqual(SaslLoginOutcome.Challenge, empty.Outcome);
        Assert.IsTrue(empty.Challenge.IsEmpty);

        var serverFirst = await Undelayed(exchange.ContinueAsync(Ascii("n,,n=user,r=abc"), CancellationToken.None));
        Assert.AreEqual($"r=abcMDEyMzQ1Njc4OWFiY2RlZg==,s={salt},i=4096", Text(serverFirst.Challenge));
    }

    [TestMethod]
    public async Task StartSaslExchange_ARealClientsLogin_IsAcceptedWithRandomSaltAndNonce()
    {
        var policy = PolicyFixture.Create(new AccountBook([new Account("alice", "s3cret")]), clock, acceptedMethods: EveryMethod);
        var exchange = SaslExchangeRunner.Start(policy, "SCRAM-SHA-256", Ascii("y,,n=alice,r=client"), null);

        var serverFirst = Text((await exchange.BeginAsync(CancellationToken.None)).Challenge);
        StringAssert.Matches(serverFirst, new System.Text.RegularExpressions.Regex("^r=client[A-Za-z0-9+/]{24},s=[A-Za-z0-9+/]{22}==,i=4096$"));
        var final = ClientFinal(ScramHash.Sha256, "s3cret", "y,,n=alice,r=client", serverFirst, channelBinding: "eSws");
        await exchange.ContinueAsync(Ascii(final), CancellationToken.None);

        var accepted = await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        Assert.AreEqual("alice", accepted.AccountName);
    }

    // ---- Refusals ----

    [TestMethod]
    public async Task Exchange_WrongProof_IsRefusedWithInvalidProof()
    {
        var exchange = Sha1Exchange();
        await exchange.BeginAsync(CancellationToken.None);

        var final = ClientFinal(ScramHash.Sha1, "wrong", Sha1ClientFirst, Sha1ServerFirst);

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii(final), CancellationToken.None), "e=invalid-proof", "Login refused: SCRAM-SHA-1 user");
    }

    [TestMethod]
    public async Task Exchange_ProofOfTheWrongLength_IsRefusedWithInvalidProof()
    {
        var exchange = Sha1Exchange();
        await exchange.BeginAsync(CancellationToken.None);

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=AAAA"), CancellationToken.None),
            "e=invalid-proof",
            "Login refused: SCRAM-SHA-1 user");
    }

    [TestMethod]
    public async Task Exchange_UnknownUser_GetsAConsistentSaltAndTheSameRefusalAsAWrongPassword()
    {
        const string ClientFirst = "n,,n=nobody,r=fyko+d2lbbFgONRv9qkxdawL";
        var exchange = Sha1Exchange(ClientFirst);
        var again = Sha1Exchange(ClientFirst);
        var other = Sha1Exchange("n,,n=somebody,r=fyko+d2lbbFgONRv9qkxdawL");

        var serverFirst = Text((await exchange.BeginAsync(CancellationToken.None)).Challenge);
        Assert.AreEqual(serverFirst, Text((await again.BeginAsync(CancellationToken.None)).Challenge));
        Assert.AreNotEqual(serverFirst, Text((await other.BeginAsync(CancellationToken.None)).Challenge));
        StringAssert.Matches(serverFirst, new System.Text.RegularExpressions.Regex("^r=fyko\\+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=[A-Za-z0-9+/]{22}==,i=4096$"));

        var final = ClientFinal(ScramHash.Sha1, "pencil", ClientFirst, serverFirst);

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii(final), CancellationToken.None), "e=invalid-proof", "Login refused: SCRAM-SHA-1 nobody");
    }

    [TestMethod]
    [DataRow("c=biws,r=fyko+d2lbbFgONRv9qkxdawL,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "the client's nonce alone")]
    [DataRow("c=biws,r=XXXX+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "not starting with the client's nonce")]
    [DataRow("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7jX,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "longer than the issued nonce")]
    public async Task Exchange_NonceThatIsNotTheIssuedOne_IsRefusedWithOtherError(string clientFinal)
    {
        var exchange = Sha1Exchange();
        await exchange.BeginAsync(CancellationToken.None);

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii(clientFinal), CancellationToken.None), "e=other-error", "Login refused: SCRAM-SHA-1 user");
    }

    [TestMethod]
    [DataRow("", "e=invalid-encoding", DisplayName = "empty")]
    [DataRow("n,,n=user", "e=invalid-encoding", DisplayName = "no nonce")]
    [DataRow("x,,n=user,r=abc", "e=invalid-encoding", DisplayName = "unknown flag")]
    [DataRow("p=tls-unique,,n=user,r=abc", "e=channel-binding-not-supported", DisplayName = "channel binding asked for")]
    [DataRow("n,,m=ext,n=user,r=abc", "e=extensions-not-supported", DisplayName = "mandatory extension")]
    [DataRow("n,b=user,n=user,r=abc", "e=invalid-encoding", DisplayName = "authzid not a=")]
    [DataRow("n,a=,n=user,r=abc", "e=invalid-encoding", DisplayName = "empty authzid")]
    [DataRow("n,,u=user,r=abc", "e=invalid-encoding", DisplayName = "user not n=")]
    [DataRow("n,,n=,r=abc", "e=invalid-encoding", DisplayName = "empty user")]
    [DataRow("n,,n=us=er,r=abc", "e=invalid-encoding", DisplayName = "bare equals in user")]
    [DataRow("n,,n=user=2,r=abc", "e=invalid-encoding", DisplayName = "cut escape in user")]
    [DataRow("n,,n=user,s=abc", "e=invalid-encoding", DisplayName = "nonce not r=")]
    [DataRow("n,,n=user,r=", "e=invalid-encoding", DisplayName = "empty nonce")]
    [DataRow("n,,n=user,r=a b", "e=invalid-encoding", DisplayName = "nonce with a space")]
    public async Task Exchange_MalformedClientFirstMessage_IsRefusedWithItsServerError(string clientFirst, string serverFinal)
    {
        var exchange = Sha1Exchange(clientFirst);

        await AssertRefusedAsync(exchange.BeginAsync(CancellationToken.None), serverFinal, "Login refused: SCRAM-SHA-1");
    }

    [TestMethod]
    public async Task Exchange_ClientFirstMessageNotUtf8_IsRefusedWithInvalidEncoding()
    {
        var policy = Policy();
        var exchange = SaslExchangeRunner.Start(policy, "SCRAM-SHA-1", [(byte)'n', (byte)',', (byte)',', 0xFF], null);

        await AssertRefusedAsync(exchange.BeginAsync(CancellationToken.None), "e=invalid-encoding", "Login refused: SCRAM-SHA-1");
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("c=biws,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "no nonce")]
    [DataRow("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j", DisplayName = "no proof")]
    [DataRow("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,x=1", DisplayName = "proof not p=")]
    [DataRow("b=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "channel binding not c=")]
    [DataRow("c=b!ws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "channel binding not base64")]
    [DataRow("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8 v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "proof with a space")]
    [DataRow("c=biws,r=,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", DisplayName = "empty nonce")]
    [DataRow("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Tsé", DisplayName = "not ASCII")]
    public async Task Exchange_MalformedClientFinalMessage_IsRefusedWithInvalidEncoding(string clientFinal)
    {
        var exchange = Sha1Exchange();
        await exchange.BeginAsync(CancellationToken.None);

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii(clientFinal), CancellationToken.None), "e=invalid-encoding", "Login refused: SCRAM-SHA-1 user");
    }

    [TestMethod]
    public async Task Exchange_ChannelBindingThatIsNotTheGs2Header_IsRefusedWithChannelBindingsDontMatch()
    {
        var exchange = Sha1Exchange();
        await exchange.BeginAsync(CancellationToken.None);

        var final = ClientFinal(ScramHash.Sha1, "pencil", Sha1ClientFirst, Sha1ServerFirst, channelBinding: "eSws");

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii(final), CancellationToken.None), "e=channel-bindings-dont-match", "Login refused: SCRAM-SHA-1 user");
    }

    [TestMethod]
    public async Task Exchange_AuthorizationIdOfAnotherUser_IsRefusedWithOtherError()
    {
        const string ClientFirst = "n,a=admin,n=user,r=fyko+d2lbbFgONRv9qkxdawL";
        var exchange = Sha1Exchange(ClientFirst);
        await exchange.BeginAsync(CancellationToken.None);

        var final = ClientFinal(ScramHash.Sha1, "pencil", ClientFirst, Sha1ServerFirst, channelBinding: "bixhPWFkbWluLA==");

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii(final), CancellationToken.None), "e=other-error", "Login refused: SCRAM-SHA-1 user");
    }

    [TestMethod]
    public async Task Exchange_EscapedUserAndAuthorizationIdOfThatUser_IsAccepted()
    {
        const string ClientFirst = "n,a=a=2Cb=3Dc,n=a=2Cb=3Dc,r=fyko+d2lbbFgONRv9qkxdawL";
        var exchange = Sha1Exchange(ClientFirst);
        var serverFirst = Text((await exchange.BeginAsync(CancellationToken.None)).Challenge);

        var gs2Header = Convert.ToBase64String(Encoding.UTF8.GetBytes("n,a=a=2Cb=3Dc,"));
        var final = ClientFinal(ScramHash.Sha1, "pencil", ClientFirst, serverFirst, channelBinding: gs2Header);
        await exchange.ContinueAsync(Ascii(final), CancellationToken.None);

        var accepted = await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        Assert.AreEqual("a,b=c", accepted.AccountName);
        Assert.AreEqual("Login accepted: SCRAM-SHA-1 a,b=c", accepted.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task Exchange_NonEmptyAnswerToTheServerFinalMessage_IsRefused()
    {
        var exchange = Sha1Exchange();
        await exchange.BeginAsync(CancellationToken.None);
        await exchange.ContinueAsync(Ascii(Sha1ClientFinal), CancellationToken.None);

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii("x"), CancellationToken.None), "e=invalid-encoding", "Login refused: SCRAM-SHA-1 user");
    }

    // ---- --allow-anonymous ----

    [TestMethod]
    public async Task Exchange_AllowAnonymous_RunsEveryStepAndAcceptsAWrongProofUnchecked()
    {
        var exchange = Sha1Exchange(allowAnonymous: true);
        await exchange.BeginAsync(CancellationToken.None);

        var final = ClientFinal(ScramHash.Sha1, "wrong", Sha1ClientFirst, Sha1ServerFirst);
        var serverFinal = await Undelayed(exchange.ContinueAsync(Ascii(final), CancellationToken.None));
        Assert.AreEqual(SaslLoginOutcome.Challenge, serverFinal.Outcome);
        StringAssert.StartsWith(Text(serverFinal.Challenge), "v=");

        var accepted = await Undelayed(exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
        Assert.AreEqual(SaslLoginOutcome.AcceptedUnchecked, accepted.Outcome);
        Assert.IsNull(accepted.CheckedLogin);
    }

    [TestMethod]
    public async Task Exchange_AllowAnonymousMalformedFinalMessage_IsStillRefused()
    {
        var exchange = Sha1Exchange(allowAnonymous: true);
        await exchange.BeginAsync(CancellationToken.None);

        await AssertRefusedAsync(
            exchange.ContinueAsync(Ascii("c=biws"), CancellationToken.None), "e=invalid-encoding", "Login refused: SCRAM-SHA-1 user");
    }

    // ---- SASLprep ----

    [TestMethod]
    [DataRow("pencil", "pencil", DisplayName = "printable ASCII")]
    [DataRow("I­X", "IX", DisplayName = "soft hyphen mapped to nothing")]
    [DataRow("a b　c", "a b c", DisplayName = "non-ASCII spaces mapped to space")]
    [DataRow("Ⅸ", "IX", DisplayName = "normalization form KC")]
    [DataRow("ª", "a", DisplayName = "RFC 4013 section 3 example: ordinal indicator")]
    public void Normalize_IsSaslPrepsMappingAndNormalization(string text, string expected)
    {
        Assert.AreEqual(expected, SaslPrep.Normalize(text));
    }

    [TestMethod]
    public async Task Exchange_PasswordWithNonAsciiSpace_IsCheckedAfterSaslPrep()
    {
        var policy = PolicyFixture.Create(new AccountBook([new Account("bob", "a b")]), clock, acceptedMethods: EveryMethod);
        var exchange = SaslExchangeRunner.Start(policy, "SCRAM-SHA-1", Ascii("n,,n=bob,r=c"), null);
        var serverFirst = Text((await exchange.BeginAsync(CancellationToken.None)).Challenge);

        await exchange.ContinueAsync(Ascii(ClientFinal(ScramHash.Sha1, "a b", "n,,n=bob,r=c", serverFirst)), CancellationToken.None);

        var accepted = await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        Assert.AreEqual("bob", accepted.AccountName);
    }
}
