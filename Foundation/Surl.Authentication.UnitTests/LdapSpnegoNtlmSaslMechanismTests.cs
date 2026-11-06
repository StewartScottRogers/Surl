using Surl.Cryptography.Rc4;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// LDAP's <c>NTLM</c> (Sicily) and <c>GSS-SPNEGO</c> binds carrying SPNEGO-wrapped NTLM (ADR-0072,
/// decision 4; BL-330). Pinned upstream curl 8.21.0's <c>WinLDAP</c> sent bare NTLM in every
/// configuration measured (the task's Notes), so, as for HTTP Negotiate, the NTLM messages it
/// recorded in <c>Fixtures/ldap-ntlm-sealed</c> and <c>Fixtures/ldap-negotiate-sealed</c> are wrapped
/// as RFC 4178 lays SPNEGO out, and the test plays the client's <c>mechListMIC</c> with
/// <see cref="NtlmSecurityLayer.ForInitiator"/> over the session key the recording carries.
/// </summary>
[TestClass]
public sealed class LdapSpnegoNtlmSaslMechanismTests
{
    // WinLDAP's NEGOTIATE_MESSAGE and AUTHENTICATE_MESSAGE flags in both recordings.
    private const uint WinLdapNegotiateFlags = 0xE20882B7;

    private const uint WinLdapAuthenticateFlags = 0xE2888235;

    private const uint Unicode = NtlmTestMessages.Unicode;

    private const uint Sign = 0x00000010;

    private const uint Seal = 0x00000020;

    private const uint ExtendedSessionSecurity = 0x00080000;

    private const uint KeyExchange = 0x40000000;

    // negTokenResp { negState accept-incomplete, supportedMech NTLMSSP }, before any responseToken.
    private const string NamingNtlmPrefix = "A0030A0101A10C060A2B06010401823702020A";

    // negTokenResp { negState accept-completed } and nothing else, as HTTP Negotiate sends it.
    private const string AcceptCompletedAlone = "A1073005A0030A0100";

    // negTokenResp { negState accept-completed, mechListMIC <16 bytes> } up to the signature.
    private const string AcceptCompletedWithMicPrefix = "A11B3019A0030A0100A3120410";

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private static readonly string[] NtlmOnly = [SpnegoTestTokens.NtlmOid];

    private static readonly string[] KerberosThenNtlm = [SpnegoTestTokens.MicrosoftKerberosOid, SpnegoTestTokens.NtlmOid];

    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy Policy(bool allowAnonymous = false) =>
        PolicyFixture.CreateWithFixedNonces(PolicyFixture.AliceAndToken, clock, allowAnonymous, EveryMethod);

    private static ISaslExchange Start(AuthenticationPolicy policy, string mechanism, byte[] initialResponse, bool canCarrySecurityLayer = true) =>
        policy.StartSaslExchange(new SaslExchangeStart("ldap", mechanism, initialResponse, null, canCarrySecurityLayer));

