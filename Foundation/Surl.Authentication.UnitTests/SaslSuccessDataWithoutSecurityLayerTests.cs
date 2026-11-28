using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL exchanges started with <see cref="SaslExchangeStart.CanCarryAdditionalSuccessData"/> alone,
/// as a server whose success carries data but which has no security layer starts them (BL-2015):
/// <c>SCRAM</c>'s server-final message and <c>DIGEST-MD5</c>'s <c>rspauth</c> come with the success,
/// and neither <c>DIGEST-MD5</c> nor <c>NTLM</c> offers, grants or installs a security layer.
/// </summary>
[TestClass]
public sealed class SaslSuccessDataWithoutSecurityLayerTests
{
    private const string Nonce = "MDEyMzQ1Njc4OWFiY2RlZg==";

    private const string ChallengeOfferingAuthAlone =
        $"realm=\"surl\",nonce=\"{Nonce}\",qop=\"auth\",charset=utf-8,algorithm=md5-sess";

    private const uint Unicode = NtlmTestMessages.Unicode;

    private const uint Sign = 0x00000010;

    private const uint Seal = 0x00000020;

    private const uint ExtendedSessionSecurity = 0x00080000;

    private const uint KeyExchange = 0x40000000;

    // Sign, seal and key exchange among them: what a client asks a security layer with.
    private const uint SealingClientFlags = 0xE20882B7;

    // ADR-0039's challenge flags for those, which grant neither sign nor seal.
    private const uint ChallengeFlagsWithoutSecurityLayer = 0xA08A8205;

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private readonly ManualTimeProvider clock = new();

    private ISaslExchange Start(string mechanism, byte[]? initialResponse = null, bool allowAnonymous = false) =>
        PolicyFixture.CreateWithFixedNonces(PolicyFixture.AliceAndToken, clock, allowAnonymous, EveryMethod)
            .StartSaslExchange(new SaslExchangeStart(
                "xmpp",
                mechanism,
                initialResponse is null ? null : (ReadOnlyMemory<byte>?)initialResponse,
                null,
                CanCarryAdditionalSuccessData: true));

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.UTF8.GetString(bytes.Span);

    // alice's right response to the fixed nonce, choosing qop and, for auth-conf, an offered cipher.
    private static (string Text, DigestMd5Response Response) ClientResponse(string qop)
    {
        var cipher = qop == "auth-conf" ? "rc4" : null;
        var unsigned = new DigestMd5Response(
            "alice", string.Empty, Nonce, "8aa82d051ad9706a2cbc26aceeb8f21a", "00000001", qop, "xmpp/127.0.0.1", string.Empty, null, cipher);
        var digest = Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponse(unsigned, "secret"u8));
        var cipherDirective = cipher is null ? string.Empty : $",cipher={cipher}";
        var text = $"username=\"alice\",realm=\"\",nonce=\"{Nonce}\",digest-uri=\"xmpp/127.0.0.1\",cnonce=\"8aa82d051ad9706a2cbc26aceeb8f21a\",nc=00000001,response={digest},qop={qop}{cipherDirective},charset=utf-8";

