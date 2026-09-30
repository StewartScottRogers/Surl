using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/> as the <see cref="IMailAuthenticationPolicy"/>: the offer
/// (ADR-0049, section 2), which mechanism an exchange runs, <c>APOP</c> before BL-195, and the
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
    public void Offer_DefaultSetOverTls_IsTheFourMechanismsInAdrOrderAndTheClearPassword()
    {
        var offer = Policy().GetMailLoginOffer(PolicyFixture.Tls);

        CollectionAssert.AreEqual(new[] { "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN" }, offer.SaslMechanisms.ToArray());
        Assert.IsTrue(offer.IsClearPasswordLoginOffered);
        Assert.IsFalse(offer.IsApopOffered);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "checked")]
    [DataRow(true, DisplayName = "--allow-anonymous")]
    public void Offer_WithoutTls_OffersNoPlaintextSecret(bool allowAnonymous)
    {
        var offer = Policy(allowAnonymous: allowAnonymous).GetMailLoginOffer(null);

        Assert.AreEqual(0, offer.SaslMechanisms.Count);
        Assert.IsFalse(offer.IsClearPasswordLoginOffered);
        Assert.IsFalse(offer.IsApopOffered);
    }

    [TestMethod]
    public void Offer_AllowPlaintextAuthWithoutTls_OffersThePlaintextMechanisms()
    {
        var offer = Policy(allowPlaintextAuth: true).GetMailLoginOffer(null);

        CollectionAssert.AreEqual(new[] { "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN" }, offer.SaslMechanisms.ToArray());
        Assert.IsTrue(offer.IsClearPasswordLoginOffered);
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

        Assert.AreEqual(MailLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("Login accepted: PLAIN user", step.CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow("CRAM-MD5", false, DisplayName = "not built yet")]
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
        Assert.AreEqual(new MailLoginStep(MailLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null), await step);
    }

    [TestMethod]
    public void StartSaslExchange_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Policy().StartSaslExchange(null!));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "checked")]
    [DataRow(true, DisplayName = "--allow-anonymous")]
    public async Task Apop_IsRefusedAsNotOfferedUntilBl195(bool allowAnonymous)
    {
        var login = new ApopLogin("pop3", "user", "<1.2@surl>", "32d4437494fda0ae78d0559952474e34", PolicyFixture.Tls);

        var step = await Policy(allowAnonymous: allowAnonymous).CheckApopLoginAsync(login, CancellationToken.None);

        Assert.AreEqual(new MailLoginStep(MailLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null), step);
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
