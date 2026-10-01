using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// LDAP's <c>GSS-SPNEGO</c> bind selecting Kerberos (ADR-0072 Amendment 1, BL-327): pinned
/// upstream curl 8.21.0's <c>WinLDAP</c> bind recorded in <c>Fixtures/ldap-kerberos-sealed</c>
/// replayed against the keytab the recording's test KDC wrote, its AP-REQ accepted and its sealed
/// buffers unwrapped to the plain search and unbind; then AP-REQs made by hand for the service
/// <c>ldap</c> through the refusals, the <c>mechListMIC</c>, the layer choices and
/// <c>--allow-anonymous</c>.
/// </summary>
[TestClass]
public sealed class LdapKerberosSaslMechanismTests
{
    private const string RecordedCase = "ldap-kerberos-sealed";

    private const string RecordedPrincipal = "tester@SURL.TEST";

    private const string Principal = "user@EXAMPLE.COM";

    private const string NegoExOid = "1.3.6.1.4.1.311.2.2.30";

    private const ulong ClientSequenceNumber = 0x01020304;

    // negTokenResp { negState accept-completed, supportedMech 1.2.840.48018.1.2.2 (MS-KRB5), responseToken ...
    private const string AcceptCompletedMsKrb5Prefix = "A0030A0100A10B06092A864882F712010202A2";

    // An unbind, message ID 5, as WinLDAP's second sealed buffer holds it.
    private const string RecordedUnbind = "3084000000050201054200";

    private static readonly HashSet<AuthenticationMethod> NegotiateOnly = [AuthenticationMethod.Negotiate];

    private static readonly string[] WinLdapMechTypes =
        [SpnegoTestTokens.MicrosoftKerberosOid, SpnegoTestTokens.KerberosOid, NegoExOid, SpnegoTestTokens.NtlmOid];

    // The recording ran at 2026-09-30 23:24:43 to 23:24:51 at -07:00.
    private readonly ManualTimeProvider recordedClock = new(new DateTimeOffset(2026, 10, 1, 6, 24, 50, TimeSpan.Zero));

    private readonly ManualTimeProvider clock = new(ApRequestBuilder.Now);

    private static ApRequestBuilder Ticket(bool mutualRequired = true, uint flags = 0x3E) =>
        new() { ServerName = ["ldap", ApRequestBuilder.Host], MutualRequired = mutualRequired, Checksum = ApRequestBuilder.GssApiChecksumBytes(flags) };

    private static InitiatorTokens Client(ApRequestBuilder ticket) => new(ticket.SessionKeyType, ticket.SessionKey);

    private static AuthenticationPolicy Policy(
        ManualTimeProvider clock, KerberosKeytab? keytab, string accountName, bool allowAnonymous = false) =>
        new(
            new AuthenticationSettings(new AccountBook([new Account(accountName, "unused")]), allowAnonymous, false, NegotiateOnly)
            {
                KerberosAcceptor = keytab is null
                    ? null
                    : new KerberosAcceptor(keytab, new KerberosReplayCache(clock), clock, new ZeroKerberosRandomSource()),
            },
            [],
            clock);

    private AuthenticationPolicy Policy(bool allowAnonymous = false, bool hasKeytab = true) =>
        Policy(clock, hasKeytab ? ApRequestBuilder.Keytab("ldap") : null, Principal, allowAnonymous);

    private static ISaslExchange Start(AuthenticationPolicy policy, byte[]? initialResponse) =>
        policy.StartSaslExchange(new SaslExchangeStart(
            "ldap", "GSS-SPNEGO", initialResponse is null ? null : (ReadOnlyMemory<byte>?)initialResponse, null, true));

    private static byte[] Spnego(ApRequestBuilder ticket, string[]? mechTypes = null, byte[]? mechListMic = null) =>
        SpnegoTestTokens.NegTokenInit(mechTypes ?? WinLdapMechTypes, ticket.Build(), mechListMic: mechListMic);