        return (text, DigestMd5Response.Read(text)!);
    }

    private async Task<SaslLoginStep> AfterTheRefusalDelay(ValueTask<SaslLoginStep> pending)
    {
        var step = pending.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await step;
    }

    [TestMethod]
    public async Task ScramSha256_RightProof_IsAcceptedWithTheServerFinalMessageAsAdditionalSuccessData()
    {
        var hash = ScramHash.Sha256;
        var exchange = Start("SCRAM-SHA-256", Utf8("n,,n=alice,r=client"));
        var serverFirst = Text((await exchange.BeginAsync(CancellationToken.None)).Challenge);
        var attributes = serverFirst.Split(',');
        var saltedPassword = hash.Hi(Utf8("secret"), Convert.FromBase64String(attributes[1][2..]), 4096);
        var clientKey = hash.Hmac(saltedPassword, "Client Key"u8);
        var withoutProof = $"c=biws,{attributes[0]}";
        var authMessage = Utf8($"n=alice,r=client,{serverFirst},{withoutProof}");
        var proof = hash.Hmac(hash.Hash(clientKey), authMessage);
        for (var index = 0; index < proof.Length; index++)
        {
            proof[index] ^= clientKey[index];
        }

        var step = await exchange.ContinueAsync(Utf8($"{withoutProof},p={Convert.ToBase64String(proof)}"), CancellationToken.None);

        var serverSignature = hash.Hmac(hash.Hmac(saltedPassword, "Server Key"u8), authMessage);
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("alice", step.AccountName);
        Assert.IsTrue(step.Challenge.IsEmpty);
        Assert.AreEqual($"v={Convert.ToBase64String(serverSignature)}", Text(step.AdditionalSuccessData));
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task DigestMd5_RightResponse_GetsQopAuthAloneThenRspauthAsAdditionalSuccessDataAndNoSecurityLayer()
    {
        var exchange = Start("DIGEST-MD5");
        var (text, response) = ClientResponse("auth");

        var challenge = await exchange.BeginAsync(CancellationToken.None);
        var step = await exchange.ContinueAsync(Utf8(text), CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);
        Assert.AreEqual(ChallengeOfferingAuthAlone, Text(challenge.Challenge));
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("alice", step.AccountName);
        Assert.IsTrue(step.Challenge.IsEmpty);
        Assert.AreEqual(
            "rspauth=" + Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponseAuth(response, "secret"u8)),
            Text(step.AdditionalSuccessData));
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    [DataRow("auth-int")]
    [DataRow("auth-conf")]
    public async Task DigestMd5_ResponseChoosingASecurityLayer_IsRefused(string qop)
    {
        var exchange = Start("DIGEST-MD5");
        await exchange.BeginAsync(CancellationToken.None);

        var step = await AfterTheRefusalDelay(
            exchange.ContinueAsync(Utf8(ClientResponse(qop).Text), CancellationToken.None));

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.IsNull(step.SecurityLayer);
        Assert.IsTrue(step.AdditionalSuccessData.IsEmpty);
    }

    [TestMethod]
    public async Task DigestMd5_EmptyInitialResponse_IsRefusedAsOnAServerWithoutASecurityLayer()
    {
        var step = await AfterTheRefusalDelay(Start("DIGEST-MD5", []).BeginAsync(CancellationToken.None));

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
    }

    [TestMethod]
    public async Task DigestMd5_AllowAnonymous_IsAcceptedUncheckedWithAnRspauthAsAdditionalSuccessData()
    {
        var exchange = Start("DIGEST-MD5", allowAnonymous: true);
        await exchange.BeginAsync(CancellationToken.None);

        var step = await exchange.ContinueAsync(Utf8("anything"), CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.AcceptedUnchecked, step.Outcome);
        Assert.AreEqual("rspauth=d41d8cd98f00b204e9800998ecf8427e", Text(step.AdditionalSuccessData));
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task DigestMd5_AllowAnonymousWithAnInitialResponse_IsAcceptedUncheckedAtOnceWithNoData()
    {
        var step = await Start("DIGEST-MD5", Utf8("anything"), allowAnonymous: true).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.AcceptedUnchecked, step.Outcome);
        Assert.IsTrue(step.AdditionalSuccessData.IsEmpty);
    }

    [TestMethod]
    public async Task Ntlm_NegotiateAskingForSealing_IsGrantedNeitherSignNorSealAndAcceptedWithNoSecurityLayer()
    {
        var exchange = Start("NTLM", NtlmTestMessages.Negotiate(SealingClientFlags));

        var challenge = await exchange.BeginAsync(CancellationToken.None);
        var step = await exchange.ContinueAsync(
            LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", Unicode | Sign | Seal | ExtendedSessionSecurity | KeyExchange),
            CancellationToken.None);

        var granted = BitConverter.ToUInt32(challenge.Challenge.Span[20..24]);
        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);
        Assert.AreEqual(ChallengeFlagsWithoutSecurityLayer, granted);
        Assert.AreEqual(0u, granted & (Sign | Seal));
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("alice", step.AccountName);
        Assert.IsNull(step.SecurityLayer);
        Assert.IsTrue(step.AdditionalSuccessData.IsEmpty);
    }

    [TestMethod]
    public async Task Ntlm_AllowAnonymousAndAUserWithNoAccount_IsAcceptedUncheckedWithNoSecurityLayer()
    {
        var exchange = Start("NTLM", NtlmTestMessages.Negotiate(SealingClientFlags), allowAnonymous: true);
        await exchange.BeginAsync(CancellationToken.None);

        var step = await exchange.ContinueAsync(
            LdapNtlmSaslMechanismTests.Authenticate("nobody", "wrong", Unicode | Sign | Seal | ExtendedSessionSecurity | KeyExchange),
            CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.AcceptedUnchecked, step.Outcome);
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task Ntlm_SpnegoFirstToken_IsNotUnwrapped()
    {
        var token = SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], NtlmTestMessages.Negotiate(SealingClientFlags));

        var step = await AfterTheRefusalDelay(Start("NTLM", token).BeginAsync(CancellationToken.None));

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.IsTrue(step.Challenge.IsEmpty);
    }

    [TestMethod]
    public async Task GssSpnego_IsRefusedAsAMechanismNotOffered()
    {
        var step = await Start("GSS-SPNEGO").BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.RefusedMechanism, step.Outcome);
    }
}
