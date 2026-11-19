using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Xmpp.UnitTests.XmppLoginScript;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// SASL ANONYMOUS on an XMPP stream (BL-1399; ADR-0114 decision 3), written from RFC 4505 and RFC 6120
/// sections 6.4.2 and 5.3.1: offered last under <c>OffersAnonymous</c> only, served with an <c>anon-</c> JID.
/// </summary>
[TestClass]
public sealed class XmppAnonymousLoginTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public async Task Features_OffersAnonymousLastOnlyWhenAsked(bool offersAnonymous, bool expected)
    {
        var options = Options(Policy()) with { OffersAnonymous = offersAnonymous };

        var (answers, _) = await ServeAsync(options, TestContext.CancellationToken, ClientHeader, Close);

        var expectedMechanisms = expected ? MechanismFeatures("SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN", "ANONYMOUS") : MechanismFeatures("SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN");
        Assert.AreEqual(ResponseHeader(1) + expectedMechanisms + Close, answers);
    }

    [TestMethod]
    [DataRow("=")]
    [DataRow("")]
    [DataRow("dHJhY2U=")]
    public async Task StartAsync_Anonymous_SucceedsAndBindsAnAnonJidFromTheRandomSource(string initialResponse)
    {
        var options = Options(Policy()) with { OffersAnonymous = true };

        var (answers, log) = await ServeAsync(
            options, TestContext.CancellationToken, ClientHeader, Auth("ANONYMOUS", initialResponse), ClientHeader, BindBalcony, Close);

        StringAssert.Contains(answers, Success + ResponseHeader(3));
        StringAssert.Contains(answers, $"<jid>anon-{Hex(2, 16)}@im.example.com/balcony</jid>");
        CollectionAssert.Contains(log.Notes.ToList(), $"Logged in as anon-{Hex(2, 16)}@im.example.com.");
    }

    [TestMethod]
    public async Task StartAsync_AnonymousNotOffered_IsInvalidMechanism()
    {
        var (answers, _) = await ServeAsync(
            Options(Policy()), TestContext.CancellationToken, ClientHeader, Auth("ANONYMOUS", "="), Close);

        Assert.AreEqual(
            ResponseHeader(1) + MechanismFeatures("SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN") + Failure("invalid-mechanism") + Close, answers);
    }

    [TestMethod]
    public async Task StartAsync_AnonymousBeforeTlsWhileRequired_IsEncryptionRequired()
    {
        var options = Options(Policy()) with { OffersAnonymous = true };

        var (answers, _) = await ServeAsync(
            options, TestContext.CancellationToken, isTls: false, ClientHeader, Auth("ANONYMOUS", "="), Close);

        StringAssert.Contains(answers, Failure("encryption-required"));
    }

    private const string BindBalcony =
        "<iq type='set' id='bind_1' xmlns='jabber:client'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><resource>balcony</resource></bind></iq>";
}