    private async Task<SaslLoginStep> ContinueAsync(ISaslExchange exchange, byte[] response)
    {
        var pending = exchange.ContinueAsync(response, CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await pending;
    }

    private static byte[] NegotiateMessage() => NtlmTestMessages.Negotiate(WinLdapNegotiateFlags);

    private static byte[] Challenge() =>
        NtlmChallengeMessage.Create((NtlmNegotiateFlags)WinLdapNegotiateFlags, FixedNtlmServerChallengeSource.FixtureChallenge, true);

    // The ExportedSessionKey a recorded AUTHENTICATE_MESSAGE from alice:secret sent under key exchange.
    private static NtlmSessionKey RecordedSessionKey(byte[] authenticate)
    {
        var message = NtlmAuthenticateMessage.TryRead(authenticate)!;
        var responseKey = NtlmV2Calculation.ComputeResponseKeyNt(NtlmV2Calculation.ComputeNtHash("secret"), "alice", string.Empty);
        var sessionBaseKey = NtlmV2Calculation.ComputeSessionBaseKey(responseKey, message.NtChallengeResponse[..16]);
        var exportedSessionKey = new byte[16];
        new Rc4(sessionBaseKey, 0).ApplyKeyStream(message.EncryptedRandomSessionKey, exportedSessionKey);

        return new NtlmSessionKey(exportedSessionKey, message.NegotiateFlags);
    }

    private static NtlmSecurityLayer Client(uint flags) =>
        NtlmSecurityLayer.ForInitiator(new NtlmSessionKey(LdapNtlmSaslMechanismTests.ExportedSessionKey, (NtlmNegotiateFlags)flags));

    private static void AssertRefused(string? refusalNote, SaslLoginStep step)
    {
        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.IsNull(step.AccountName);
        Assert.IsNull(step.SecurityLayer);
        Assert.IsTrue(step.AdditionalSuccessData.IsEmpty);
        Assert.AreEqual(refusalNote, step.RefusalNote);
    }

    [TestMethod]
    [DataRow("ldap-ntlm-sealed", "NTLM", "choice 0x8A ", "choice 0x8B ", 4)]
    [DataRow("ldap-negotiate-sealed", "GSS-SPNEGO", "sasl GSS-SPNEGO credentials ", "sasl GSS-SPNEGO credentials ", 5)]
    public async Task WinLdapsRecordedBind_WrappedInSpnego_IsAcceptedWithBothMechListMicsAndSealsFromSequenceNumberOne(
        string caseName, string mechanism, string negotiateMarker, string authenticateMarker, int searchMessageId)
    {
        var negotiate = LdapNtlmSaslMechanismTests.TranscriptHex(caseName, negotiateMarker)[0];
        var authenticate = LdapNtlmSaslMechanismTests.TranscriptHex(caseName, authenticateMarker).First(message => message[8] == 3);
        var recordedBuffer = LdapNtlmSaslMechanismTests.TranscriptHex(caseName, "SASL-wrapped buffer of 84 bytes: ")[0].AsSpan(4).ToArray();
        var mechTypes = SpnegoTestTokens.MechTypesDer(NtlmOnly);
        var client = NtlmSecurityLayer.ForInitiator(RecordedSessionKey(authenticate));
        var exchange = Start(Policy(), mechanism, SpnegoTestTokens.NegTokenInit(NtlmOnly, negotiate));

        var challenge = await exchange.BeginAsync(CancellationToken.None);
        var clientMechListMic = client.SignMechListMic(mechTypes);
        var step = await exchange.ContinueAsync(SpnegoTestTokens.NegTokenResp(authenticate, mechListMic: clientMechListMic), CancellationToken.None);

        // accept-incomplete naming NTLMSSP, with the challenge the recording answered.
        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);
        CollectionAssert.AreEqual(
            SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptIncomplete, SpnegoToken.NtlmOid, Challenge()),
            challenge.Challenge.ToArray());
        StringAssert.Contains(Convert.ToHexString(challenge.Challenge.Span), NamingNtlmPrefix);
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("alice", step.AccountName);
        Assert.AreEqual($"Login accepted: {mechanism} alice", step.CheckedLogin?.Note);

        // accept-completed with the server's mechListMIC, which the client's keys verify.
        var completed = step.AdditionalSuccessData.ToArray();
        StringAssert.StartsWith(Convert.ToHexString(completed), AcceptCompletedWithMicPrefix);
        Assert.IsTrue(client.VerifyMechListMic(mechTypes, completed[^16..]));

