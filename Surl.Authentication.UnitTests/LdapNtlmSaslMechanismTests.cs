using System.Security.Cryptography;
using Surl.Cryptography.Rc4;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// LDAP's <c>NTLM</c> (Sicily) and <c>GSS-SPNEGO</c> binds, started with
/// <see cref="SaslExchangeStart.CanCarrySecurityLayer"/> (ADR-0072, decision 4): the recordings
/// <c>Fixtures/ldap-ntlm-sealed</c> and <c>Fixtures/ldap-negotiate-sealed</c> of pinned upstream
/// curl 8.21.0's <c>WinLDAP</c> answering Surl's own LDAP challenge for <c>alice:secret</c> and
/// sealing its first search, and the flag and <c>--allow-anonymous</c> cases around them.
/// </summary>
[TestClass]
public sealed class LdapNtlmSaslMechanismTests
{
    // The base search of ADR-0072's simple-bind transcript, message ID 2 at offset 8.
    private static readonly byte[] BaseSearch = Convert.FromHexString(
        "30840000003E020102638400000035041164633D6578616D706C652C64633D636F6D0A01000A0100020100020100010100870B4F626A656374436C617373308400000000");

    // WinLDAP's NEGOTIATE_MESSAGE flags in both recordings.
    private const uint WinLdapNegotiateFlags = 0xE20882B7;

    // What Surl's LDAP challenge grants them: the HTTP set plus sign, seal and key exchange.
    private const uint LdapChallengeFlags = 0xE08A8235;

    // What ADR-0039's HTTP challenge grants them.
    private const uint HttpChallengeFlags = 0xA08A8205;

    private const uint Unicode = NtlmTestMessages.Unicode;

    private const uint Sign = 0x00000010;

    private const uint Seal = 0x00000020;

    private const uint ExtendedSessionSecurity = 0x00080000;

    private const uint KeyExchange = 0x40000000;

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private static readonly byte[] ExportedSessionKey = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy Policy(bool allowAnonymous = false, IReadOnlySet<AuthenticationMethod>? acceptedMethods = null) =>
        PolicyFixture.CreateWithFixedNonces(PolicyFixture.AliceAndToken, clock, allowAnonymous, acceptedMethods ?? EveryMethod);

    private static ISaslExchange Start(AuthenticationPolicy policy, string mechanism, bool canCarrySecurityLayer = true) =>
        policy.StartSaslExchange(new SaslExchangeStart(
            "ldap", mechanism, NtlmTestMessages.Negotiate(WinLdapNegotiateFlags), null, canCarrySecurityLayer));

    private async Task<SaslLoginStep> LoginAsync(ISaslExchange exchange, byte[] authenticate)
    {
        var challenge = await exchange.BeginAsync(CancellationToken.None);
        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);

        var pending = exchange.ContinueAsync(authenticate, CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await pending;
    }

