using System.Net;
using System.Text;
using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The incremental XML stream reader (BL-536; ADR-0114 decisions 6 and 7), every stream written
/// by hand from RFC 6120 sections 4, 9 and 11.
/// </summary>
[TestClass]
public sealed class XmppStreamReaderTests
{
    private const string Header =
        "<?xml version='1.0'?><stream:stream to='im.example.com' version='1.0' xml:lang='en' " +
        "xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>";

    private const string Message =
        "<message from='juliet@im.example.com/balcony' id='ju2ba41c' to='romeo@example.net' type='chat' xml:lang='en'>" +
        "<body>Art thou not Romeo, and a Montague?</body></message>";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadHeaderAsync_Rfc6120Header_ReturnsItsAttributes()
    {
        var reader = Reader(Header);

        var header = await reader.ReadHeaderAsync(TestContext.CancellationToken);

        Assert.AreEqual(new XmppStreamHeader("im.example.com", "1.0", "en", IsEmptyElement: false), header);
    }

    [TestMethod]
    public async Task ReadHeaderAsync_EmptyHeaderAfterWhitespaceWithoutDeclaration_IsReadAsEmpty()
    {
        var reader = Reader(" \n<stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'/>");

        var header = await reader.ReadHeaderAsync(TestContext.CancellationToken);

        Assert.AreEqual(new XmppStreamHeader(null, null, null, IsEmptyElement: true), header);
    }

