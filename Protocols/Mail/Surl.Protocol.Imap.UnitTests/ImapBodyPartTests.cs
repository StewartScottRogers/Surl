using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 4: messages read from their bytes by RFC 5322 and RFC 2045/2046 structure,
/// and the bytes each <c>BODY[&lt;section&gt;]</c> names in them.
/// </summary>
[TestClass]
public sealed class ImapBodyPartTests
{
    [TestMethod]
    [DataRow("", MessageStructureSamples.Multipart)]
    [DataRow("HEADER.FIELDS (SUBJECT FROM)", "From: \"Ann Example\" <ann@example.com>\r\nSubject: Report\r\n\r\n")]
    [DataRow("TEXT", "preamble\r\n--b1\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nhello\r\n--b1\r\nContent-Type: message/rfc822\r\nContent-Disposition: attachment; filename=\"inner.eml\"\r\n\r\n" + MessageStructureSamples.Inner + "\r\n--b1--\r\nepilogue\r\n")]
    [DataRow("1", "hello")]
    [DataRow("1.MIME", "Content-Type: text/plain; charset=utf-8\r\n\r\n")]
    [DataRow("1.TEXT", "")]
    [DataRow("1.1", "")]
    [DataRow("2", MessageStructureSamples.Inner)]
    [DataRow("2.HEADER", "Subject: inner\r\nContent-Type: multipart/alternative; boundary=b2\r\n\r\n")]
    [DataRow("2.HEADER.FIELDS.NOT (CONTENT-TYPE)", "Subject: inner\r\n\r\n")]
    [DataRow("2.TEXT", "--b2\r\nContent-Type: text/plain\r\n\r\ninner text\r\n--b2   \r\nContent-Type: text/html\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n<p>x</p>\r\n--b2--")]
    [DataRow("2.MIME", "Content-Type: message/rfc822\r\nContent-Disposition: attachment; filename=\"inner.eml\"\r\n\r\n")]
    [DataRow("2.1", "inner text")]
    [DataRow("2.2", "<p>x</p>")]
    [DataRow("2.2.MIME", "Content-Type: text/html\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n")]
    [DataRow("2.2.1", "")]
    [DataRow("2.3", "")]
    [DataRow("3", "")]
    public void Content_SectionOfAMultipartMessage_IsThePartTheSectionNames(string specifier, string expected)
    {
        var message = ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(MessageStructureSamples.Multipart));