    // The hex after marker on each transcript line that has it, in order.
    private static List<byte[]> TranscriptHex(string caseName, string marker) =>
        [.. RecordedFixture.ReadText(caseName, "transcript.txt")
            .Split("\r\n")
            .Where(line => line.Contains(marker, StringComparison.Ordinal))
            .Select(line => Convert.FromHexString(line[(line.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]))];

    // An NTLMv2 AUTHENTICATE_MESSAGE from user with password to the fixed challenge, sending
    // ExportedSessionKey RC4-encrypted under the session base key when the flags ask for key exchange.
    private static byte[] Authenticate(string user, string password, uint flags)
    {
        var responseKey = NtlmV2Calculation.ComputeResponseKeyNt(NtlmV2Calculation.ComputeNtHash(password), user, string.Empty);
        var proof = NtlmV2Calculation.ComputeNtProof(responseKey, FixedNtlmServerChallengeSource.FixtureChallenge, NtlmTestMessages.ClientBlob);
        byte[] encryptedSessionKey = [];
        if ((flags & KeyExchange) != 0)
        {
            encryptedSessionKey = new byte[16];
            new Rc4(NtlmV2Calculation.ComputeSessionBaseKey(responseKey, proof), 0).ApplyKeyStream(ExportedSessionKey, encryptedSessionKey);
        }

        return NtlmTestMessages.Authenticate(user, string.Empty, [.. proof, .. NtlmTestMessages.ClientBlob], flags, encryptedSessionKey);
    }

    private static void AssertRefused(string? refusalNote, SaslLoginStep step)
    {
        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.IsNull(step.AccountName);
        Assert.IsNull(step.SecurityLayer);
        Assert.AreEqual(refusalNote, step.RefusalNote);
    }

    [TestMethod]
    [DataRow("ldap-ntlm-sealed", "NTLM", "choice 0x8A ", "choice 0x8B ", 4)]
    [DataRow("ldap-negotiate-sealed", "GSS-SPNEGO", "sasl GSS-SPNEGO credentials ", "sasl GSS-SPNEGO credentials ", 5)]
    public async Task WinLdapsRecordedBind_IsAcceptedAndItsFirstBufferUnsealsToTheBaseSearch(
        string caseName, string mechanism, string negotiateMarker, string authenticateMarker, int searchMessageId)
    {
        var negotiate = TranscriptHex(caseName, negotiateMarker)[0];
        var authenticate = TranscriptHex(caseName, authenticateMarker).First(message => message[8] == 3);
        var buffer = TranscriptHex(caseName, "SASL-wrapped buffer of 84 bytes: ")[0];
        var exchange = Policy().StartSaslExchange(new SaslExchangeStart("ldap", mechanism, negotiate, null, true));

        var challenge = await exchange.BeginAsync(CancellationToken.None);
        var step = await exchange.ContinueAsync(authenticate, CancellationToken.None);

        // The recorder sent this very challenge, so curl answered Surl's own.
        CollectionAssert.AreEqual(NtlmChallengeMessage.Create((NtlmNegotiateFlags)WinLdapNegotiateFlags, FixedNtlmServerChallengeSource.FixtureChallenge, true), challenge.Challenge.ToArray());
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("alice", step.AccountName);
        Assert.AreEqual($"Login accepted: {mechanism} alice", step.CheckedLogin?.Note);
        Assert.IsNotNull(step.SecurityLayer);
        Assert.AreEqual(84, buffer.Length - 4);
        Assert.IsTrue(step.SecurityLayer.TryUnprotect(buffer.AsSpan(4), out var search));
        byte[] expected = [.. BaseSearch];
        expected[8] = (byte)searchMessageId;
        CollectionAssert.AreEqual(expected, search);
    }

    [TestMethod]
    public void RecordedChallenge_IsTheOneTheRecordingsScripted()
    {
        var scripted = Convert.FromHexString(
            "4E544C4D53535000020000000800080030000000" + "35828AE0" + "0123456789ABCDEF" + "0000000000000000"
            + "1C001C0038000000" + "5300550052004C00" + "020008005300550052004C00" + "010008005300550052004C00" + "00000000");

        CollectionAssert.AreEqual(
            scripted,
            NtlmChallengeMessage.Create((NtlmNegotiateFlags)WinLdapNegotiateFlags, FixedNtlmServerChallengeSource.FixtureChallenge, true));
    }

    [TestMethod]
    public void ChooseFlags_LdapAndHttp_GrantWhatTheirAdrsDecide()
    {
        Assert.AreEqual(LdapChallengeFlags, (uint)NtlmChallengeMessage.ChooseFlags((NtlmNegotiateFlags)WinLdapNegotiateFlags, true));
        Assert.AreEqual(HttpChallengeFlags, (uint)NtlmChallengeMessage.ChooseFlags((NtlmNegotiateFlags)WinLdapNegotiateFlags));
    }

    [TestMethod]
    public async Task MailNtlm_SameNegotiateMessage_GetsTheHttpChallenge()
    {
        var challenge = await Start(Policy(), "NTLM", canCarrySecurityLayer: false).BeginAsync(CancellationToken.None);

        Assert.AreEqual(
            HttpChallengeFlags,
            BitConverter.ToUInt32(challenge.Challenge.Span[20..24]));
    }

    [TestMethod]
    [DataRow("NTLM")]
    [DataRow("GSS-SPNEGO")]
    public async Task SealedLogin_ServerAndClientLayers_RoundTripBothDirections(string mechanism)
    {
        var flags = Unicode | Sign | Seal | ExtendedSessionSecurity | KeyExchange | 0x20000000;

        var step = await LoginAsync(Start(Policy(), mechanism), Authenticate("alice", "secret", flags));

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        var server = step.SecurityLayer!;
        var client = NtlmSecurityLayer.ForInitiator(new NtlmSessionKey(ExportedSessionKey, (NtlmNegotiateFlags)flags));
        Assert.IsTrue(server.TryUnprotect(client.Protect(BaseSearch), out var atServer));
        CollectionAssert.AreEqual(BaseSearch, atServer);
        Assert.IsTrue(client.TryUnprotect(server.Protect(BaseSearch), out var atClient));
        CollectionAssert.AreEqual(BaseSearch, atClient);
    }

    [TestMethod]
    public async Task SigningOnlyLogin_WithoutKeyExchange_SignsWithTheSessionBaseKey()
    {
        var flags = Unicode | Sign | ExtendedSessionSecurity;
        var responseKey = NtlmV2Calculation.ComputeResponseKeyNt(NtlmV2Calculation.ComputeNtHash("secret"), "alice", string.Empty);
        var proof = NtlmV2Calculation.ComputeNtProof(responseKey, FixedNtlmServerChallengeSource.FixtureChallenge, NtlmTestMessages.ClientBlob);
        var sessionBaseKey = NtlmV2Calculation.ComputeSessionBaseKey(responseKey, proof);

        var step = await LoginAsync(Start(Policy(), "NTLM"), Authenticate("alice", "secret", flags));

        var client = NtlmSecurityLayer.ForInitiator(new NtlmSessionKey(sessionBaseKey, (NtlmNegotiateFlags)flags));
        var toClient = step.SecurityLayer!.Protect(BaseSearch);
        CollectionAssert.AreEqual(BaseSearch, toClient[NtlmSecurityLayer.SignatureLength..]);
        Assert.IsTrue(client.TryUnprotect(toClient, out _));
    }

    [TestMethod]
    public async Task Login_AskingNoSigningOrSealing_IsAcceptedWithNoLayer()
    {
        var step = await LoginAsync(Start(Policy(), "NTLM"), Authenticate("alice", "secret", Unicode | ExtendedSessionSecurity | KeyExchange));

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("alice", step.AccountName);
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task Login_AskingSealingWithoutExtendedSessionSecurity_IsRefusedWithANote()
    {
        var step = await LoginAsync(Start(Policy(), "NTLM"), Authenticate("alice", "secret", Unicode | Sign | Seal | KeyExchange));

        AssertRefused(NtlmSaslExchange.NoExtendedSessionSecurityNote, step);
        Assert.AreEqual("Login refused: NTLM alice", step.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task Login_KeyExchangeWithoutA16ByteKey_IsRefused()
    {
        var responseKey = NtlmV2Calculation.ComputeResponseKeyNt(NtlmV2Calculation.ComputeNtHash("secret"), "alice", string.Empty);
        var proof = NtlmV2Calculation.ComputeNtProof(responseKey, FixedNtlmServerChallengeSource.FixtureChallenge, NtlmTestMessages.ClientBlob);
        var authenticate = NtlmTestMessages.Authenticate(
            "alice", string.Empty, [.. proof, .. NtlmTestMessages.ClientBlob], Unicode | Seal | ExtendedSessionSecurity | KeyExchange, new byte[15]);

        AssertRefused(null, await LoginAsync(Start(Policy(), "NTLM"), authenticate));
    }

    [TestMethod]
    public async Task Login_WrongPassword_IsRefusedWithNoNote()
    {
        AssertRefused(null, await LoginAsync(Start(Policy(), "NTLM"), Authenticate("alice", "wrong", Unicode | Seal | ExtendedSessionSecurity)));
    }

    [TestMethod]
    public async Task AllowAnonymous_UnknownUser_IsRefusedBecauseTheLayerNeedsThePassword()
    {
        var step = await LoginAsync(Start(Policy(allowAnonymous: true), "GSS-SPNEGO"), Authenticate("mallory", "secret", Unicode | Seal | ExtendedSessionSecurity));

        AssertRefused(NtlmSaslExchange.SecurityLayerNeedsPasswordNote, step);
        Assert.AreEqual("Login refused: GSS-SPNEGO mallory", step.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task AllowAnonymous_UnreadableMessage_IsRefusedBecauseTheLayerNeedsThePassword()
    {
        AssertRefused(NtlmSaslExchange.SecurityLayerNeedsPasswordNote, await LoginAsync(Start(Policy(allowAnonymous: true), "NTLM"), [1, 2, 3]));
    }

    [TestMethod]
    public async Task AllowAnonymous_KnownUserWithTheWrongPassword_IsRefusedWithNoNote()
    {
        AssertRefused(null, await LoginAsync(Start(Policy(allowAnonymous: true), "NTLM"), Authenticate("alice", "wrong", Unicode | Seal | ExtendedSessionSecurity)));
    }

    [TestMethod]
    public async Task AllowAnonymous_KnownUserWithThePassword_IsCheckedAndGetsTheLayer()
    {
        var step = await LoginAsync(Start(Policy(allowAnonymous: true), "NTLM"), Authenticate("alice", "secret", Unicode | Seal | ExtendedSessionSecurity));

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsNotNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task MailNtlm_AllowAnonymous_IsStillAcceptedUnchecked()
    {
        var step = await LoginAsync(Start(Policy(allowAnonymous: true), "NTLM", canCarrySecurityLayer: false), Authenticate("mallory", "x", Unicode | Seal | ExtendedSessionSecurity));

        Assert.AreEqual(SaslLoginOutcome.AcceptedUnchecked, step.Outcome);
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task MailNtlm_SealingAsked_IsAcceptedWithNoLayer()
    {
        var step = await LoginAsync(Start(Policy(), "NTLM", canCarrySecurityLayer: false), Authenticate("alice", "secret", Unicode | Seal | ExtendedSessionSecurity | KeyExchange));

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task GssSpnego_WhereNoSecurityLayerCanFollow_IsRefusedAsAMechanism()
    {
        var step = await Start(Policy(), "gss-spnego", canCarrySecurityLayer: false).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.RefusedMechanism, step.Outcome);
    }

    [TestMethod]
    public async Task GssSpnego_NegotiateNotAccepted_IsRefusedAsAMechanism()
    {
        var policy = Policy(acceptedMethods: new HashSet<AuthenticationMethod>(EveryMethod.Where(method => method != AuthenticationMethod.Negotiate)));

        var step = await Start(policy, "GSS-SPNEGO").BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.RefusedMechanism, step.Outcome);
    }

    [TestMethod]
    public void RecordedAuthenticateMessage_AsksForSealingWithAKeyExchangeKey()
    {
        // Guards the fixture's own premise: the recorded AUTHENTICATE_MESSAGE asks for sealing.
        var authenticate = NtlmAuthenticateMessage.TryRead(TranscriptHex("ldap-ntlm-sealed", "choice 0x8B ")[0])!;

        Assert.AreEqual(0xE2888235u, (uint)authenticate.NegotiateFlags);
        Assert.HasCount(16, authenticate.EncryptedRandomSessionKey);
        Assert.AreEqual("alice", authenticate.UserName);
        Assert.IsFalse(CryptographicOperations.FixedTimeEquals(authenticate.EncryptedRandomSessionKey, new byte[16]));
    }
}
