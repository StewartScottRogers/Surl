using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Xmpp.UnitTests.XmppLoginScript;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// Resource binding (BL-538; ADR-0114 decision 4), driven through the server after a PLAIN login
/// as <c>user</c>, every request and answer written by hand from RFC 6120 sections 7.6, 7.7 and
/// 8.2.3 and their examples.
/// </summary>
[TestClass]
public sealed class XmppResourceBindingTests
{
    // RFC 6120 7.7.1's request, with the example's id.
    private const string BindBalcony =
        "<iq id='wy2xa82b4' type='set'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><resource>balcony</resource></bind></iq>";

    // RFC 6120 7.6.1's request.
    private const string BindGenerated = "<iq id='yhc13a95' type='set'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></iq>";

    private static readonly string LoggedIn = ResponseHeader(1) + MechanismFeatures("PLAIN") + Success + ResponseHeader(2) + BindFeatures;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_BindWithAResource_IsAnsweredWithTheFullJid()
    {
        var (answers, log) = await AfterLoginAsync(BindBalcony, Close);

        Assert.AreEqual(
            LoggedIn + "<iq type='result' id='wy2xa82b4'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>user@im.example.com/balcony</jid></bind></iq>" + Close,
            answers);
        CollectionAssert.Contains(log.Notes.ToList(), "Bound user@im.example.com/balcony.");
    }

    [TestMethod]
    public async Task AnswerAsync_BindWithoutAResource_IsAnsweredWithAGeneratedOne()
    {
        var (answers, _) = await AfterLoginAsync(BindGenerated, Close);

        Assert.AreEqual(
            LoggedIn + $"<iq type='result' id='yhc13a95'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>user@im.example.com/surl-{Hex(3, 16)}</jid></bind></iq>" + Close,
            answers);
    }

    [TestMethod]
    public async Task AnswerAsync_AnonymousLogin_BindsAnAnonName()
    {
        var policy = new UnitTestXmppSaslPolicy([], ["ANONYMOUS"]);

        var (answers, _) = await ServeAsync(
            Options(policy), TestContext.CancellationToken, ClientHeader, Auth("ANONYMOUS", "="), ClientHeader, BindBalcony, Close);

        StringAssert.Contains(answers, $"<jid>anon-{Hex(2, 16)}@im.example.com/balcony</jid>");
    }

    [TestMethod]
    public async Task AnswerAsync_MailboxAccountOnTheStreamDomain_BindsThatJid()
    {
        var (answers, _) = await ServeAsync(
            Options(PlainPolicy("Juliet@IM.example.com")), TestContext.CancellationToken,
            ClientHeader, Auth("PLAIN", PlainUserPencil), ClientHeader, BindBalcony, Close);

        StringAssert.Contains(answers, "<jid>Juliet@im.example.com/balcony</jid>");
    }

    [TestMethod]
    [DataRow("<message to='romeo@example.net'><body>hi</body></message>")]
    [DataRow("<presence/>")]
    [DataRow("<iq id='p1' type='get'><ping xmlns='urn:xmpp:ping'/></iq>")]
    [DataRow("<iq id='s1' type='set'><session xmlns='urn:ietf:params:xml:ns:xmpp-session'/></iq>")]
    [DataRow("<iq id='b1' type='get'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></iq>")]
    [DataRow("<iq id='b1' type='set'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></iq>")]
    [DataRow("<iq id='b1' type='set'><query xmlns='jabber:iq:roster'/></iq>")]
    public async Task AnswerAsync_StanzaBeforeBind_EndsTheStreamNotAuthorized(string stanza)
    {
        var (answers, log) = await AfterLoginAsync(stanza);

        Assert.AreEqual(LoggedIn + ErrorAndClose("not-authorized"), answers);
        Assert.IsTrue(log.Notes.Any(note => note.Contains("stanza arrived before a resource was bound", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("<iq type='set'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></iq>")]
    [DataRow("<iq id='b1' type='ask'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></iq>")]
    [DataRow("<iq id='b1'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></iq>")]
    public async Task AnswerAsync_IqWithNoIdOrAnUnknownType_IsInvalidXml(string stanza)
    {
        var (answers, _) = await AfterLoginAsync(stanza);

        Assert.AreEqual(LoggedIn + ErrorAndClose("invalid-xml"), answers);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("bal&#x7F;cony")]
    public async Task AnswerAsync_ResourceNotAResourcepart_IsBadRequestAndTheStreamKept(string resource)
    {
        var bind = $"<iq id='b1' type='set'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><resource>{resource}</resource></bind></iq>";

        var (answers, _) = await AfterLoginAsync(bind, Close);

        Assert.AreEqual(
            LoggedIn + "<iq type='error' id='b1'><error type='modify'><bad-request xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></iq>" + Close,
            answers);
    }

    [TestMethod]
    public async Task AnswerAsync_SecondBind_IsNotAllowed()
    {
        var (answers, _) = await AfterLoginAsync(BindBalcony, BindGenerated, Close);

        Assert.EndsWith(
            "<iq type='error' id='yhc13a95'><error type='cancel'><not-allowed xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></iq>" + Close,
            answers);
    }

    [TestMethod]
    public async Task AnswerAsync_LegacySessionAfterBind_IsAnEmptyResult()
    {
        var (answers, _) = await AfterLoginAsync(
            BindBalcony, "<iq id='s1' type='set'><session xmlns='urn:ietf:params:xml:ns:xmpp-session'/></iq>", Close);

        Assert.EndsWith("<iq type='result' id='s1'/>" + Close, answers);
    }

    [TestMethod]
    public async Task AnswerAsync_OtherIqAfterBind_IsServiceUnavailable()
    {
        var (answers, _) = await AfterLoginAsync(BindBalcony, "<iq id='p1' type='get'><ping xmlns='urn:xmpp:ping'/></iq>", Close);

        Assert.EndsWith(
            "<iq type='error' id='p1'><error type='cancel'><service-unavailable xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></iq>" + Close,
            answers);
    }

    [TestMethod]
    [DataRow("<iq id='r1' type='result'/>")]
    [DataRow("<iq id='r1' type='error'/>")]
    [DataRow("<message to='romeo@example.net'><body>hi</body></message>")]
    public async Task AnswerAsync_IqReplyOrMessageAfterBind_IsNotAnswered(string stanza)
    {
        var (answers, _) = await AfterLoginAsync(BindBalcony, stanza, Close);

        Assert.EndsWith("</jid></bind></iq>" + Close, answers);
    }

    [TestMethod]
    [DataRow("<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>", "policy-violation")]
    [DataRow("<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>=</auth>", "policy-violation")]
    [DataRow("<thing/>", "unsupported-stanza-type")]
    public async Task AnswerAsync_NonStanzaAfterLogin_EndsTheStream(string element, string condition)
    {
        var (answers, _) = await AfterLoginAsync(element);

        Assert.AreEqual(LoggedIn + ErrorAndClose(condition), answers);
    }

    private Task<(string Answers, RecordingExchangeLog Log)> AfterLoginAsync(params string[] chunks) =>
        ServeAsync(Options(PlainPolicy()), TestContext.CancellationToken, [ClientHeader, Auth("PLAIN", PlainUserPencil), ClientHeader, .. chunks]);
}
