using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/>'s SASL <c>EXTERNAL</c> against ADR-0049 sections 2, 4, 5
/// and 7: the client is its verified TLS client certificate, built here in memory, and the
/// response is the authorization identity upstream curl 8.21.0 sent when ADR-0049 measured it
/// (<c>-u user:</c>, <c>dXNlcg==</c>).
/// </summary>
[TestClass]
public sealed class ExternalSaslMechanismTests
{
    // ADR-0049, "What curl sends for each mechanism": the -u user name, base64 as recorded.
    private const string CurlExternal = "dXNlcg==";

    private static readonly TlsSession UserCertificateTls = TlsWithCertificate("user");

    private readonly ManualTimeProvider clock = new();
    private readonly SaslExchangeRunner runner;

    public ExternalSaslMechanismTests()
    {
        runner = new SaslExchangeRunner(clock);
    }

    private AuthenticationPolicy Policy(
        AccountBook? accounts = null,
        bool allowAnonymous = false,
        IReadOnlySet<AuthenticationMethod>? acceptedMethods = null) =>
        PolicyFixture.Create(accounts ?? SaslExchangeRunner.UserAndToken, clock, allowAnonymous, false, acceptedMethods);

    private static TlsSession TlsWithCertificate(string commonName)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;

        return PolicyFixture.Tls with { ClientCertificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1)) };
    }

    [TestMethod]
    [DataRow(CurlExternal, DisplayName = "curl's authorization identity, the name")]
    [DataRow("", DisplayName = "empty authorization identity")]
    public async Task CertificateNamingAnAccount_WithInitialResponse_IsAcceptedUndelayedAsTheName(string authzid)
    {
        var exchange = SaslExchangeRunner.Start(Policy(), "EXTERNAL", Convert.FromBase64String(authzid), UserCertificateTls);

        var pending = exchange.BeginAsync(CancellationToken.None);

        Assert.IsTrue(pending.IsCompleted);
        var step = await pending;
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("user", step.AccountName);
        Assert.AreEqual(new CheckedLogin("EXTERNAL", "user", true), step.CheckedLogin);
    }

    [TestMethod]
    [DataRow(CurlExternal, DisplayName = "curl's authorization identity, the name")]
    [DataRow("", DisplayName = "empty authorization identity")]
    public async Task CertificateNamingAnAccount_WithoutInitialResponse_IsAskedWithOneEmptyChallengeThenAccepted(string authzid)
    {
        var exchange = SaslExchangeRunner.Start(Policy(), "EXTERNAL", null, UserCertificateTls);

        var steps = await runner.RunAsync(exchange, Convert.FromBase64String(authzid));

        Assert.AreEqual(new SaslLoginStep(SaslLoginOutcome.Challenge, ReadOnlyMemory<byte>.Empty, null, null), steps[0]);
        Assert.AreEqual(SaslLoginOutcome.Accepted, steps[1].Outcome);
        Assert.AreEqual("user", steps[1].AccountName);
        Assert.AreEqual("Login accepted: EXTERNAL user", steps[1].CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow("bob", "user", DisplayName = "no account of the certificate's name")]
    [DataRow("user", "boss", DisplayName = "another authorization identity")]
    [DataRow("user", "User", DisplayName = "the name in another case")]
    public async Task UnknownNameOrOtherAuthorizationIdentity_IsRefusedAfterTheDelayWithTheCertificatesName(
        string commonName, string authzid)
    {
        var exchange = SaslExchangeRunner.Start(
            Policy(), "EXTERNAL", SaslExchangeRunner.Utf8(authzid), TlsWithCertificate(commonName));

        var pending = exchange.BeginAsync(CancellationToken.None).AsTask();

        Assert.IsFalse(pending.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromTicks(1));
        Assert.IsFalse(pending.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        var step = await pending;
        Assert.AreEqual(
            new SaslLoginStep(
                SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin("EXTERNAL", commonName, false)),
            step);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no initial response")]
    [DataRow("Ym9zcw==", DisplayName = "another authorization identity")]
    public async Task AllowAnonymous_AcceptsTheFirstResponseUncheckedWithNoNote(string? response)
    {
        var exchange = SaslExchangeRunner.Start(Policy(PolicyFixture.NoAccounts, allowAnonymous: true), "EXTERNAL", null, UserCertificateTls);

        var steps = await runner.RunAsync(exchange, response is null ? [] : Convert.FromBase64String(response));

        Assert.AreEqual(new SaslLoginStep(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null), steps[^1]);
    }

    [TestMethod]
    [DataRow(false, false, DisplayName = "no TLS")]
    [DataRow(true, false, DisplayName = "TLS without a client certificate")]
    [DataRow(false, true, DisplayName = "no TLS, --allow-anonymous")]
    [DataRow(true, true, DisplayName = "TLS without a client certificate, --allow-anonymous")]
    public async Task NoClientCertificate_IsRefusedAsAMechanismUndelayedAndNotOffered(bool isEncrypted, bool allowAnonymous)
    {
        var tls = isEncrypted ? PolicyFixture.Tls : null;
        var policy = Policy(allowAnonymous: allowAnonymous);

        var pending = SaslExchangeRunner.Start(policy, "EXTERNAL", SaslExchangeRunner.Utf8("user"), tls).BeginAsync(CancellationToken.None);

        Assert.IsTrue(pending.IsCompleted);
        Assert.AreEqual(new SaslLoginStep(SaslLoginOutcome.RefusedMechanism, ReadOnlyMemory<byte>.Empty, null, null), await pending);
        CollectionAssert.DoesNotContain(policy.GetMailLoginOffer(tls).SaslMechanisms.ToArray(), "EXTERNAL");
    }

    [TestMethod]
    public void Offer_WithClientCertificate_ListsExternalLast()
    {
        var offer = Policy().GetMailLoginOffer(UserCertificateTls);

        CollectionAssert.AreEqual(
            new[] { "CRAM-MD5", "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN", "EXTERNAL" }, offer.SaslMechanisms.ToArray());
    }

    [TestMethod]
    public async Task NotAcceptedByAuth_IsNotOfferedAndRefusedAsAMechanism()
    {
        var policy = Policy(acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Plain });

        var step = await SaslExchangeRunner.Start(policy, "EXTERNAL", [], UserCertificateTls).BeginAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "PLAIN" }, policy.GetMailLoginOffer(UserCertificateTls).SaslMechanisms.ToArray());
        Assert.AreEqual(SaslLoginOutcome.RefusedMechanism, step.Outcome);
    }
}
