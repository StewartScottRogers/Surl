using System.Text;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>GSSAPI</c> (ADR-0057, decisions 9 and 10): an AP-REQ made by hand under a fixed service
/// key (decision 11) replayed through RFC 4752's steps, the refusals of the ticket, the AP-REP
/// answer and the security-layer choice, the account match, and the login note.
/// </summary>
[TestClass]
public sealed class GssapiSaslMechanismTests
{
    private const string Principal = "user@EXAMPLE.COM";
    private const string MailHost = "mail.example.com";
    private const ulong ClientSequenceNumber = 0x01020304;

    private static readonly byte[] NoBytes = [];

    private static readonly byte[] KerberosOid = [0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02];

    private static readonly HashSet<AuthenticationMethod> GssapiAndDigestMd5 =
        [AuthenticationMethod.Gssapi, AuthenticationMethod.DigestMd5, AuthenticationMethod.Plain];

    private readonly ManualTimeProvider clock = new(ApRequestBuilder.Now);
    private readonly SaslExchangeRunner runner;

    public GssapiSaslMechanismTests()
    {
        runner = new SaslExchangeRunner(clock);
    }

    private static ApRequestBuilder Ticket(string service = "smtp", bool mutualRequired = true) =>
        new() { ServerName = [service, MailHost], MutualRequired = mutualRequired };

    private static InitiatorTokens Client(ApRequestBuilder ticket) => new(ticket.SessionKeyType, ticket.SessionKey);

    private static byte[] Choice(byte layer = 0x01, string authzid = "") =>
        [layer, 0x00, 0x00, 0x00, .. Encoding.UTF8.GetBytes(authzid)];

    private static KerberosKeytab MailKeytab() => new(
        from service in new[] { "smtp", "imap", "pop" }
        select new KerberosKeytabEntry(
            new KerberosPrincipalName(ApRequestBuilder.Realm, [service, MailHost]),
            3,
            KerberosEncryptionType.Aes256CtsHmacSha196,
            ApRequestBuilder.ServiceKeyOf(KerberosEncryptionType.Aes256CtsHmacSha196)));

    private AuthenticationPolicy Policy(
        bool allowAnonymous = false,
        IReadOnlySet<AuthenticationMethod>? acceptedMethods = null,
        bool hasKeytab = true,
        string accountName = Principal) =>
        new(
            new AuthenticationSettings(
                new AccountBook([new Account(accountName, "unused")]), allowAnonymous, false, acceptedMethods ?? GssapiAndDigestMd5)
            {
                KerberosAcceptor = hasKeytab
                    ? new KerberosAcceptor(MailKeytab(), new KerberosReplayCache(clock), clock, new ZeroKerberosRandomSource())
                    : null,
            },
            [],
            clock);

    private static ISaslExchange Start(AuthenticationPolicy policy, string scheme, byte[]? initialResponse) =>
        policy.StartSaslExchange(new SaslExchangeStart(
            scheme, "GSSAPI", initialResponse is null ? null : (ReadOnlyMemory<byte>?)initialResponse, null));