    [TestMethod]
    [DataRow("<?xml version='1.0' encoding='UTF-8'?>")]
    [DataRow("<?xml version='1.0' encoding='utf-8'?>")]
    public async Task ReadHeaderAsync_DeclarationNamingUtf8_IsAccepted(string declaration)
    {
        var reader = Reader(declaration + Header[21..]);

        Assert.IsNotNull(await reader.ReadHeaderAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<?xml version='1.0'?><stream:str")]
    public async Task ReadHeaderAsync_InputEndsBeforeTheHeader_ReturnsNull(string client)
    {
        var reader = Reader(client);

        Assert.IsNull(await reader.ReadHeaderAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("<stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.InvalidNamespace)]
    [DataRow("<stream:stream xmlns='jabber:server' xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.InvalidNamespace)]
    [DataRow("<stream:stream xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.InvalidNamespace)]
    [DataRow("<message xmlns='jabber:client'/>", XmppStreamErrors.NotWellFormed)]
    [DataRow("hello", XmppStreamErrors.NotWellFormed)]
    [DataRow("<![CDATA[x]]>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' to='a' to='b'>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<x:stream>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<!DOCTYPE stream><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<!-- hi --><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<?pi x?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<?xml version='1.0' encoding='ISO-8859-1'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.UnsupportedEncoding)]
    [DataRow("<?xml version='1.0' encoding='UTF-16'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", XmppStreamErrors.UnsupportedEncoding)]
    public async Task ReadHeaderAsync_RefusedHeader_ThrowsItsStreamError(string client, string condition)
    {
        var reader = Reader(client);

        var error = await Assert.ThrowsExactlyAsync<XmppStreamErrorException>(() => reader.ReadHeaderAsync(TestContext.CancellationToken));

        Assert.AreEqual(condition, error.Condition);
    }

    [TestMethod]
    public async Task ReadHeaderAsync_BytesThatAreNotUtf8_IsUnsupportedEncoding()
    {
        var connection = new InMemoryConnection([new byte[] { 0xFF, 0xFE, (byte)'<', 0 }]);
        var reader = new XmppStreamReader(connection, ExchangeLimits.Default.MaxMessageBytes);

        var error = await Assert.ThrowsExactlyAsync<XmppStreamErrorException>(() => reader.ReadHeaderAsync(TestContext.CancellationToken));

        Assert.AreEqual(XmppStreamErrors.UnsupportedEncoding, error.Condition);
    }

    [TestMethod]
    public async Task ReadHeaderAsync_HeaderOverTheSizeBound_IsPolicyViolation()
    {
        var reader = Reader(1000, "<stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' to='" + new string('a', 5000) + "'>");

        var error = await Assert.ThrowsExactlyAsync<XmppStreamErrorException>(() => reader.ReadHeaderAsync(TestContext.CancellationToken));

        Assert.AreEqual(XmppStreamErrors.PolicyViolation, error.Condition);
    }

    [TestMethod]
    public async Task ReadElementAsync_ElementSplitAcrossReads_IsDeliveredWhole()
    {
        var chunks = new List<string> { Header };
        chunks.AddRange(Message.Chunk(7).Select(chunk => new string(chunk)));
        var reader = await OpenedReaderAsync([.. chunks]);

        var element = await reader.ReadElementAsync(TestContext.CancellationToken);

        Assert.AreEqual(
            "<message from=\"juliet@im.example.com/balcony\" id=\"ju2ba41c\" to=\"romeo@example.net\" type=\"chat\" xml:lang=\"en\" xmlns=\"jabber:client\">" +
            "<body>Art thou not Romeo, and a Montague?</body></message>",
            element!.ToString(SaveOptions.DisableFormatting));
    }

    [TestMethod]
    public async Task ReadElementAsync_TwoElementsInOneRead_AreDeliveredInOrder()
    {
        var reader = await OpenedReaderAsync(Header, "<presence/><message id='m1'><body>hi</body></message>");

        var first = await reader.ReadElementAsync(TestContext.CancellationToken);
        var second = await reader.ReadElementAsync(TestContext.CancellationToken);

        Assert.AreEqual(XName.Get("presence", XmppNamespaces.Client), first!.Name);
        Assert.AreEqual(XName.Get("message", XmppNamespaces.Client), second!.Name);
        Assert.AreEqual("m1", (string?)second.Attribute("id"));
        Assert.AreEqual("hi", second.Value);
    }

    [TestMethod]
    public async Task ReadElementAsync_ElementBeforeTheClientSendsMore_IsDeliveredWithoutWaiting()
    {
        var connection = new UnitTestXmppClientConnection();
        connection.Send(Header + "<presence/>");
        var reader = new XmppStreamReader(connection, ExchangeLimits.Default.MaxMessageBytes);
        await reader.ReadHeaderAsync(TestContext.CancellationToken);

        var element = await reader.ReadElementAsync(TestContext.CancellationToken);

        Assert.AreEqual("presence", element!.Name.LocalName);
    }

    [TestMethod]
    public async Task ReadElementAsync_WhitespaceKeepalivesThenEndTag_ReturnsNullAsAClientClose()
    {
        var reader = await OpenedReaderAsync(Header, " ", "\n\t ", "</stream:stream>");

        var element = await reader.ReadElementAsync(TestContext.CancellationToken);

        Assert.IsNull(element);
        Assert.IsTrue(reader.ClientClosedStream);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<message><body>cut o")]
    [DataRow("<message><body>&lt;&en")]
    public async Task ReadElementAsync_InputEnds_ReturnsNullAsNoClientClose(string tail)
    {
        var reader = await OpenedReaderAsync(Header, tail);

        var element = await reader.ReadElementAsync(TestContext.CancellationToken);

        Assert.IsNull(element);
        Assert.IsFalse(reader.ClientClosedStream);
    }

    [TestMethod]
    public async Task ReadElementAsync_NamespaceDeclarationsCdataAndPrefixedAttributes_AreKept()
    {
        var reader = await OpenedReaderAsync(Header, "<iq type='get' id='1' xml:lang='fr'><query xmlns='http://jabber.org/protocol/disco#info' xmlns:x='urn:x' x:a='b'><![CDATA[<raw>]]> &amp; &#65;</query></iq>");

        var element = await reader.ReadElementAsync(TestContext.CancellationToken);

        var query = element!.Elements().Single();
        Assert.AreEqual(XName.Get("query", "http://jabber.org/protocol/disco#info"), query.Name);
        Assert.AreEqual("b", (string?)query.Attribute(XName.Get("a", "urn:x")));
        Assert.AreEqual("urn:x", (string?)query.Attribute(XNamespace.Xmlns + "x"));
        Assert.AreEqual("http://jabber.org/protocol/disco#info", (string?)query.Attribute("xmlns"));
        Assert.AreEqual("fr", (string?)element.Attribute(XNamespace.Xml + "lang"));
        Assert.AreEqual("<raw> & A", query.Value);
    }

    [TestMethod]
    public async Task ReadElementAsync_PreservedWhitespace_IsAKeepaliveBetweenElementsAndKeptInsideThem()
    {
        var reader = await OpenedReaderAsync(
            "<stream:stream xml:space='preserve' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>",
            " \n ",
            "<message><body> </body><x xml:space='default'>\n</x>\n</message>",
            " </stream:stream>");

        var element = await reader.ReadElementAsync(TestContext.CancellationToken);
        var end = await reader.ReadElementAsync(TestContext.CancellationToken);

        var texts = element!.DescendantNodes().OfType<XText>().Select(text => text.Value).ToArray();
        CollectionAssert.AreEqual(new[] { " ", "\n", "\n" }, texts);
        Assert.IsNull(end);
        Assert.IsTrue(reader.ClientClosedStream);
    }

    [TestMethod]
    [DataRow("<message><!-- c --></message>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<message><?pi x?></message>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<!-- c -->", XmppStreamErrors.RestrictedXml)]
    [DataRow("<message><body>&nbsp;</body></message>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<message><body>&lt;&ent;</body></message>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<!DOCTYPE x>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<message><body></message>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<message to='a' to='b'/>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<y:message/>", XmppStreamErrors.NotWellFormed)]
    [DataRow("hello", XmppStreamErrors.PolicyViolation)]
    [DataRow("<![CDATA[hello]]>", XmppStreamErrors.PolicyViolation)]
    public async Task ReadElementAsync_RefusedContent_ThrowsItsStreamError(string client, string condition)
    {
        var reader = await OpenedReaderAsync(Header, client);

        var error = await Assert.ThrowsExactlyAsync<XmppStreamErrorException>(() => reader.ReadElementAsync(TestContext.CancellationToken));

        Assert.AreEqual(condition, error.Condition);
    }

    [TestMethod]
    public async Task ReadElementAsync_ElementOverTheSizeBound_IsPolicyViolation()
    {
        var body = new string('x', 20000);
        var reader = await OpenedReaderAsync(10000, Header, "<message><body>", body, "</body></message>");

        var error = await Assert.ThrowsExactlyAsync<XmppStreamErrorException>(() => reader.ReadElementAsync(TestContext.CancellationToken));

        Assert.AreEqual(XmppStreamErrors.PolicyViolation, error.Condition);
    }

    [TestMethod]
    public async Task ReadElementAsync_ManyElementsTogetherOverTheSizeBound_AreEachMeasuredAlone()
    {
        var message = "<message><body>" + new string('x', 3000) + "</body></message>";
        var reader = await OpenedReaderAsync(10000, Header, message, message, message, message, message);

        for (var count = 0; count < 5; count++)
        {
            Assert.IsNotNull(await reader.ReadElementAsync(TestContext.CancellationToken));
        }
    }

    // The bytes before a first-level start tag is reported count toward the element before it, so
    // a value split across reads around that start tag passes the stream's count and is caught as
    // a value.
    [TestMethod]
    [DataRow("<message a='", "'/>")]
    [DataRow("<message><body>", "</body></message>")]
    public async Task ReadElementAsync_ValueOverTheSizeBoundAcrossTheStartTag_IsPolicyViolation(string start, string end)
    {
        var half = new string('v', 250);
        var reader = await OpenedReaderAsync(300, Header, "<presence/>" + start + half, half + end);
        Assert.IsNotNull(await reader.ReadElementAsync(TestContext.CancellationToken));

        var error = await Assert.ThrowsExactlyAsync<XmppStreamErrorException>(() => reader.ReadElementAsync(TestContext.CancellationToken));

        Assert.AreEqual(XmppStreamErrors.PolicyViolation, error.Condition);
        StringAssert.StartsWith(error.Message, "A value passed", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(32, false)]
    [DataRow(33, true)]
    public async Task ReadElementAsync_Nesting_IsRefusedPast32Levels(int levels, bool refused)
    {
        var element = string.Concat(Enumerable.Repeat("<a>", levels)) + string.Concat(Enumerable.Repeat("</a>", levels));
        var reader = await OpenedReaderAsync(Header, element);

        var read = ReadOrErrorAsync(reader);

        Assert.AreEqual(refused ? XmppStreamErrors.PolicyViolation : "a", await read);
    }

    [TestMethod]
    [DataRow(64, false)]
    [DataRow(65, true)]
    public async Task ReadElementAsync_Attributes_AreRefusedPast64(int attributes, bool refused)
    {
        var element = "<message><a " + string.Concat(Enumerable.Range(0, attributes).Select(index => $"n{index}='v' ")) + "/></message>";
        var reader = await OpenedReaderAsync(Header, element);

        var read = ReadOrErrorAsync(reader);

        Assert.AreEqual(refused ? XmppStreamErrors.PolicyViolation : "message", await read);
    }

    [TestMethod]
    public async Task ReadElementAsync_Cancelled_ThrowsOperationCanceled()
    {
        var connection = new UnitTestXmppClientConnection();
        connection.Send(Header);
        var reader = new XmppStreamReader(connection, ExchangeLimits.Default.MaxMessageBytes);
        await reader.ReadHeaderAsync(TestContext.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => reader.ReadElementAsync(cancellation.Token));
    }

    private async Task<string> ReadOrErrorAsync(XmppStreamReader reader)
    {
        try
        {
            return (await reader.ReadElementAsync(TestContext.CancellationToken))!.Name.LocalName;
        }
        catch (XmppStreamErrorException error)
        {
            return error.Condition;
        }
    }

    private Task<XmppStreamReader> OpenedReaderAsync(params string[] chunks) =>
        OpenedReaderAsync(ExchangeLimits.Default.MaxMessageBytes, chunks);

    private async Task<XmppStreamReader> OpenedReaderAsync(long maxElementBytes, params string[] chunks)
    {
        var reader = Reader(maxElementBytes, chunks);
        Assert.IsNotNull(await reader.ReadHeaderAsync(TestContext.CancellationToken));
        return reader;
    }

    private static XmppStreamReader Reader(params string[] chunks) => Reader(ExchangeLimits.Default.MaxMessageBytes, chunks);

    private static XmppStreamReader Reader(long maxElementBytes, params string[] chunks) => new(
        new InMemoryConnection(chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk)), new IPEndPoint(IPAddress.Loopback, 5222)),
        maxElementBytes);
}
