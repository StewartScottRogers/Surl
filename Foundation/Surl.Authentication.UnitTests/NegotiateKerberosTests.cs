using System.Formats.Asn1;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Kerberos inside Negotiate (ADR-0057 decisions 8 and 10): AP-REQs made by hand under a fixed
/// service key (decision 11), bare and wrapped in SPNEGO as MS-SPNG lays them out, answered in one
/// leg with the final token; the <c>mechListMIC</c> both ways; the refusals, after the delay and
/// with every challenge again; the login note; <c>--allow-anonymous</c>; and Negotiate unchanged
/// without a Kerberos acceptor.
/// </summary>
[TestClass]
public sealed class NegotiateKerberosTests
{
    private const string Principal = "user@EXAMPLE.COM";

    private static readonly byte[] KerberosOidDer = [0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02];

    private static readonly HashSet<AuthenticationMethod> NegotiateAndNtlm =
        [AuthenticationMethod.Negotiate, AuthenticationMethod.Ntlm];

    private readonly ManualTimeProvider clock = new(ApRequestBuilder.Now);

    private static ApRequestBuilder Ticket(bool mutualRequired = false) => new() { MutualRequired = mutualRequired };

    private static string[] KerberosThenNtlm(string kerberosOid) => [kerberosOid, SpnegoTestTokens.NtlmOid];

    private static byte[] Spnego(ApRequestBuilder ticket, string kerberosOid = SpnegoTestTokens.MicrosoftKerberosOid) =>
        SpnegoTestTokens.NegTokenInit(KerberosThenNtlm(kerberosOid), ticket.Build());

    private static HttpAuthenticationRequest Get(byte[] token) => PolicyFixture.Get(SpnegoTestTokens.Authorization(token));

    private AuthenticationSettings Settings(bool allowAnonymous = false, bool hasKeytab = true, string accountName = Principal) =>
        new(new AccountBook([new Account(accountName, "unused")]), allowAnonymous, false, NegotiateAndNtlm)
        {
            KerberosAcceptor = hasKeytab
                ? new KerberosAcceptor(ApRequestBuilder.Keytab(), new KerberosReplayCache(clock), clock, new ZeroKerberosRandomSource())
                : null,
        };

    private AuthenticationPolicy Policy(AuthenticationSettings settings) =>
        new(settings, [NegotiateOf(settings), new NtlmAuthenticationMethod(settings.Accounts)], clock);

    private IHttpAuthenticationSession Session(bool allowAnonymous = false, bool hasKeytab = true, string accountName = Principal) =>
        Policy(Settings(allowAnonymous, hasKeytab, accountName)).StartHttpConnection(null);

    private async Task<HttpCredentialCheck> VerifyAsync(byte[] token, AuthenticationSettings? settings = null) =>
        await NegotiateOf(settings ?? Settings()).StartConnection()
            .VerifyAsync(Convert.ToBase64String(token), PolicyFixture.Get(), CancellationToken.None);

    private async Task<HttpAuthenticationVerdict> JudgeRefusedAsync(IHttpAuthenticationSession session, byte[] token)
    {
        var judging = session.JudgeAsync(Get(token), CancellationToken.None).AsTask();
        Assert.IsFalse(judging.IsCompleted, "a refusal waits for the delay");
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await judging;
    }

    private static void AssertEveryChallenge(HttpAuthenticationVerdict verdict)
    {
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        Assert.IsNull(verdict.AccountName);
        CollectionAssert.AreEqual(new[] { "Negotiate", "NTLM" }, verdict.WwwAuthenticateValues.ToArray());
    }

    private static NegTokenResp ReadFinalToken(IReadOnlyList<string> values)
    {
        Assert.HasCount(1, values);
        StringAssert.StartsWith(values[0], "Negotiate ");

        return NegTokenResp.Read(Convert.FromBase64String(values[0]["Negotiate ".Length..]));
    }

    private static void AssertApRepToken(byte[] token)
    {
        Assert.AreEqual(0x60, token[0]);
        var afterOid = token.AsSpan().IndexOf(KerberosOidDer) + KerberosOidDer.Length;
        CollectionAssert.AreEqual(new byte[] { 0x02, 0x00 }, token[afterOid..(afterOid + 2)], "the AP-REP's TOK_ID after the Kerberos OID");
    }