    private static void AssertWrappedOffer(MailLoginStep step)
    {
        Assert.AreEqual(MailLoginOutcome.Challenge, step.Outcome);
        var token = step.Challenge.ToArray();
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x04, 0x01, 0xFF }, token[..4], "an RFC 4121 wrap token sent by the acceptor, unsealed");
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }, token[16..20], "no security layer, no maximum size");
    }

    [TestMethod]
    [DataRow("smtp", "smtp", DisplayName = "smtp")]
    [DataRow("SMTPS", "smtp", DisplayName = "smtps, any case")]
    [DataRow("imap", "imap", DisplayName = "imap")]
    [DataRow("imaps", "imap", DisplayName = "imaps")]
    [DataRow("pop3", "pop", DisplayName = "pop3")]
    [DataRow("pop3s", "pop", DisplayName = "pop3s")]
    public async Task Login_MutualAuthentication_IsAcceptedAsTheTicketsPrincipal(string scheme, string service)
    {
        var ticket = Ticket(service);

        var steps = await runner.RunAsync(
            Start(Policy(), scheme, ticket.Build()),
            NoBytes,
            Client(ticket).WrapSigned(Choice(), ClientSequenceNumber));

        Assert.HasCount(3, steps);
        Assert.AreEqual(MailLoginOutcome.Challenge, steps[0].Outcome);
        var apReply = steps[0].Challenge.ToArray();
        var afterOid = apReply.AsSpan().IndexOf(KerberosOid) + KerberosOid.Length;
        Assert.AreEqual(0x60, apReply[0]);
        CollectionAssert.AreEqual(new byte[] { 0x02, 0x00 }, apReply[afterOid..(afterOid + 2)], "the AP-REP's TOK_ID after the Kerberos OID");
        AssertWrappedOffer(steps[1]);
        Assert.AreEqual(
            new MailLoginStep(MailLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, Principal, new CheckedLogin("GSSAPI", Principal, true)),
            steps[2]);
    }

    [TestMethod]
    public async Task Login_WithoutInitialResponseOrMutualAuthentication_OffersTheLayerAtOnce()
    {
        var ticket = Ticket(mutualRequired: false);

        var steps = await runner.RunAsync(
            Start(Policy(), "smtp", null),
            ticket.Build(),
            Client(ticket).WrapSealed(Choice(authzid: Principal), ClientSequenceNumber, rightRotationCount: 5));

        Assert.HasCount(3, steps);
        Assert.AreEqual(new MailLoginStep(MailLoginOutcome.Challenge, ReadOnlyMemory<byte>.Empty, null, null), steps[0]);
        AssertWrappedOffer(steps[1]);
        Assert.AreEqual(MailLoginOutcome.Accepted, steps[2].Outcome);
        Assert.AreEqual("Login accepted: GSSAPI user@EXAMPLE.COM", steps[2].CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "checked")]
    [DataRow(true, DisplayName = "--allow-anonymous")]
    public async Task Ticket_UnderTheWrongKey_IsRefusedAfterTheDelayNamingNoUser(bool allowAnonymous)
    {
        var ticket = Ticket() with { TicketKey = new byte[32] };

        var pending = Start(Policy(allowAnonymous), "smtp", ticket.Build()).BeginAsync(CancellationToken.None);

        Assert.IsFalse(pending.IsCompleted, "a refused ticket waits the refusal delay");
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        Assert.AreEqual(
            new MailLoginStep(MailLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin("GSSAPI", null, false)),
            await pending);
    }

    [TestMethod]
    public async Task Ticket_ExpiredPastTheClockSkew_IsRefusedAfterTheDelay()
    {
        var ticket = Ticket() with { EndTime = ApRequestBuilder.Now };
        clock.Advance(KerberosAcceptor.ClockSkew + TimeSpan.FromSeconds(1));

        var pending = Start(Policy(), "smtp", ticket.Build()).BeginAsync(CancellationToken.None);

        Assert.IsFalse(pending.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        Assert.AreEqual(MailLoginOutcome.RefusedCredentials, (await pending).Outcome);
    }

    [TestMethod]
    public async Task Authenticator_Replayed_IsRefusedAfterTheDelay()
    {
        var policy = Policy();
        var token = Ticket().Build();
        var first = await runner.RunAsync(Start(policy, "smtp", token));

        var pending = Start(policy, "smtp", token).BeginAsync(CancellationToken.None);

        Assert.AreEqual(MailLoginOutcome.Challenge, first[0].Outcome);
        Assert.IsFalse(pending.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        Assert.AreEqual(MailLoginOutcome.RefusedCredentials, (await pending).Outcome);
    }

    [TestMethod]
    public async Task ApReply_AnsweredWithBytes_IsRefusedNamingThePrincipal()
    {
        var steps = await runner.RunAsync(Start(Policy(), "smtp", Ticket().Build()), new byte[] { 0x00 });

        Assert.AreEqual(
            new MailLoginStep(MailLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin("GSSAPI", Principal, false)),
            steps[^1]);
    }

    [TestMethod]
    [DataRow(0x02, DisplayName = "integrity layer")]
    [DataRow(0x03, DisplayName = "several layers")]
    [DataRow(0x00, DisplayName = "no bit")]
    public async Task Choice_OtherThanNoLayer_IsRefused(int layer)
    {
        var ticket = Ticket();

        var steps = await runner.RunAsync(
            Start(Policy(allowAnonymous: true), "smtp", ticket.Build()), NoBytes, Client(ticket).WrapSigned(Choice((byte)layer), ClientSequenceNumber));

        Assert.AreEqual(new CheckedLogin("GSSAPI", Principal, false), steps[^1].CheckedLogin);
        Assert.AreEqual(MailLoginOutcome.RefusedCredentials, steps[^1].Outcome);
    }

    [TestMethod]
    public async Task Choice_ShorterThanFourBytes_IsRefused()
    {
        var ticket = Ticket();

        var steps = await runner.RunAsync(
            Start(Policy(), "smtp", ticket.Build()), NoBytes, Client(ticket).WrapSigned([0x01, 0x00, 0x00], ClientSequenceNumber));

        Assert.AreEqual(MailLoginOutcome.RefusedCredentials, steps[^1].Outcome);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "not the client's next sequence number")]
    [DataRow(false, DisplayName = "not a wrap token")]
    public async Task Choice_NotTheClientsNextIntactWrapToken_IsRefused(bool isWrapToken)
    {
        var ticket = Ticket();
        var token = isWrapToken ? Client(ticket).WrapSigned(Choice(), ClientSequenceNumber + 1) : Choice();

        var steps = await runner.RunAsync(Start(Policy(allowAnonymous: true), "smtp", ticket.Build()), NoBytes, token);

        Assert.AreEqual(MailLoginOutcome.RefusedCredentials, steps[^1].Outcome);
    }

    [TestMethod]
    [DataRow("admin@EXAMPLE.COM", DisplayName = "another principal")]
    [DataRow("USER@EXAMPLE.COM", DisplayName = "the principal in another case")]
    public async Task AuthorizationIdentity_OtherThanEmptyOrThePrincipal_IsRefusedAfterTheDelay(string authzid)
    {
        var ticket = Ticket();
        var exchange = Start(Policy(), "smtp", ticket.Build());
        await runner.RunAsync(exchange, NoBytes);

        var pending = exchange.ContinueAsync(Client(ticket).WrapSigned(Choice(authzid: authzid), ClientSequenceNumber), CancellationToken.None);

        Assert.IsFalse(pending.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        Assert.AreEqual(
            new MailLoginStep(MailLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin("GSSAPI", Principal, false)),
            await pending);
    }

    [TestMethod]
    public async Task Principal_WithNoAccountOfItsName_IsRefused()
    {
        var ticket = Ticket();

        var steps = await runner.RunAsync(
            Start(Policy(accountName: "user"), "smtp", ticket.Build()), NoBytes, Client(ticket).WrapSigned(Choice(), ClientSequenceNumber));

        Assert.AreEqual(new CheckedLogin("GSSAPI", Principal, false), steps[^1].CheckedLogin);
        Assert.AreEqual(MailLoginOutcome.RefusedCredentials, steps[^1].Outcome);
    }

    [TestMethod]
    public async Task AllowAnonymous_RunsEveryStepAndSkipsOnlyTheAccountMatch()
    {
        var ticket = Ticket();

        var steps = await runner.RunAsync(
            Start(Policy(allowAnonymous: true, accountName: "nobody"), "smtp", ticket.Build()),
            NoBytes,
            Client(ticket).WrapSigned(Choice(authzid: "admin"), ClientSequenceNumber));

        Assert.HasCount(3, steps);
        AssertWrappedOffer(steps[1]);
        Assert.AreEqual(new MailLoginStep(MailLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null), steps[2]);
    }

    [TestMethod]
    public void Offer_GssapiAcceptedWithAKeytab_ListsGssapiFirstOnAnyConnection()
    {
        var offer = Policy().GetMailLoginOffer(null);

        CollectionAssert.AreEqual(new[] { "GSSAPI", "DIGEST-MD5" }, offer.SaslMechanisms.ToArray());
    }

    [TestMethod]
    [DataRow(false, true, DisplayName = "gssapi not accepted")]
    [DataRow(true, false, DisplayName = "gssapi accepted, no keytab")]
    public async Task Gssapi_NotAcceptedOrWithoutKeytab_IsNotOfferedAndIsRefusedUndelayed(bool isAccepted, bool hasKeytab)
    {
        var policy = Policy(
            allowAnonymous: true,
            acceptedMethods: isAccepted ? GssapiAndDigestMd5 : new HashSet<AuthenticationMethod> { AuthenticationMethod.DigestMd5 },
            hasKeytab: hasKeytab);

        var step = Start(policy, "smtp", Ticket().Build()).BeginAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "DIGEST-MD5" }, policy.GetMailLoginOffer(null).SaslMechanisms.ToArray());
        Assert.IsTrue(step.IsCompleted);
        Assert.AreEqual(new MailLoginStep(MailLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null), await step);
    }

    private sealed class ZeroKerberosRandomSource : IKerberosRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Clear();
    }
}
