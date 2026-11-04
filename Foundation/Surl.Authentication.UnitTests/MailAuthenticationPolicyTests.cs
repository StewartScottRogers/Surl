using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/> as the <see cref="IMailAuthenticationPolicy"/>: the offer
/// (ADR-0049, section 2), which mechanism an exchange runs, <c>APOP</c> when not accepted, and the
/// order <see cref="ISaslExchange"/>'s calls must come in (section 6).
/// </summary>
[TestClass]
public sealed class MailAuthenticationPolicyTests
{
    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy Policy(
        AccountBook? accounts = null,
        bool allowAnonymous = false,
        bool allowPlaintextAuth = false,
        IReadOnlySet<AuthenticationMethod>? acceptedMethods = null) =>
        PolicyFixture.Create(accounts ?? SaslExchangeRunner.UserAndToken, clock, allowAnonymous, allowPlaintextAuth, acceptedMethods);

    [TestMethod]
    public void Offer_DefaultSetOverTls_IsTheFiveMechanismsInAdrOrderAndTheClearPassword()
    {
        var offer = Policy().GetMailLoginOffer(PolicyFixture.Tls);

        CollectionAssert.AreEqual(new[] { "CRAM-MD5", "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN" }, offer.SaslMechanisms.ToArray());
        Assert.IsTrue(offer.IsClearPasswordLoginOffered);
        Assert.IsFalse(offer.IsApopOffered);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "checked")]
    [DataRow(true, DisplayName = "--allow-anonymous")]
    public void Offer_WithoutTls_OffersNoPlaintextSecret(bool allowAnonymous)
    {
        var offer = Policy(allowAnonymous: allowAnonymous).GetMailLoginOffer(null);

        CollectionAssert.AreEqual(new[] { "CRAM-MD5" }, offer.SaslMechanisms.ToArray());
        Assert.IsFalse(offer.IsClearPasswordLoginOffered);
        Assert.IsFalse(offer.IsApopOffered);
    }

    [TestMethod]
    public void Offer_AllowPlaintextAuthWithoutTls_OffersThePlaintextMechanisms()
    {
        var offer = Policy(allowPlaintextAuth: true).GetMailLoginOffer(null);

        CollectionAssert.AreEqual(new[] { "CRAM-MD5", "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN" }, offer.SaslMechanisms.ToArray());
        Assert.IsTrue(offer.IsClearPasswordLoginOffered);
    }

    [TestMethod]
    public void Offer_EveryMechanismAccepted_ListsDigestMd5FirstAndApopWithoutTls()
    {
        var offer = Policy(acceptedMethods: new HashSet<AuthenticationMethod>(Enum.GetValues<AuthenticationMethod>()))
            .GetMailLoginOffer(null);

        CollectionAssert.AreEqual(new[] { "DIGEST-MD5", "CRAM-MD5", "NTLM" }, offer.SaslMechanisms.ToArray());
        Assert.IsTrue(offer.IsApopOffered);
    }

    [TestMethod]
    public void Offer_OnlyTheAcceptedMechanisms_AndNotDependingOnAccounts()
    {
        var accepted = new HashSet<AuthenticationMethod> { AuthenticationMethod.Login, AuthenticationMethod.Basic, AuthenticationMethod.XOAuth2 };

        var offer = Policy(PolicyFixture.NoAccounts, acceptedMethods: accepted).GetMailLoginOffer(PolicyFixture.Tls);

        CollectionAssert.AreEqual(new[] { "XOAUTH2", "LOGIN" }, offer.SaslMechanisms.ToArray());
    }

    [TestMethod]
    [DataRow("plain", DisplayName = "lower case")]
    [DataRow("Plain", DisplayName = "mixed case")]
    public async Task Mechanism_IsMatchedCaseInsensitively(string mechanism)
    {
        var step = await SaslExchangeRunner.Start(
            Policy(), mechanism, SaslExchangeRunner.Plain(string.Empty, "user", "secret"), PolicyFixture.Tls).BeginAsync(CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("Login accepted: PLAIN user", step.CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow("DIGEST-MD5", false, DisplayName = "not in the default set")]
    [DataRow("NTLM", false, DisplayName = "NTLM, not in the default set")]
    [DataRow("SCRAM-SHA-256", false, DisplayName = "unknown")]
    [DataRow("", false, DisplayName = "empty")]
    [DataRow("PLAIN", true, DisplayName = "not accepted by --auth")]
    public async Task Mechanism_UnknownOrNotAccepted_IsRefusedUndelayedWithNoNote(string mechanism, bool onlyLoginAccepted)
    {
        var policy = Policy(
            allowAnonymous: true, acceptedMethods: onlyLoginAccepted ? new HashSet<AuthenticationMethod> { AuthenticationMethod.Login } : null);
        var exchange = SaslExchangeRunner.Start(policy, mechanism, [], PolicyFixture.Tls);

        var step = exchange.BeginAsync(CancellationToken.None);

        Assert.IsTrue(step.IsCompleted);
        Assert.AreEqual(new SaslLoginStep(SaslLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null), await step);
    }

    [TestMethod]
    [DataRow("smtp", true, false, DisplayName = "smtp over TLS")]
    [DataRow("smtp", false, false, DisplayName = "smtp without TLS")]
    [DataRow("imap", false, true, DisplayName = "imap, every method accepted")]
    [DataRow("pop3", true, true, DisplayName = "pop3 over TLS, every method accepted")]
    public void GetSaslMechanisms_MailScheme_IsTheMailLoginOffersMechanisms(string scheme, bool isEncrypted, bool acceptsEveryMethod)
    {
        var policy = Policy(acceptedMethods: acceptsEveryMethod ? new HashSet<AuthenticationMethod>(Enum.GetValues<AuthenticationMethod>()) : null);
        var tlsSession = isEncrypted ? PolicyFixture.Tls : null;

        var mechanisms = policy.GetSaslMechanisms(new SaslOfferRequest(scheme, tlsSession));

        CollectionAssert.AreEqual(policy.GetMailLoginOffer(tlsSession).SaslMechanisms.ToArray(), mechanisms.ToArray());
    }

    [TestMethod]
    [DataRow("ldap", DisplayName = "ldap")]
    [DataRow("LDAPS", DisplayName = "ldaps, upper case")]
    public void GetSaslMechanisms_LdapWithNegotiateAcceptedAndNoGssapi_ListsGssSpnegoFirst(string scheme)
    {
        var policy = Policy(acceptedMethods: new HashSet<AuthenticationMethod>(Enum.GetValues<AuthenticationMethod>()));

        var mechanisms = policy.GetSaslMechanisms(new SaslOfferRequest(scheme, null));

        CollectionAssert.AreEqual(new[] { "GSS-SPNEGO", "DIGEST-MD5", "CRAM-MD5", "NTLM" }, mechanisms.ToArray());
    }

    [TestMethod]
    public void GetSaslMechanisms_LdapWithoutNegotiateAccepted_IsTheMailLoginOffersMechanisms()
    {
        var policy = Policy();

        var mechanisms = policy.GetSaslMechanisms(new SaslOfferRequest("ldap", PolicyFixture.Tls));

        CollectionAssert.AreEqual(new[] { "CRAM-MD5", "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN" }, mechanisms.ToArray());
    }

    [TestMethod]
    public void GetSaslMechanisms_LdapWithOnlyNegotiateAccepted_ListsGssSpnegoAlone()
    {
        var policy = Policy(acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Negotiate });

        var mechanisms = policy.GetSaslMechanisms(new SaslOfferRequest("ldap", null));

        CollectionAssert.AreEqual(new[] { "GSS-SPNEGO" }, mechanisms.ToArray());
    }

    [TestMethod]
    public void GetSaslMechanisms_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Policy().GetSaslMechanisms(null!));
    }

    [TestMethod]
    public void StartSaslExchange_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Policy().StartSaslExchange(null!));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "checked")]
    [DataRow(true, DisplayName = "--allow-anonymous")]
    public async Task Apop_NotAcceptedByAuth_IsRefusedUndelayedAsNotOffered(bool allowAnonymous)
    {
        var login = new ApopLogin("pop3", "user", "<1.2@surl>", "32d4437494fda0ae78d0559952474e34", PolicyFixture.Tls);

        var step = await Policy(allowAnonymous: allowAnonymous).CheckApopLoginAsync(login, CancellationToken.None);

        Assert.AreEqual(new SaslLoginStep(SaslLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null), step);
    }

    [TestMethod]
    public async Task Apop_NullOrCancelled_Throws()
    {
        var login = new ApopLogin("pop3", "user", "<1.2@surl>", "00", null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Policy().CheckApopLoginAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Policy().CheckApopLoginAsync(login, cancellation.Token));
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "a mechanism's exchange")]
    [DataRow("FOO", DisplayName = "a refused exchange")]
    public async Task Exchange_BegunTwice_Throws(string mechanism)
    {
        var exchange = SaslExchangeRunner.Start(Policy(), mechanism, null, PolicyFixture.Tls);
        await exchange.BeginAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await exchange.BeginAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "a mechanism's exchange")]
    [DataRow("FOO", DisplayName = "a refused exchange")]
    public async Task Exchange_ContinuedBeforeBegun_Throws(string mechanism)
    {
        var exchange = SaslExchangeRunner.Start(Policy(), mechanism, null, PolicyFixture.Tls);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
    }

    [TestMethod]
    public async Task Exchange_ContinuedAfterItsLastStep_Throws()
    {
        var exchange = SaslExchangeRunner.Start(Policy(), "PLAIN", SaslExchangeRunner.Plain(string.Empty, "user", "secret"), PolicyFixture.Tls);
        await exchange.BeginAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
    }

    [TestMethod]
    public async Task Exchange_StepWithACancelledToken_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var begun = SaslExchangeRunner.Start(Policy(), "LOGIN", null, PolicyFixture.Tls);
        await begun.BeginAsync(CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await SaslExchangeRunner.Start(Policy(), "LOGIN", null, PolicyFixture.Tls).BeginAsync(cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await SaslExchangeRunner.Start(Policy(), "FOO", null, PolicyFixture.Tls).BeginAsync(cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await begun.ContinueAsync(SaslExchangeRunner.Utf8("user"), cancellation.Token));
    }
}