    private async Task<SaslLoginStep> BeginAsync(ISaslExchange exchange)
    {
        var pending = exchange.BeginAsync(CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await pending;
    }

    private static void AssertRefused(string? user, string? refusalNote, SaslLoginStep step)
    {
        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.IsNull(step.AccountName);
        Assert.IsNull(step.SecurityLayer);
        Assert.IsTrue(step.AdditionalSuccessData.IsEmpty);
        Assert.AreEqual(user, step.CheckedLogin?.User);
        Assert.AreEqual(refusalNote, step.RefusalNote);
    }

    [TestMethod]
    public async Task WinLdapsRecordedKerberosBind_IsAcceptedAndItsSealedBuffersUnwrapToTheSearchAndTheUnbind()
    {
        var keytab = KerberosKeytab.Read(RecordedFixture.ReadBytes(RecordedCase, "service.keytab")).Keytab;
        var bind = LdapNtlmSaslMechanismTests.TranscriptHex(RecordedCase, "sasl GSS-SPNEGO credentials ")[0];
        var search = LdapNtlmSaslMechanismTests.TranscriptHex(RecordedCase, "SASL-wrapped buffer of 132 bytes: ")[0];
        var unbind = LdapNtlmSaslMechanismTests.TranscriptHex(RecordedCase, "SASL-wrapped buffer of 71 bytes: ")[0];
        var plainSearch = LdapNtlmSaslMechanismTests.TranscriptHex(RecordedCase, "unwrapped (sealed) to ")[0];

        var step = await Start(Policy(recordedClock, keytab, RecordedPrincipal), bind).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual(RecordedPrincipal, step.AccountName);
        Assert.AreEqual($"Login accepted: GSS-SPNEGO {RecordedPrincipal}", step.CheckedLogin?.Note);
        StringAssert.Contains(Convert.ToHexString(step.AdditionalSuccessData.Span), AcceptCompletedMsKrb5Prefix);
        var layer = step.SecurityLayer!;
        Assert.AreEqual(int.MaxValue, layer.MaximumProtectedBytes);
        Assert.IsTrue(layer.TryUnprotect(search.AsSpan(4), out var searchMessage));
        CollectionAssert.AreEqual(plainSearch, searchMessage);
        StringAssert.StartsWith(Convert.ToHexString(searchMessage), "3084000000420201046384");
        Assert.IsTrue(layer.TryUnprotect(unbind.AsSpan(4), out var unbindMessage));
        Assert.AreEqual(RecordedUnbind, Convert.ToHexString(unbindMessage));
        StringAssert.StartsWith(Convert.ToHexString(layer.Protect(unbindMessage)), "050403FF0000001C");
    }

    [TestMethod]
    public async Task HandMadeBind_ForAnAccount_IsAcceptedWithTheApRepAndASealingLayer()
    {
        var ticket = Ticket();

        var step = await Start(Policy(), Spnego(ticket)).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual(Principal, step.AccountName);
        StringAssert.Contains(Convert.ToHexString(step.AdditionalSuccessData.Span), AcceptCompletedMsKrb5Prefix);
        var layer = step.SecurityLayer!;
        Assert.IsTrue(layer.TryUnprotect(Client(ticket).WrapSealed(LdapNtlmSaslMechanismTests.BaseSearch, ClientSequenceNumber, 0, 28), out var message));
        CollectionAssert.AreEqual(LdapNtlmSaslMechanismTests.BaseSearch, message);
        Assert.AreEqual(0x03, layer.Protect(message)[2]);
    }

    [TestMethod]
    public async Task NoInitialResponse_IsAnEmptyChallengeThenTheKerberosBind()
    {
        var exchange = Start(Policy(), null);

        var challenge = await exchange.BeginAsync(CancellationToken.None);
        var step = await exchange.ContinueAsync(Spnego(Ticket()), CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);
        Assert.IsTrue(challenge.Challenge.IsEmpty);
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
    }

    [TestMethod]
    public async Task IntegrityAlone_IsASigningLayer()
    {
        var step = await Start(Policy(), Spnego(Ticket(flags: 0x22))).BeginAsync(CancellationToken.None);

        Assert.AreEqual(0x01, step.SecurityLayer!.Protect([0x30, 0x00])[2]);
    }

    [TestMethod]
    public async Task NeitherConfidentialityNorIntegrity_HasNoLayer()
    {
        var step = await Start(Policy(), Spnego(Ticket(flags: 0x02))).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task MutualAuthenticationNotAsked_SendsNoApRep()
    {
        var step = await Start(Policy(), Spnego(Ticket(mutualRequired: false))).BeginAsync(CancellationToken.None);

        Assert.AreEqual("A1143012A0030A0100A10B06092A864882F712010202", Convert.ToHexString(step.AdditionalSuccessData.Span));
    }

    [TestMethod]
    public async Task ClientMechListMic_IsCheckedAndAnsweredWithSurls()
    {
        var ticket = Ticket();
        var mechTypes = SpnegoTestTokens.MechTypesDer(WinLdapMechTypes);

        var step = await Start(Policy(), Spnego(ticket, mechListMic: Client(ticket).Mic(mechTypes, ClientSequenceNumber)))
            .BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        StringAssert.Contains(Convert.ToHexString(step.AdditionalSuccessData.Span), "A31E041C040401FF");
    }

    [TestMethod]
    public async Task WrongMechListMic_IsRefusedNamingThePrincipal()
    {
        var ticket = Ticket();

        var step = await BeginAsync(Start(Policy(), Spnego(ticket, mechListMic: Client(ticket).Mic([0x30, 0x00], ClientSequenceNumber))));

        AssertRefused(Principal, null, step);
    }

    [TestMethod]
    public async Task PrincipalWithNoAccount_IsRefusedNamingIt()
    {
        var policy = Policy(clock, ApRequestBuilder.Keytab("ldap"), "someone@EXAMPLE.COM");

        var step = await BeginAsync(Start(policy, Spnego(Ticket())));

        AssertRefused(Principal, null, step);
    }

    [TestMethod]
    public async Task AllowAnonymous_PrincipalWithNoAccount_IsAcceptedUncheckedWithItsLayer()
    {
        var policy = Policy(clock, ApRequestBuilder.Keytab("ldap"), "someone@EXAMPLE.COM", allowAnonymous: true);

        var step = await Start(policy, Spnego(Ticket())).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.AcceptedUnchecked, step.Outcome);
        Assert.IsNotNull(step.SecurityLayer);
        StringAssert.Contains(Convert.ToHexString(step.AdditionalSuccessData.Span), AcceptCompletedMsKrb5Prefix);
    }

    [TestMethod]
    public async Task TicketForAnotherService_IsRefusedWithTheAcceptorsReason()
    {
        var ticket = new ApRequestBuilder();

        var step = await BeginAsync(Start(Policy(), Spnego(ticket)));

        AssertRefused(null, "Kerberos: no key for HTTP/web01.example.com@EXAMPLE.COM aes256-cts-hmac-sha1-96 kvno 3", step);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "NEGOEX listed before Kerberos")]
    [DataRow(false, DisplayName = "Kerberos first with no optimistic token")]
    public async Task KerberosSelectedWithoutItsOptimisticApReq_IsRefused(bool isNegoExFirst)
    {
        string[] mechTypes = isNegoExFirst ? [NegoExOid, .. WinLdapMechTypes] : WinLdapMechTypes;
        var token = SpnegoTestTokens.NegTokenInit(mechTypes, isNegoExFirst ? Ticket().Build() : null);

        var step = await BeginAsync(Start(Policy(), token));

        AssertRefused(null, "Kerberos: no optimistic AP-REQ", step);
    }