    [TestMethod]
    [DataRow(SpnegoTestTokens.MicrosoftKerberosOid, DisplayName = "Microsoft's Kerberos OID")]
    [DataRow(SpnegoTestTokens.KerberosOid, DisplayName = "the RFC 1964 Kerberos OID")]
    public async Task Spnego_KerberosFirstWithAValidApReq_IsAcceptedEchoingTheOid(string kerberosOid)
    {
        var check = await VerifyAsync(Spnego(Ticket(), kerberosOid));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual(Principal, check.AccountName);
        Assert.AreEqual(Principal, check.UserAsSent);
        var reply = ReadFinalToken(check.WwwAuthenticateValues);
        Assert.AreEqual(SpnegoNegState.AcceptCompleted, reply.NegState);
        Assert.AreEqual(kerberosOid, reply.SupportedMech);
        Assert.IsNull(reply.ResponseToken, "no AP-REP without mutual-required");
        Assert.IsNull(reply.MechListMic, "no client MIC, no reply MIC");
    }

    [TestMethod]
    public async Task Spnego_MutualRequired_CarriesTheApRepTokenAsResponseToken()
    {
        var check = await VerifyAsync(Spnego(Ticket(mutualRequired: true)));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        var reply = ReadFinalToken(check.WwwAuthenticateValues);
        Assert.AreEqual(SpnegoTestTokens.MicrosoftKerberosOid, reply.SupportedMech);
        AssertApRepToken(reply.ResponseToken!);
    }

    [TestMethod]
    public async Task Bare_MutualRequired_IsAnsweredWithTheBareApRepToken()
    {
        var check = await VerifyAsync(Ticket(mutualRequired: true).Build());

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual(Principal, check.AccountName);
        Assert.HasCount(1, check.WwwAuthenticateValues);
        AssertApRepToken(Convert.FromBase64String(check.WwwAuthenticateValues[0]["Negotiate ".Length..]));
    }