        // Both mechListMICs took sequence number 0 and left each RC4 handle as it was, so the first
        // search is sealed to WinLDAP's recorded bytes but signed with sequence number 1.
        byte[] search = [.. LdapNtlmSaslMechanismTests.BaseSearch];
        search[8] = (byte)searchMessageId;
        var sealedSearch = client.Protect(search);
        CollectionAssert.AreEqual(recordedBuffer[NtlmSecurityLayer.SignatureLength..], sealedSearch[NtlmSecurityLayer.SignatureLength..]);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0 }, sealedSearch[12..16]);
        Assert.IsTrue(step.SecurityLayer!.TryUnprotect(sealedSearch, out var atServer));
        CollectionAssert.AreEqual(search, atServer);
        Assert.IsTrue(client.TryUnprotect(step.SecurityLayer.Protect(search), out var atClient));
        CollectionAssert.AreEqual(search, atClient);
    }

    [TestMethod]
    public async Task RecordedBind_WrappedInSpnego_ItsBareFirstBufferNoLongerUnseals()
    {
        // The bare recording sealed its first search with sequence number 0, which SPNEGO's
        // mechListMIC has used.
        var authenticate = LdapNtlmSaslMechanismTests.TranscriptHex("ldap-ntlm-sealed", "choice 0x8B ")[0];
        var recordedBuffer = LdapNtlmSaslMechanismTests.TranscriptHex("ldap-ntlm-sealed", "SASL-wrapped buffer of 84 bytes: ")[0].AsSpan(4).ToArray();
        var negotiate = LdapNtlmSaslMechanismTests.TranscriptHex("ldap-ntlm-sealed", "choice 0x8A ")[0];
        var client = NtlmSecurityLayer.ForInitiator(RecordedSessionKey(authenticate));
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, negotiate));
        await exchange.BeginAsync(CancellationToken.None);

        var step = await exchange.ContinueAsync(
            SpnegoTestTokens.NegTokenResp(authenticate, mechListMic: client.SignMechListMic(SpnegoTestTokens.MechTypesDer(NtlmOnly))),
            CancellationToken.None);

        Assert.IsFalse(step.SecurityLayer!.TryUnprotect(recordedBuffer, out _));
    }

    [TestMethod]
    public async Task WrongMechListMic_IsRefusedWithANote()
    {
        var flags = WinLdapAuthenticateFlags;
        var exchange = Start(Policy(), "GSS-SPNEGO", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);
        var mechListMic = Client(flags).SignMechListMic(SpnegoTestTokens.MechTypesDer(NtlmOnly));
        mechListMic[^1] ^= 1;

        var step = await ContinueAsync(
            exchange, SpnegoTestTokens.NegTokenResp(LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", flags), mechListMic: mechListMic));

        AssertRefused(NtlmSaslExchange.WrongMechListMicNote, step);
        Assert.AreEqual("Login refused: GSS-SPNEGO alice", step.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task MechListMicOverAnotherMechList_IsRefused()
    {
        var flags = WinLdapAuthenticateFlags;
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);
        var mechListMic = Client(flags).SignMechListMic(SpnegoTestTokens.MechTypesDer(KerberosThenNtlm));

        var step = await ContinueAsync(
            exchange, SpnegoTestTokens.NegTokenResp(LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", flags), mechListMic: mechListMic));

        AssertRefused(NtlmSaslExchange.WrongMechListMicNote, step);
    }

    [TestMethod]
    public async Task NtlmFirstWithoutMechListMic_IsAcceptedWithAcceptCompletedAloneAndSealsFromSequenceNumberZero()
    {
        var flags = WinLdapAuthenticateFlags;
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);

        var step = await ContinueAsync(exchange, SpnegoTestTokens.NegTokenResp(LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", flags)));

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual(AcceptCompletedAlone, Convert.ToHexString(step.AdditionalSuccessData.Span));
        var sealedSearch = Client(flags).Protect(LdapNtlmSaslMechanismTests.BaseSearch);
        CollectionAssert.AreEqual(new byte[4], sealedSearch[12..16]);
        Assert.IsTrue(step.SecurityLayer!.TryUnprotect(sealedSearch, out _));
    }

    [TestMethod]
    public async Task NtlmNotFirst_IsNamedThenStartedInTheNextTokenAndNeedsAMechListMic()
    {
        var flags = WinLdapAuthenticateFlags;
        var authenticate = LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", flags);
        var exchange = Start(Policy(), "GSS-SPNEGO", SpnegoTestTokens.NegTokenInit(KerberosThenNtlm, [0x60, 0x01, 0x00]));

        var named = await exchange.BeginAsync(CancellationToken.None);
        var challenge = await exchange.ContinueAsync(SpnegoTestTokens.NegTokenResp(NegotiateMessage()), CancellationToken.None);
        var step = await ContinueAsync(exchange, SpnegoTestTokens.NegTokenResp(authenticate));

        Assert.AreEqual($"A1153013{NamingNtlmPrefix}", Convert.ToHexString(named.Challenge.Span));
        CollectionAssert.AreEqual(
            SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptIncomplete, null, Challenge()),
            challenge.Challenge.ToArray());
        AssertRefused(NtlmSaslExchange.MissingMechListMicNote, step);
    }

    [TestMethod]
    public async Task NtlmNotFirst_WithItsMechListMic_IsAccepted()
    {
        var flags = WinLdapAuthenticateFlags;
        var client = Client(flags);
        var exchange = Start(Policy(), "GSS-SPNEGO", SpnegoTestTokens.NegTokenInit(KerberosThenNtlm, null));
        await exchange.BeginAsync(CancellationToken.None);
        await exchange.ContinueAsync(SpnegoTestTokens.NegTokenResp(NegotiateMessage()), CancellationToken.None);

        var step = await exchange.ContinueAsync(
            SpnegoTestTokens.NegTokenResp(
                LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", flags),
                mechListMic: client.SignMechListMic(SpnegoTestTokens.MechTypesDer(KerberosThenNtlm))),
            CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsTrue(client.VerifyMechListMic(SpnegoTestTokens.MechTypesDer(KerberosThenNtlm), step.AdditionalSuccessData.Span[^16..]));
    }

    [TestMethod]
    public async Task NtlmFirstWithNoOptimisticToken_IsNamedThenStartedInTheNextToken()
    {
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, null));

        var named = await exchange.BeginAsync(CancellationToken.None);
        var challenge = await exchange.ContinueAsync(SpnegoTestTokens.NegTokenResp(NegotiateMessage()), CancellationToken.None);
        var step = await ContinueAsync(
            exchange, SpnegoTestTokens.NegTokenResp(LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", WinLdapAuthenticateFlags)));

        Assert.AreEqual($"A1153013{NamingNtlmPrefix}", Convert.ToHexString(named.Challenge.Span));
        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
    }

    [TestMethod]
    public async Task MechListMicWithNoLayerNegotiated_IsCheckedAndAnsweredWithNoLayer()
    {
        var flags = Unicode | ExtendedSessionSecurity | KeyExchange;
        var client = Client(flags);
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);

        var step = await exchange.ContinueAsync(
            SpnegoTestTokens.NegTokenResp(
                LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", flags),
                mechListMic: client.SignMechListMic(SpnegoTestTokens.MechTypesDer(NtlmOnly))),
            CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsNull(step.SecurityLayer);
        Assert.IsTrue(client.VerifyMechListMic(SpnegoTestTokens.MechTypesDer(NtlmOnly), step.AdditionalSuccessData.Span[^16..]));
    }

    [TestMethod]
    public async Task MechListMicWithoutExtendedSessionSecurity_IsRefusedWithANote()
    {
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);

        var step = await ContinueAsync(
            exchange,
            SpnegoTestTokens.NegTokenResp(LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", Unicode | KeyExchange), mechListMic: new byte[16]));

        AssertRefused(NtlmSaslExchange.NoExtendedSessionSecurityNote, step);
    }

    [TestMethod]
    public async Task WrongPassword_InSpnego_IsRefusedWithNoNote()
    {
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);

        var step = await ContinueAsync(
            exchange, SpnegoTestTokens.NegTokenResp(LdapNtlmSaslMechanismTests.Authenticate("alice", "wrong", WinLdapAuthenticateFlags)));

        AssertRefused(null, step);
        Assert.AreEqual("Login refused: NTLM alice", step.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task SecondNegotiateMessage_InSpnego_IsRefused()
    {
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);

        AssertRefused(null, await ContinueAsync(exchange, SpnegoTestTokens.NegTokenResp(NegotiateMessage())));
    }

    [TestMethod]
    [DataRow(false, null)]
    [DataRow(true, NtlmSaslExchange.SecurityLayerNeedsPasswordNote)]
    public async Task NegTokenInitWithoutNtlm_IsRefused(bool allowAnonymous, string? refusalNote)
    {
        var exchange = Start(Policy(allowAnonymous), "GSS-SPNEGO", SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.KerberosOid], [0x60, 0x01, 0x00]));

        var pending = exchange.BeginAsync(CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        AssertRefused(refusalNote, await pending);
    }

    [TestMethod]
    public async Task MalformedInitialContextToken_IsRefused()
    {
        var exchange = Start(Policy(), "NTLM", [0x60, 0x03, 0x06, 0x01, 0x00]);

        var pending = exchange.BeginAsync(CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        AssertRefused(null, await pending);
    }

    [TestMethod]
    public async Task BareMessageAfterSpnegoStarted_IsRefused()
    {
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()));
        await exchange.BeginAsync(CancellationToken.None);

        AssertRefused(null, await ContinueAsync(exchange, LdapNtlmSaslMechanismTests.Authenticate("alice", "secret", WinLdapAuthenticateFlags)));
    }

    [TestMethod]
    public async Task SpnegoTokenAfterABareChallenge_IsRefused()
    {
        var exchange = Start(Policy(), "NTLM", NegotiateMessage());
        await exchange.BeginAsync(CancellationToken.None);

        AssertRefused(null, await ContinueAsync(exchange, SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage())));
    }

    [TestMethod]
    public async Task MailNtlm_SpnegoToken_IsNotUnwrapped()
    {
        var exchange = Start(Policy(), "NTLM", SpnegoTestTokens.NegTokenInit(NtlmOnly, NegotiateMessage()), canCarrySecurityLayer: false);

        var pending = exchange.BeginAsync(CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        AssertRefused(null, await pending);
    }

    [TestMethod]
    public void MechListMic_IsTheSignatureOfTheMechListWithSequenceNumberZero()
    {
        // Signing only, so Protect's signature is taken with the same RC4 state.
        var flags = Unicode | Sign | ExtendedSessionSecurity | KeyExchange;
        var mechTypes = SpnegoTestTokens.MechTypesDer(NtlmOnly);

        var mechListMic = Client(flags).SignMechListMic(mechTypes);

        CollectionAssert.AreEqual(Client(flags).Protect(mechTypes)[..NtlmSecurityLayer.SignatureLength], mechListMic);
        CollectionAssert.AreEqual(new byte[4], mechListMic[12..16]);
    }
}
