using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Xmpp.UnitTests.XmppLoginScript;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// Stanza routing and the server's IQ answers (BL-539; ADR-0114 decision 5), driven through one
/// server with several streams open at once, each logged in over PLAIN and bound. Every stanza and
/// every expected answer is written by hand from RFC 6120 sections 8 and 10, RFC 6121 sections 4.7
/// and 8.5, XEP-0030 examples 1 to 4 and XEP-0199 section 4.2. A ping to the server after each
/// stanza is the fence: once it is answered, the stanza before it has been answered or delivered.
/// </summary>
[TestClass]
public sealed class XmppStanzaRouterTests
{
    private const string Domain = "im.example.com";

    private const string Stanzas = "xmlns=\"urn:ietf:params:xml:ns:xmpp-stanzas\"";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_ChatToAnotherUsersFullJid_IsDeliveredToThatStreamWithFromStamped()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, "<message to='romeo@im.example.com/orchard' type='chat' id='m1'><body>Art thou not Romeo?</body></message>");

        Assert.AreEqual(string.Empty, answers);
        Assert.AreEqual(
            "<message to=\"romeo@im.example.com/orchard\" type=\"chat\" id=\"m1\" from=\"juliet@im.example.com/balcony\" xmlns=\"jabber:client\">" +
            "<body>Art thou not Romeo?</body></message>",
            Received(romeo));
        CollectionAssert.Contains(juliet.Log.Notes.ToList(), "Routed a message from juliet@im.example.com/balcony to romeo@im.example.com/orchard.");
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    [DataRow("romeo@im.example.com")]
    [DataRow("nobody@im.example.com")]
    [DataRow("nobody@im.example.com/orchard")]
    public async Task AnswerAsync_ChatToAUserWithNoStream_IsServiceUnavailableWhetherOrNotTheAccountExists(string to)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, $"<message to='{to}' type='chat' id='m1'><body>Wherefore?</body></message>");

        Assert.AreEqual(
            $"<message to=\"juliet@im.example.com/balcony\" type=\"error\" id=\"m1\" from=\"{to}\" xmlns=\"jabber:client\">" +
            $"<error type=\"cancel\"><service-unavailable {Stanzas} /></error></message>",
            answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_ChatToABareJid_GoesToEveryAvailableStreamWithNonNegativePriority()
    {
        var server = NewServer();
        var balcony = await OpenAsync(server, "juliet", "balcony");
        var chamber = await OpenAsync(server, "juliet", "chamber");
        var garden = await OpenAsync(server, "juliet", "garden");
        var romeo = await OpenAsync(server, "romeo", "orchard");
        await SendAsync(chamber, "<presence><priority>-1</priority></presence>");
        await SendAsync(garden, "<presence type='unavailable'/>");

        var answers = await SendAsync(romeo, "<message to='juliet@im.example.com'><body>Hi</body></message>");

        Assert.AreEqual(string.Empty, answers);
        Assert.AreEqual(
            "<message to=\"juliet@im.example.com\" from=\"romeo@im.example.com/orchard\" xmlns=\"jabber:client\"><body>Hi</body></message>", Received(balcony));
        Assert.AreEqual(string.Empty, Received(chamber));
        Assert.AreEqual(string.Empty, Received(garden));
        CollectionAssert.Contains(garden.Log.Notes.ToList(), "juliet@im.example.com/garden is unavailable.");
        await CloseAsync(balcony, chamber, garden, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_PresenceAgainAfterUnavailable_MakesTheStreamAvailable()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");
        await SendAsync(juliet, "<presence type='unavailable'/>");
        await SendAsync(juliet, "<presence><priority>5</priority></presence>");

        await SendAsync(romeo, "<message to='juliet@im.example.com' type='chat'><body>Hi</body></message>");

        StringAssert.Contains(Received(juliet), "<body>Hi</body>");
        CollectionAssert.Contains(juliet.Log.Notes.ToList(), "juliet@im.example.com/balcony is available at priority 5.");
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_PresenceWithNoPriority_IsAvailableAtPriorityZero()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, "<presence/>");

        Assert.AreEqual(string.Empty, answers);
        CollectionAssert.Contains(juliet.Log.Notes.ToList(), "juliet@im.example.com/balcony is available at priority 0.");
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_MessageWithNoTo_IsHandledAsAddressedToTheSendersBareJid()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var chamber = await OpenAsync(server, "juliet", "chamber");

        var answers = await SendAsync(juliet, "<message><body>Note to self</body></message>");

        Assert.AreEqual(
            "<message from=\"juliet@im.example.com/balcony\" xmlns=\"jabber:client\"><body>Note to self</body></message>", answers);
        StringAssert.Contains(Received(chamber), "<body>Note to self</body>");
        await CloseAsync(juliet, chamber);
    }

    [TestMethod]
    public async Task AnswerAsync_MessageOfUnknownType_IsTreatedAsNormalAndDeliveredToTheBareJidsStreams()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(romeo, "<message to='juliet@im.example.com' type='gossip'><body>Hi</body></message>");

        Assert.AreEqual(string.Empty, answers);
        StringAssert.Contains(Received(juliet), "<body>Hi</body>");
        var unanswered = await SendAsync(romeo, "<message to='nobody@im.example.com' type='gossip'><body>Hi</body></message>");
        StringAssert.Contains(unanswered, $"<service-unavailable {Stanzas} />");
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    [DataRow("headline")]
    [DataRow("error")]
    public async Task AnswerAsync_HeadlineOrErrorToAUserWithNoStream_IsDropped(string type)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, $"<message to='romeo@im.example.com' type='{type}'><body>News</body></message>");

        Assert.AreEqual(string.Empty, answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_GroupchatToAUser_IsServiceUnavailable()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, "<message to='romeo@im.example.com' type='groupchat'><body>All</body></message>");

        StringAssert.Contains(answers, $"<service-unavailable {Stanzas} />");
        Assert.AreEqual(string.Empty, Received(romeo));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_ErrorMessageToABoundFullJid_IsDelivered()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        await SendAsync(juliet, "<message to='romeo@im.example.com/orchard' type='error'/>");

        Assert.AreEqual(
            "<message to=\"romeo@im.example.com/orchard\" type=\"error\" from=\"juliet@im.example.com/balcony\" xmlns=\"jabber:client\" />", Received(romeo));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("normal", true)]
    [DataRow("chat", true)]
    [DataRow("headline", false)]
    [DataRow("groupchat", false)]
    [DataRow("error", false)]
    public async Task AnswerAsync_MessageToTheServer_IsServiceUnavailableForNormalAndChatAndDroppedOtherwise(string? type, bool isRefused)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var typeAttribute = type is null ? string.Empty : $" type='{type}'";

        var answers = await SendAsync(juliet, $"<message to='im.example.com'{typeAttribute}><body>Hello server</body></message>");

        Assert.AreEqual(isRefused, answers.Contains("<service-unavailable", StringComparison.Ordinal));
        Assert.AreEqual(isRefused, answers.Length > 0);
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_MessageToAnotherServersDomain_IsRemoteServerNotFound()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, "<message to='romeo@example.net' id='m2'><body>hi</body></message>");

        Assert.AreEqual(
            "<message to=\"juliet@im.example.com/balcony\" id=\"m2\" type=\"error\" from=\"romeo@example.net\" xmlns=\"jabber:client\">" +
            $"<error type=\"cancel\"><remote-server-not-found {Stanzas} /></error></message>",
            answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    [DataRow("romeo@")]
    [DataRow("ro meo@im.example.com")]
    [DataRow("romeo@im.example.com/")]
    [DataRow("romeo@im example.com")]
    [DataRow("a@b@im.example.com")]
    public async Task AnswerAsync_ToThatIsNotAJid_IsJidMalformed(string to)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, $"<message to='{to}'><body>hi</body></message>");

        StringAssert.Contains(answers, $"<error type=\"modify\"><jid-malformed {Stanzas} /></error>");
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_DiscoInfoToTheServer_IsAnsweredInXep0030Example2sShape()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        // XEP-0030 example 1, from the stream's own full JID.
        var answers = await SendAsync(
            juliet,
            "<iq type='get' from='juliet@im.example.com/balcony' to='im.example.com' id='info1'><query xmlns='http://jabber.org/protocol/disco#info'/></iq>");

        Assert.AreEqual(
            "<iq type='result' from='im.example.com' to='juliet@im.example.com/balcony' id='info1'>" +
            "<query xmlns='http://jabber.org/protocol/disco#info'><identity category='server' type='im'/>" +
            "<feature var='http://jabber.org/protocol/disco#info'/><feature var='http://jabber.org/protocol/disco#items'/>" +
            "<feature var='urn:xmpp:ping'/></query></iq>",
            answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_DiscoItemsToTheServer_IsAnEmptyQuery()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, "<iq type='get' to='im.example.com' id='items1'><query xmlns='http://jabber.org/protocol/disco#items'/></iq>");

        Assert.AreEqual(
            "<iq type='result' from='im.example.com' to='juliet@im.example.com/balcony' id='items1'><query xmlns='http://jabber.org/protocol/disco#items'/></iq>",
            answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_PingToTheServer_IsAnEmptyResult()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        // XEP-0199 example 3, the domain in this server's name.
        var answers = await SendAsync(juliet, "<iq from='juliet@im.example.com/balcony' to='im.example.com' id='c2s1' type='get'><ping xmlns='urn:xmpp:ping'/></iq>");

        Assert.AreEqual("<iq type='result' from='im.example.com' to='juliet@im.example.com/balcony' id='c2s1'/>", answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_LegacySessionToTheServer_IsAnEmptyResult()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, "<iq type='set' to='im.example.com' id='s1'><session xmlns='urn:ietf:params:xml:ns:xmpp-session'/></iq>");

        Assert.AreEqual("<iq type='result' from='im.example.com' to='juliet@im.example.com/balcony' id='s1'/>", answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    [DataRow("im.example.com", "http://jabber.org/protocol/disco#info")]
    [DataRow("im.example.com", "http://jabber.org/protocol/disco#items")]
    [DataRow("romeo@im.example.com", "http://jabber.org/protocol/disco#info")]
    [DataRow("romeo@im.example.com", "http://jabber.org/protocol/disco#items")]
    public async Task AnswerAsync_DiscoWithANode_IsItemNotFound(string to, string space)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, $"<iq type='get' to='{to}' id='n1'><query xmlns='{space}' node='http://jabber.org/protocol/commands'/></iq>");

        StringAssert.Contains(answers, $"<error type=\"cancel\"><item-not-found {Stanzas} /></error></iq>");
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_DiscoInfoToABareJidWithABoundStream_IsARegisteredAccount()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, "<iq type='get' to='Romeo@IM.example.com' id='a1'><query xmlns='http://jabber.org/protocol/disco#info'/></iq>");

        Assert.AreEqual(
            "<iq type='result' from='Romeo@IM.example.com' to='juliet@im.example.com/balcony' id='a1'>" +
            "<query xmlns='http://jabber.org/protocol/disco#info'><identity category='account' type='registered'/>" +
            "<feature var='http://jabber.org/protocol/disco#info'/><feature var='http://jabber.org/protocol/disco#items'/></query></iq>",
            answers);
        Assert.AreEqual(string.Empty, Received(romeo));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_DiscoItemsToABareJidWithABoundStream_IsAnEmptyQuery()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, "<iq type='get' to='romeo@im.example.com' id='a2'><query xmlns='http://jabber.org/protocol/disco#items'/></iq>");

        Assert.AreEqual(
            "<iq type='result' from='romeo@im.example.com' to='juliet@im.example.com/balcony' id='a2'><query xmlns='http://jabber.org/protocol/disco#items'/></iq>",
            answers);
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_RosterGetOfOnesOwn_IsAnEmptyRoster()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        // RFC 6121 2.1.3's request, which names no to.
        var answers = await SendAsync(juliet, "<iq from='juliet@im.example.com/balcony' id='bv1bs71f' type='get'><query xmlns='jabber:iq:roster'/></iq>");

        Assert.AreEqual("<iq type='result' to='juliet@im.example.com/balcony' id='bv1bs71f'><query xmlns='jabber:iq:roster'/></iq>", answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    [DataRow("<iq type='get' to='romeo@im.example.com' id='u1'><query xmlns='jabber:iq:roster'/></iq>")]
    [DataRow("<iq type='get' to='nobody@im.example.com' id='u1'><query xmlns='http://jabber.org/protocol/disco#info'/></iq>")]
    [DataRow("<iq type='get' to='im.example.com' id='u1'><query xmlns='jabber:iq:version'/></iq>")]
    [DataRow("<iq type='set' to='im.example.com' id='u1'><query xmlns='http://jabber.org/protocol/disco#info'/></iq>")]
    [DataRow("<iq type='get' to='im.example.com' id='u1'><info xmlns='http://jabber.org/protocol/disco#info'/></iq>")]
    [DataRow("<iq type='get' to='im.example.com/desk' id='u1'><ping xmlns='urn:xmpp:ping'/></iq>")]
    [DataRow("<iq type='get' to='romeo@im.example.com/elsewhere' id='u1'><ping xmlns='urn:xmpp:ping'/></iq>")]
    [DataRow("<iq type='set' id='u1'><query xmlns='jabber:iq:private'/></iq>")]
    public async Task AnswerAsync_IqNoOneAnswers_IsServiceUnavailable(string iq)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, iq);

        StringAssert.StartsWith(answers, "<iq ");
        StringAssert.EndsWith(answers, $"<error type=\"cancel\"><service-unavailable {Stanzas} /></error></iq>");
        Assert.AreEqual(string.Empty, Received(romeo));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_IqInAnUnknownNamespace_IsServiceUnavailableWithTheChildKept()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, "<iq type='get' to='im.example.com' id='v1'><query xmlns='jabber:iq:version'/></iq>");

        Assert.AreEqual(
            "<iq type=\"error\" to=\"juliet@im.example.com/balcony\" id=\"v1\" from=\"im.example.com\" xmlns=\"jabber:client\">" +
            $"<query xmlns=\"jabber:iq:version\" /><error type=\"cancel\"><service-unavailable {Stanzas} /></error></iq>",
            answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    [DataRow("<iq type='get' to='im.example.com' id='t1'><ping xmlns='urn:xmpp:ping'/><ping xmlns='urn:xmpp:ping'/></iq>")]
    [DataRow("<iq type='get' to='im.example.com' id='t1'/>")]
    public async Task AnswerAsync_IqGetWithoutExactlyOneChild_IsBadRequest(string iq)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, iq);

        StringAssert.EndsWith(answers, $"<error type=\"modify\"><bad-request {Stanzas} /></error></iq>");
        await CloseAsync(juliet);
    }

    [TestMethod]
    [DataRow("<iq type='result' to='im.example.com' id='r1'/>")]
    [DataRow("<iq type='error' to='romeo@im.example.com' id='r1'/>")]
    [DataRow("<iq type='result' id='r1'><query xmlns='jabber:iq:version'/></iq>")]
    public async Task AnswerAsync_IqResultOrErrorFromTheClient_IsNeverAnswered(string iq)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, iq);

        Assert.AreEqual(string.Empty, answers);
        Assert.IsTrue(juliet.Log.Notes.Any(note => note.Contains("an IQ reply is never answered", StringComparison.Ordinal)));
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_IqToABoundFullJid_IsDeliveredAndItsResultRoutedBack()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        await SendAsync(juliet, "<iq type='get' to='romeo@im.example.com/orchard' id='v1'><query xmlns='jabber:iq:version'/></iq>");
        var delivered = Received(romeo);
        await SendAsync(romeo, "<iq type='result' to='juliet@im.example.com/balcony' id='v1'><query xmlns='jabber:iq:version'/></iq>");

        Assert.AreEqual(
            "<iq type=\"get\" to=\"romeo@im.example.com/orchard\" id=\"v1\" from=\"juliet@im.example.com/balcony\" xmlns=\"jabber:client\"><query xmlns=\"jabber:iq:version\" /></iq>",
            delivered);
        Assert.AreEqual(
            "<iq type=\"result\" to=\"juliet@im.example.com/balcony\" id=\"v1\" from=\"romeo@im.example.com/orchard\" xmlns=\"jabber:client\"><query xmlns=\"jabber:iq:version\" /></iq>",
            Received(juliet));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    [DataRow("romeo@im.example.com/orchard")]
    [DataRow("romeo@im.example.com")]
    [DataRow("juliet@im.example.com/chamber")]
    [DataRow("not a jid@")]
    public async Task AnswerAsync_FromThatIsNotTheStreamsJid_EndsTheStreamInvalidFrom(string from)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        juliet.Connection.Send($"<message from='{from}' to='romeo@im.example.com/orchard'><body>Forged</body></message>");
        await juliet.Serving;

        Assert.AreEqual(ErrorAndClose("invalid-from"), Received(juliet));
        Assert.AreEqual(string.Empty, Received(romeo));
        await CloseAsync(romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_FromThatIsTheStreamsBareJid_IsAcceptedAndStampedWithTheFullJid()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        await SendAsync(juliet, "<message from='Juliet@im.example.com' to='romeo@im.example.com/orchard'><body>Me</body></message>");

        StringAssert.Contains(Received(romeo), "from=\"juliet@im.example.com/balcony\"");
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    [DataRow("128")]
    [DataRow("-129")]
    [DataRow("high")]
    public async Task AnswerAsync_PresencePriorityOutOfRange_IsBadRequest(string priority)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");

        var answers = await SendAsync(juliet, $"<presence><priority>{priority}</priority></presence>");

        Assert.AreEqual(
            $"<presence type=\"error\" to=\"juliet@im.example.com/balcony\" xmlns=\"jabber:client\"><error type=\"modify\"><bad-request {Stanzas} /></error></presence>",
            answers);
        await CloseAsync(juliet);
    }

    [TestMethod]
    [DataRow("<presence to='romeo@im.example.com'/>")]
    [DataRow("<presence to='romeo@im.example.com' type='subscribe'/>")]
    [DataRow("<presence type='probe'/>")]
    [DataRow("<presence type='error'><priority>500</priority></presence>")]
    public async Task AnswerAsync_DirectedOrSubscriptionPresence_IsDropped(string presence)
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, presence);

        Assert.AreEqual(string.Empty, answers);
        Assert.AreEqual(string.Empty, Received(romeo));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_RecipientsConnectionFails_IsNotedInTheRecipientsLogAndTheSenderKept()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");
        romeo.Connection.WritesFail = true;

        var answers = await SendAsync(juliet, "<message to='romeo@im.example.com/orchard'><body>Hi</body></message>");

        Assert.AreEqual(string.Empty, answers);
        await WaitUntilAsync(() => romeo.Log.Notes.Any(note => note.StartsWith("A routed stanza could not be written", StringComparison.Ordinal)));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_RecipientsWritesCancelled_IsNotedInTheRecipientsLog()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");
        romeo.Connection.WritesAreCancelled = true;

        await SendAsync(juliet, "<message to='romeo@im.example.com/orchard'><body>Hi</body></message>");

        await WaitUntilAsync(() => romeo.Log.Notes.Any(note => note.StartsWith("A routed stanza could not be written", StringComparison.Ordinal)));
        romeo.Connection.WritesAreCancelled = false;
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task ServeAsync_StreamThatEnded_IsNoLongerRoutedTo()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");
        await CloseAsync(romeo);

        var answers = await SendAsync(juliet, "<message to='romeo@im.example.com/orchard' type='chat'><body>Gone?</body></message>");

        StringAssert.Contains(answers, $"<service-unavailable {Stanzas} />");
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task ServeAsync_TwoServers_NeverRouteBetweenEachOther()
    {
        var juliet = await OpenAsync(NewServer(), "juliet", "balcony");
        var romeo = await OpenAsync(NewServer(), "romeo", "orchard");

        var answers = await SendAsync(juliet, "<message to='romeo@im.example.com/orchard' type='chat'><body>Hi</body></message>");

        StringAssert.Contains(answers, $"<service-unavailable {Stanzas} />");
        Assert.AreEqual(string.Empty, Received(romeo));
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task ServeAsync_RecipientWhoseWritesNeverComplete_LeavesTheSenderFreeToSendAndBeAnswered()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony");
        var romeo = await OpenAsync(server, "romeo", "orchard");
        romeo.Connection.WritesWaitFor = new TaskCompletionSource().Task;

        var answers = await SendAsync(juliet, "<message to='romeo@im.example.com/orchard' type='chat'><body>Hi</body></message>");
        var second = await SendAsync(juliet, "<message to='nobody@im.example.com/none' type='chat'><body>Hi</body></message>");

        Assert.AreEqual(string.Empty, answers);
        StringAssert.Contains(second, $"<service-unavailable {Stanzas} />");
        Assert.AreEqual(string.Empty, Received(romeo));
        romeo.Connection.Abort();
        juliet.Connection.Close();
        await Task.WhenAll(juliet.Serving, romeo.Serving).WaitAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ServeAsync_RecipientWithMoreThan16MiBRoutedUnsent_EndsWithPolicyViolationAndTheSenderGetsNoError()
    {
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony", ExchangeLimits.Default with { MaxMessageBytes = 1024 * 1024 });
        var romeo = await OpenAsync(server, "romeo", "orchard");
        var slowReader = new TaskCompletionSource();
        romeo.Connection.WritesWaitFor = slowReader.Task;
        var body = new string('a', 512 * 1024);

        var answers = new StringBuilder();
        for (var number = 0; number < 33; number++)
        {
            answers.Append(await SendAsync(juliet, $"<message to='romeo@im.example.com/orchard' type='chat'><body>{body}</body></message>"));
        }

        slowReader.SetResult();
        await romeo.Serving.WaitAsync(TestContext.CancellationToken);
        StringAssert.EndsWith(
            romeo.Connection.Written,
            "<stream:error><policy-violation xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>");
        Assert.AreEqual(string.Empty, answers.ToString());
        juliet.Connection.Close();
        await juliet.Serving;
    }

    // BL-1063 (ADR-0124 decisions 4 and 9): a stanza an error rule matches is answered with the rule's condition.
    private static FaultRule StanzaRule(FaultKind kind, string? reply = null, string command = "message") =>
        new("f1", kind) { Scheme = "xmpp", Command = command, Path = "romeo@im.example.com/orchard", Reply = reply, DelayMilliseconds = kind == FaultKind.Delay ? 1 : null };

    private const string ChatToRomeo = "<message to='romeo@im.example.com/orchard' type='chat' id='m1'><body>Hi</body></message>";

    [TestMethod]
    public async Task AnswerAsync_StanzaUnderAnErrorRule_GetsTheChosenConditionAndIsNotDelivered()
    {
        var faults = new ScriptedFaultPlan(StanzaRule(FaultKind.Error, "recipient-unavailable"));
        var events = new RecordingSurlEventPublisher();
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony", faults: faults, events: events);
        var romeo = await OpenAsync(server, "romeo", "orchard");

        var answers = await SendAsync(juliet, ChatToRomeo);

        Assert.AreEqual(
            "<message to=\"juliet@im.example.com/balcony\" type=\"error\" id=\"m1\" from=\"romeo@im.example.com/orchard\" xmlns=\"jabber:client\">" +
            $"<error type=\"wait\"><recipient-unavailable {Stanzas} /></error></message>",
            answers);
        Assert.AreEqual(string.Empty, Received(romeo));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "romeo@im.example.com/orchard"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command)
            {
                ExchangeId = 1, ListenUrl = "xmpp://im.example.com:5222/", Scheme = "xmpp", Account = "juliet", Path = "romeo@im.example.com/orchard", Command = "message",
            },
            faults.Points.First());
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_StanzaTheRuleDoesNotName_IsServed()
    {
        var events = new RecordingSurlEventPublisher();
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony", faults: new ScriptedFaultPlan(StanzaRule(FaultKind.Error, "forbidden", "presence")), events: events);
        var romeo = await OpenAsync(server, "romeo", "orchard");

        await SendAsync(juliet, ChatToRomeo);

        StringAssert.Contains(Received(romeo), "<body>Hi</body>");
        Assert.IsEmpty(events.Events);
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    public async Task AnswerAsync_StanzaWithNoToUnderADelayRule_IsServedAfterTheHold()
    {
        var events = new RecordingSurlEventPublisher();
        var rule = new FaultRule("f1", FaultKind.Delay) { Command = "presence", DelayMilliseconds = 1 };
        var server = NewServer();
        var clock = new ManualTimeProvider();
        var juliet = await OpenAsync(server, "juliet", "balcony", faults: new ScriptedFaultPlan(rule), events: events, clock: clock);

        juliet.Connection.Send("<presence/>");
        await WaitUntilAsync(() => events.Events.Count == 1);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await SendAsync(juliet, string.Empty);

        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "presence"), events.Events.Single());
        await CloseAsync(juliet);
    }

    [TestMethod]
    public async Task AnswerAsync_StanzaUnderADropRule_EndsTheConnection()
    {
        var events = new RecordingSurlEventPublisher();
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony", faults: new ScriptedFaultPlan(StanzaRule(FaultKind.Drop)), events: events);

        juliet.Connection.Send(ChatToRomeo);
        await juliet.Serving;

        Assert.AreEqual(FaultKind.Drop, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
    }

    [TestMethod]
    [DataRow(FaultKind.Error, "teapot", "its reply does not fit the xmpp error shape")]
    [DataRow(FaultKind.Error, null, "its reply does not fit the xmpp error shape")]
    [DataRow(FaultKind.RefuseLogin, null, "a RefuseLogin rule is not applied at a stanza")]
    public async Task AnswerAsync_StanzaUnderARuleThatDoesNotFit_IsServedAndNoted(FaultKind kind, string? reply, string why)
    {
        var events = new RecordingSurlEventPublisher();
        var server = NewServer();
        var juliet = await OpenAsync(server, "juliet", "balcony", faults: new ScriptedFaultPlan(StanzaRule(kind, reply)), events: events);
        var romeo = await OpenAsync(server, "romeo", "orchard");

        await SendAsync(juliet, ChatToRomeo);

        StringAssert.Contains(Received(romeo), "<body>Hi</body>");
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(juliet.Log.Notes.ToList(), $"Fault rule f1 not applied to message: {why}.");
        await CloseAsync(juliet, romeo);
    }

    [TestMethod]
    [DataRow("bad-request", "modify")]
    [DataRow("forbidden", "auth")]
    [DataRow("service-unavailable", "cancel")]
    [DataRow("resource-constraint", "wait")]
    [DataRow("nonsense", null)]
    public void TypeOf_Reply_IsTheTypeRfc6120Gives(string reply, string? type) => Assert.AreEqual(type, XmppStanzaFaults.TypeOf(reply));

    // Accepts any user over PLAIN in plain text, as the account the message names.
    private static XmppServer NewServer() => new(new XmppServerOptions
    {
        RandomSource = new CountingRandomSource(),
        AuthenticationPolicy = new UnitTestXmppSaslPolicy(["PLAIN"], ["PLAIN"])
        {
            Answer = message =>
            {
                var user = Encoding.UTF8.GetString(message!).Split('\0')[1];
                return new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, user, new CheckedLogin("PLAIN", user, true));
            },
        },
    });

    private async Task<TestStream> OpenAsync(
        XmppServer server, string user, string resource, ExchangeLimits? limits = null, IFaultPlan? faults = null, ISurlEventPublisher? events = null, TimeProvider? clock = null)
    {
        var connection = new UnitTestXmppClientConnection();
        var log = new RecordingExchangeLog();
        var context = new ExchangeContext(
            1,
            new ListenUrl("xmpp", Domain, 5222).WithBoundPort(5222),
            new IPEndPoint(IPAddress.Loopback, 5222),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            clock ?? new ManualTimeProvider(),
            TestContext.CancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
            Faults = faults ?? EmptyFaultPlan.Instance,
            Events = events ?? DiscardingSurlEventPublisher.Instance,
        };
        var serving = server.ServeAsync(connection, context);
        connection.Send(ClientHeader + Auth("PLAIN", Base64($"\0{user}\0pencil")));
        await WaitUntilAsync(() => connection.Written.Contains(Success, StringComparison.Ordinal));
        connection.Send(ClientHeader + $"<iq id='b1' type='set'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><resource>{resource}</resource></bind></iq>");
        await WaitUntilAsync(() => connection.Written.EndsWith("</jid></bind></iq>", StringComparison.Ordinal));

        return new TestStream(connection, serving, log, $"{user}@{Domain}/{resource}") { Mark = connection.Written.Length };
    }

    // Sends a stanza and the fence, and returns what the stream was sent between them.
    private async Task<string> SendAsync(TestStream stream, string stanza)
    {
        var start = stream.Connection.Written.Length;
        var fence = $"fence{++stream.Fences}";
        var fenceAnswer = $"<iq type='result' from='{Domain}' to='{stream.FullJid}' id='{fence}'/>";
        stream.Connection.Send(stanza + $"<iq type='get' to='{Domain}' id='{fence}'><ping xmlns='urn:xmpp:ping'/></iq>");
        await WaitUntilAsync(() => stream.Connection.Written.IndexOf(fenceAnswer, start, StringComparison.Ordinal) >= 0);

        var written = stream.Connection.Written;
        var end = written.IndexOf(fenceAnswer, start, StringComparison.Ordinal);
        stream.Mark = end + fenceAnswer.Length;
        return written[start..end];
    }

    // What the stream was sent since it was last read, its own fences' answers left out.
    private static string Received(TestStream stream)
    {
        var written = stream.Connection.Written;
        var received = written[stream.Mark..];
        stream.Mark = written.Length;
        return received;
    }

    private static async Task CloseAsync(params TestStream[] streams)
    {
        foreach (var stream in streams)
        {
            stream.Connection.Close();
            await stream.Serving;
        }
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The server did not reach the expected state in time.");
            }

            await Task.Delay(5, TestContext.CancellationToken);
        }
    }

    private sealed class TestStream(UnitTestXmppClientConnection connection, Task serving, RecordingExchangeLog log, string fullJid)
    {
        public UnitTestXmppClientConnection Connection { get; } = connection;

        public Task Serving { get; } = serving;

        public RecordingExchangeLog Log { get; } = log;

        public string FullJid { get; } = fullJid;

        public int Mark { get; set; }

        public int Fences { get; set; }
    }
}