        Assert.AreEqual(expected, Encoding.UTF8.GetString(Section(specifier).Content(message).Span));
    }

    [TestMethod]
    [DataRow("Subject: x", "HEADER", "Subject: x")]
    [DataRow("Subject: x", "TEXT", "")]
    [DataRow("Subject: x", "HEADER.FIELDS (SUBJECT)", "Subject: x\r\n\r\n")]
    [DataRow("", "HEADER", "")]
    [DataRow("\r\nbody", "HEADER", "\r\n")]
    [DataRow("\r\nbody", "TEXT", "body")]
    [DataRow("Subject: a\n\nbody\n", "HEADER.FIELDS (SUBJECT)", "Subject: a\n\r\n")]
    [DataRow("Subject: a\r\n b\r\nX: y\r\n\r\n", "HEADER.FIELDS (SUBJECT)", "Subject: a\r\n b\r\n\r\n")]
    [DataRow(" lead: 1\r\njunk\r\nX: y\r\n\r\n", "HEADER.FIELDS.NOT (X)", " lead: 1\r\njunk\r\n\r\n")]
    [DataRow("Subject: 1\r\n\r\nhello\r\n", "1", "hello\r\n")]
    [DataRow("Subject: 1\r\n\r\nhello\r\n", "1.MIME", "Subject: 1\r\n\r\n")]
    [DataRow("Content-Type: message/rfc822\r\n\r\nSubject: in\r\n\r\nbody", "1.HEADER", "Subject: in\r\n\r\n")]
    [DataRow("Content-Type: message/rfc822\r\n\r\nSubject: in\r\n\r\nbody", "1.1", "body")]
    [DataRow("Content-Type: multipart/mixed; boundary=a\r\n\r\n--a\r\nContent-Type: multipart/alternative; boundary=b\r\n\r\n--b\r\n\r\none\r\n--b\r\n\r\ntwo\r\n--b--\r\n--a--", "1.2", "two")]
    public void Content_SectionOfASimpleMessage_IsThePartTheSectionNames(string message, string specifier, string expected)
    {
        var part = ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(message));

        Assert.AreEqual(expected, Encoding.UTF8.GetString(Section(specifier).Content(part).Span));
    }

    [TestMethod]
    public void ReadMessage_FoldedField_UnfoldsItsValueAndKeepsItsBytes()
    {
        var message = ImapBodyPart.ReadMessage("Subject:  a\r\n\tb \r\nNo colon\r\n\r\n"u8.ToArray());

        Assert.HasCount(2, message.Fields);
        Assert.AreEqual("Subject", message.Fields[0].Name);
        Assert.AreEqual("a\tb", message.Fields[0].Text);
        Assert.AreEqual("Subject:  a\r\n\tb \r\n", Encoding.UTF8.GetString(message.Fields[0].Raw.Span));
        Assert.AreEqual(string.Empty, message.Fields[1].Name);
        Assert.AreEqual(string.Empty, message.Fields[1].Text);
    }

    [TestMethod]
    [DataRow("Content-Type: multipart/mixed\r\n\r\n--b\r\nx\r\n--b--\r\n")]
    [DataRow("Content-Type: multipart/mixed; boundary=\"\"\r\n\r\n--\r\nx\r\n")]
    [DataRow("Content-Type: multipart/mixed; boundary=b\r\n\r\nno delimiter\r\n")]
    [DataRow("Content-Type: multipart/mixed; boundary=b\r\n\r\n--bx\r\n--b-- \r\n")]
    [DataRow("Content-Type: multipart/mixed; boundary=b\r\n\r\n--b--\r\n--b\r\nlate\r\n")]
    public void ReadMessage_MultipartThatDoesNotParse_IsOneTextPlainPart(string text)
    {
        var message = ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(text));

        Assert.AreSame(ImapContentType.TextPlain, message.Type);
        Assert.IsEmpty(message.Parts);
    }

    [TestMethod]
    public void ReadMessage_MultipartWithoutItsCloseDelimiter_EndsItsLastPartAtTheBodysEnd()
    {
        var message = ImapBodyPart.ReadMessage("Content-Type: multipart/mixed; boundary=b\r\n\r\n--b\r\n\r\none\r\n--b\r\n\r\ntwo"u8.ToArray());

        Assert.HasCount(2, message.Parts);
        Assert.AreEqual("one", Encoding.UTF8.GetString(message.Parts[0].Body.Span));
        Assert.AreEqual("two", Encoding.UTF8.GetString(message.Parts[1].Body.Span));
    }

    [TestMethod]
    public void ReadMessage_EmptyPartBetweenDelimiters_IsAnEmptyPart()
    {
        var message = ImapBodyPart.ReadMessage("Content-Type: multipart/mixed; boundary=b\r\n\r\n--b\r\n--b\r\n\r\nx\r\n--b--"u8.ToArray());

        Assert.HasCount(2, message.Parts);
        Assert.IsTrue(message.Parts[0].Entity.IsEmpty);
    }

    [TestMethod]
    public void ReadMessage_DigestPartWithoutAContentType_IsAMessage()
    {
        var message = ImapBodyPart.ReadMessage("Content-Type: multipart/digest; boundary=b\r\n\r\n--b\r\n\r\nSubject: in\r\n\r\nx\r\n--b--"u8.ToArray());

        Assert.AreSame(ImapContentType.MessageRfc822, message.Parts[0].Type);
        Assert.AreEqual("in", message.Parts[0].Message!.Fields[0].Text);
    }

    [TestMethod]
    public void ReadMessage_NestingPastTheDepthLimit_StopsReadingParts()
    {
        var text = string.Concat(Enumerable.Repeat("Content-Type: message/rfc822\r\n\r\n", ImapBodyPart.MaxDepth + 5))
            + "Content-Type: multipart/mixed; boundary=b\r\n\r\n--b\r\nx\r\n--b--";

        var part = ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(text));

        var depth = 0;
        for (; part.Message is { } held; part = held)
        {
            depth++;
        }

        Assert.AreEqual(ImapBodyPart.MaxDepth, depth);
        Assert.IsTrue(part.Type.IsMessage);
    }

    [TestMethod]
    public void ReadMessage_MultipartAtTheDepthLimit_IsReadAsTextPlain()
    {
        var text = string.Concat(Enumerable.Repeat("Content-Type: message/rfc822\r\n\r\n", ImapBodyPart.MaxDepth))
            + "Content-Type: multipart/mixed; boundary=b\r\n\r\n--b\r\nx\r\n--b--";

        var part = ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(text));
        while (part.Message is { } held)
        {
            part = held;
        }

        Assert.AreSame(ImapContentType.TextPlain, part.Type);
    }

    [TestMethod]
    [DataRow("Content-Type: text")]
    [DataRow("Content-Type: /plain")]
    [DataRow("Content-Type: text/")]
    public void ReadMessage_ContentTypeThatIsNotTypeAndSubtype_IsTextPlain(string field)
    {
        var message = ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(field + "\r\n\r\nx"));

        Assert.AreSame(ImapContentType.TextPlain, message.Type);
    }

    [TestMethod]
    public void ReadMessage_ContentTypeWithParametersAndComments_ReadsEachParameter()
    {
        var message = ImapBodyPart.ReadMessage("Content-Type: Text/HTML (web) ; Charset = \"utf-8\" ; bare ; =x ; name=\"a;b\\\"c\"\r\n\r\n"u8.ToArray());

        Assert.AreEqual("TEXT", message.Type.Type);
        Assert.AreEqual("HTML", message.Type.Subtype);
        Assert.AreEqual("utf-8", message.Type.Parameter("CHARSET"));
        Assert.AreEqual("a;b\"c", message.Type.Parameter("NAME"));
        Assert.IsNull(message.Type.Parameter("BARE"));
        Assert.HasCount(2, message.Type.Parameters);
    }

    private static ImapSection Section(string specifier)
    {
        var arguments = new ImapArguments(new ImapCommandText([Encoding.ASCII.GetBytes(specifier + "]")], []));
        return ImapSection.Read(arguments) ?? throw new AssertFailedException($"{specifier} is no section.");
    }
}