    [TestMethod]
    public async Task NoKeytab_KerberosFirst_IsAnsweredByNtlmsRule()
    {
        var step = await Start(Policy(hasKeytab: false), Spnego(Ticket())).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Challenge, step.Outcome);
        CollectionAssert.AreEqual(
            SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptIncomplete, SpnegoToken.NtlmOid, null),
            step.Challenge.ToArray());
    }

    [TestMethod]
    public async Task WithKeytab_BareNtlm_IsAnsweredByNtlm()
    {
        var step = await Start(Policy(), NtlmTestMessages.Negotiate(0xE20882B7)).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Challenge, step.Outcome);
        Assert.AreEqual("NTLMSSP\0", System.Text.Encoding.ASCII.GetString(step.Challenge.Span[..8]));
    }

    [TestMethod]
    public async Task WithKeytab_SpnegoNamingNtlmFirst_IsAnsweredByNtlm()
    {
        var token = SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid, SpnegoTestTokens.MicrosoftKerberosOid], NtlmTestMessages.Negotiate(0xE20882B7));

        var step = await Start(Policy(), token).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Challenge, step.Outcome);
        StringAssert.Contains(Convert.ToHexString(step.Challenge.Span), "A10C060A2B06010401823702020A");
    }

    [TestMethod]
    public async Task WithKeytab_MalformedSpnego_IsRefusedAsNtlmRefusesIt()
    {
        var step = await BeginAsync(Start(Policy(), [0x60, 0x03, 0x06, 0x01, 0x00]));

        AssertRefused(null, null, step);
    }

    private sealed class ZeroKerberosRandomSource : IKerberosRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Clear();
    }
}