    [TestMethod]
    public async Task Bare_WithoutMutualAuthentication_IsAcceptedWithNoFinalToken()
    {
        var check = await VerifyAsync(Ticket().Build());

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual(Principal, check.AccountName);
        Assert.IsEmpty(check.WwwAuthenticateValues);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "surl's sequence number is the client's")]
    [DataRow(true, DisplayName = "surl's sequence number is the AP-REP's")]
    public async Task MechListMic_ValidClientMic_IsAcceptedAndAnsweredWithAMicThatVerifiesUnderKeyUsage23(bool mutualRequired)
    {
        var ticket = Ticket(mutualRequired);
        string[] mechTypes = KerberosThenNtlm(SpnegoTestTokens.MicrosoftKerberosOid);
        var mechTypesDer = SpnegoTestTokens.MechTypesDer(mechTypes);
        var clientMic = new InitiatorTokens(ticket.SessionKeyType, ticket.SessionKey).Mic(mechTypesDer, (ulong)ticket.SequenceNumber!);

        var check = await VerifyAsync(SpnegoTestTokens.NegTokenInit(mechTypes, ticket.Build(), mechListMic: clientMic));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        var mic = ReadFinalToken(check.WwwAuthenticateValues).MechListMic!;
        var header = mic[..16];
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x04, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, header[..8], "a MIC token sent by the acceptor");
        var sequenceNumber = mutualRequired ? 0UL : (ulong)ticket.SequenceNumber!;
        Assert.AreEqual(sequenceNumber, System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8)));
        var expected = KerberosEncryptionProfile.For(ticket.SessionKeyType).ComputeChecksum(ticket.SessionKey, 23, [.. mechTypesDer, .. header]);
        CollectionAssert.AreEqual(expected, mic[16..]);
    }

    [TestMethod]
    public async Task MechListMic_Tampered_IsRefusedNamingThePrincipalAfterTheDelay()
    {
        var ticket = Ticket();
        string[] mechTypes = KerberosThenNtlm(SpnegoTestTokens.MicrosoftKerberosOid);
        var clientMic = new InitiatorTokens(ticket.SessionKeyType, ticket.SessionKey)
            .Mic(SpnegoTestTokens.MechTypesDer(mechTypes), (ulong)ticket.SequenceNumber!);
        clientMic[^1] ^= 0x01;

        var verdict = await JudgeRefusedAsync(Session(), SpnegoTestTokens.NegTokenInit(mechTypes, ticket.Build(), mechListMic: clientMic));

        AssertEveryChallenge(verdict);
        Assert.AreEqual(new CheckedLogin("Negotiate", Principal, false), verdict.CheckedLogin);
    }

    public static IEnumerable<object?[]> RefusedTickets() =>
    [
        ["a Kerberos-first NegTokenInit without an optimistic token",
            SpnegoTestTokens.NegTokenInit(KerberosThenNtlm(SpnegoTestTokens.MicrosoftKerberosOid), null), null],
        ["Kerberos selected but not first",
            SpnegoTestTokens.NegTokenInit(["1.3.6.1.4.1.311.2.2.30", SpnegoTestTokens.KerberosOid, SpnegoTestTokens.NtlmOid], Ticket().Build()), null],
        ["a ticket under the wrong key", Spnego(Ticket() with { TicketKey = new byte[32] }), null],
        ["an expired ticket",
            Spnego(Ticket() with { AuthTime = ApRequestBuilder.Now.AddHours(-12), StartTime = ApRequestBuilder.Now.AddHours(-12), EndTime = ApRequestBuilder.Now.AddHours(-2) }),
            null],
        ["a malformed bare token", new byte[] { 0x60, 0x03, 0x06, 0x01, 0x80 }, null],
        ["a NegTokenInit naming no supported mechanism", SpnegoTestTokens.NegTokenInit(["1.3.6.1.4.1.311.2.2.30"], [1]), null],
    ];

    [TestMethod]
    [DynamicData(nameof(RefusedTickets))]
    public async Task Refusal_IsAnsweredWithEveryChallengeAfterTheDelayWithNoNtlmFallback(string reason, byte[] token, string? user)
    {
        var verdict = await JudgeRefusedAsync(Session(), token);

        AssertEveryChallenge(verdict);
        Assert.AreEqual(new CheckedLogin("Negotiate", user, false), verdict.CheckedLogin, reason);
    }

    [TestMethod]
    public async Task Refusal_ValidTicketForAPrincipalWithNoAccount_NamesThePrincipal()
    {
        var verdict = await JudgeRefusedAsync(Session(accountName: "user"), Spnego(Ticket()));

        AssertEveryChallenge(verdict);
        Assert.AreEqual(new CheckedLogin("Negotiate", Principal, false), verdict.CheckedLogin);
        Assert.AreEqual("Login refused: Negotiate user@EXAMPLE.COM", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public async Task Refusal_ReplayedAuthenticator_IsRefusedOnTheNextConnection()
    {
        var policy = Policy(Settings());
        var token = Spnego(Ticket());
        var accepted = await policy.StartHttpConnection(null).JudgeAsync(Get(token), CancellationToken.None);

        var replayed = await JudgeRefusedAsync(policy.StartHttpConnection(null), token);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, accepted.Outcome);
        AssertEveryChallenge(replayed);
        Assert.AreEqual(new CheckedLogin("Negotiate", null, false), replayed.CheckedLogin);
    }

    [TestMethod]
    public async Task Accepted_BehindThePolicy_ServesTheAccountWithTheFinalTokenAndRemembersTheConnection()
    {
        var session = Session();

        var accepted = await session.JudgeAsync(Get(Spnego(Ticket(mutualRequired: true))), CancellationToken.None);
        var later = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, accepted.Outcome);
        Assert.AreEqual(Principal, accepted.AccountName);
        Assert.AreEqual(new CheckedLogin("Negotiate", Principal, true), accepted.CheckedLogin);
        Assert.AreEqual("Login accepted: Negotiate user@EXAMPLE.COM", accepted.CheckedLogin!.Note);
        Assert.AreEqual(SpnegoNegState.AcceptCompleted, ReadFinalToken(accepted.WwwAuthenticateValues).NegState);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, later.Outcome);
        Assert.AreEqual(Principal, later.AccountName);
        Assert.IsNull(later.CheckedLogin);
    }

    [TestMethod]
    public async Task Spnego_NtlmBeforeKerberos_RunsNtlmAsAdr0040Decides()
    {
        var init = SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid, SpnegoTestTokens.KerberosOid], null);

        var check = await VerifyAsync(init);

        Assert.AreEqual(HttpCredentialOutcome.Continue, check.Outcome);
        Assert.AreEqual(SpnegoTestTokens.NtlmOid, ReadFinalToken(check.WwwAuthenticateValues).SupportedMech);
    }

    [TestMethod]
    public async Task AllowAnonymous_ValidTicketWithNoAccount_IsServedUncheckedWithTheFinalToken()
    {
        var verdict = await Session(allowAnonymous: true, accountName: "nobody")
            .JudgeAsync(Get(Spnego(Ticket(mutualRequired: true))), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsNull(verdict.AccountName);
        Assert.IsNull(verdict.CheckedLogin);
        AssertApRepToken(ReadFinalToken(verdict.WwwAuthenticateValues).ResponseToken!);
    }

    [TestMethod]
    public async Task AllowAnonymous_BareValidTicket_IsServedUnchecked()
    {
        var check = await VerifyAsync(Ticket().Build(), Settings(allowAnonymous: true, accountName: "nobody"));

        Assert.AreEqual(new HttpCredentialCheck(HttpCredentialOutcome.AcceptedUnchecked, null, check.WwwAuthenticateValues, Principal), check);
        Assert.IsEmpty(check.WwwAuthenticateValues);
    }

    [TestMethod]
    public async Task AllowAnonymous_WrongKeyTicket_IsStillRefusedAfterTheDelay()
    {
        var verdict = await JudgeRefusedAsync(Session(allowAnonymous: true), Spnego(Ticket() with { TicketKey = new byte[32] }));

        AssertEveryChallenge(verdict);
        Assert.AreEqual(new CheckedLogin("Negotiate", null, false), verdict.CheckedLogin);
    }

    public static IEnumerable<object[]> TokensServedUnchecked() =>
    [
        ["bare NTLM", NtlmTestMessages.Negotiate(0xA2088207)],
        ["a negTokenResp", SpnegoTestTokens.NegTokenResp([1, 2, 3])],
        ["a NegTokenInit selecting NTLM", SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], null)],
        ["a NegTokenInit naming no supported mechanism", SpnegoTestTokens.NegTokenInit(["1.3.6.1.4.1.311.2.2.30"], [1])],
    ];

    [TestMethod]
    [DynamicData(nameof(TokensServedUnchecked))]
    public async Task AllowAnonymous_EveryTokenButKerberos_IsServedUncheckedWithNoNote(string description, byte[] token)
    {
        var verdict = await Session(allowAnonymous: true).JudgeAsync(Get(token), CancellationToken.None);

        Assert.AreEqual(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null), verdict with { WwwAuthenticateValues = [] }, description);
        Assert.IsEmpty(verdict.WwwAuthenticateValues, description);
    }

    [TestMethod]
    public async Task AllowAnonymousWithoutKeytab_KerberosToken_IsServedWithoutBeingRead()
    {
        var verdict = await Session(allowAnonymous: true, hasKeytab: false)
            .JudgeAsync(Get(Spnego(Ticket() with { TicketKey = new byte[32] })), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsEmpty(verdict.WwwAuthenticateValues);
        Assert.IsNull(verdict.CheckedLogin);
    }

    [TestMethod]
    public async Task WithoutKeytab_BareKerberosToken_IsRefusedAsAdr0040Decides()
    {
        var check = await VerifyAsync(Ticket().Build(), Settings(hasKeytab: false));

        Assert.AreEqual(new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, check.WwwAuthenticateValues), check);
        Assert.IsEmpty(check.WwwAuthenticateValues);
    }

    [TestMethod]
    public async Task WithoutKeytab_KerberosFirstNegTokenInit_ChoosesNtlmAsAdr0040Decides()
    {
        var check = await VerifyAsync(Spnego(Ticket()), Settings(hasKeytab: false));

        Assert.AreEqual(HttpCredentialOutcome.Continue, check.Outcome);
        var reply = ReadFinalToken(check.WwwAuthenticateValues);
        Assert.AreEqual(SpnegoNegState.AcceptIncomplete, reply.NegState);
        Assert.AreEqual(SpnegoTestTokens.NtlmOid, reply.SupportedMech);
    }

    [TestMethod]
    public void Constructor_NullAccounts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new NegotiateAuthenticationMethod(null!, null, false));
    }

    private static NegotiateAuthenticationMethod NegotiateOf(AuthenticationSettings settings) =>
        new(settings.Accounts, settings.KerberosAcceptor, settings.AllowAnonymous);

    // A server's negTokenResp, read back field by field (RFC 4178 section 4.2.2).
    private sealed record NegTokenResp(SpnegoNegState NegState, string? SupportedMech, byte[]? ResponseToken, byte[]? MechListMic)
    {
        public static NegTokenResp Read(byte[] token)
        {
            var fields = new AsnReader(token, AsnEncodingRules.DER).ReadSequence(Context(1)).ReadSequence();
            var negState = fields.ReadSequence(Context(0)).ReadEnumeratedValue<SpnegoNegState>();
            var supportedMech = HasField(fields, 1) ? fields.ReadSequence(Context(1)).ReadObjectIdentifier() : null;
            var responseToken = HasField(fields, 2) ? fields.ReadSequence(Context(2)).ReadOctetString() : null;
            var mechListMic = HasField(fields, 3) ? fields.ReadSequence(Context(3)).ReadOctetString() : null;
            fields.ThrowIfNotEmpty();

            return new NegTokenResp(negState, supportedMech, responseToken, mechListMic);
        }

        private static bool HasField(AsnReader fields, int number) => fields.HasData && fields.PeekTag() == Context(number);

        private static Asn1Tag Context(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);
    }

    private sealed class ZeroKerberosRandomSource : IKerberosRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Clear();
    }
}
