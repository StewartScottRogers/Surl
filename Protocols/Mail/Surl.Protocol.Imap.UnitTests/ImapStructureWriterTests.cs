using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 4: <c>ENVELOPE</c>, <c>BODY</c> and <c>BODYSTRUCTURE</c> as RFC 3501 section
/// 7.4.2 lays them out.
/// </summary>
[TestClass]
public sealed class ImapStructureWriterTests
{
    [TestMethod]
    public void WriteEnvelope_MultipartMessage_WritesEveryFieldAndDefaultsSenderAndReplyToToFrom()
    {
        Assert.AreEqual(MessageStructureSamples.MultipartEnvelope, Envelope(MessageStructureSamples.Multipart));
    }

    [TestMethod]
    public void WriteEnvelope_EveryAddressFieldGiven_WritesEachOwnList()
    {
        var envelope = Envelope("From: a@x\r\nSender: s@x\r\nReply-To: r@x\r\nTo: t@x\r\nCc: c@x\r\nBcc: b@x\r\nIn-Reply-To: <p@x>\r\n\r\n");

        Assert.AreEqual(
            "(NIL NIL ((NIL NIL \"a\" \"x\")) ((NIL NIL \"s\" \"x\")) ((NIL NIL \"r\" \"x\")) ((NIL NIL \"t\" \"x\")) ((NIL NIL \"c\" \"x\")) ((NIL NIL \"b\" \"x\")) \"<p@x>\" NIL)",
            envelope);
    }

    [TestMethod]
    public void WriteEnvelope_EmptyMessage_IsAllNil()
    {
        Assert.AreEqual("(NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL)", Envelope(string.Empty));
    }

    [TestMethod]
    public void WriteBodyStructure_MultipartMessage_WritesThePartsAndTheExtensionData()
    {
        Assert.AreEqual(MessageStructureSamples.MultipartBodyStructure, Structure(MessageStructureSamples.Multipart, isExtended: true));
    }

    [TestMethod]
    public void WriteBodyStructure_BodyOfAMultipartMessage_LeavesTheExtensionDataOut()
    {
        Assert.AreEqual(MessageStructureSamples.MultipartBody, Structure(MessageStructureSamples.Multipart, isExtended: false));
    }

    [TestMethod]
    [DataRow("Content-Type: image/png; name=a.png\r\nContent-ID: <i@x>\r\nContent-Description: pic\r\nContent-Transfer-Encoding: base64\r\nContent-MD5: abc=\r\nContent-Disposition: inline\r\nContent-Language: en\r\nContent-Location: http://x/a.png\r\n\r\nAAAA",
        "(\"IMAGE\" \"PNG\" (\"NAME\" \"a.png\") \"<i@x>\" \"pic\" \"BASE64\" 4 \"abc=\" (\"inline\" NIL) \"en\" \"http://x/a.png\")")]
    [DataRow("Content-Type: text/plain\r\n\r\n", "(\"TEXT\" \"PLAIN\" NIL NIL NIL \"7BIT\" 0 0 NIL NIL NIL NIL)")]
    [DataRow("\r\none\r\ntwo\r\n", "(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"US-ASCII\") NIL NIL \"7BIT\" 10 2 NIL NIL NIL NIL)")]
    public void WriteBodyStructure_SinglePart_WritesItsFields(string message, string expected)
    {
        Assert.AreEqual(expected, Structure(message, isExtended: true));
    }

    private static string Envelope(string message)
    {
        var writer = new ImapDataWriter();
        ImapStructureWriter.WriteEnvelope(writer, ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(message)));
        return Encoding.UTF8.GetString(writer.ToArray());
    }

    private static string Structure(string message, bool isExtended)
    {
        var writer = new ImapDataWriter();
        ImapStructureWriter.WriteBodyStructure(writer, ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(message)), isExtended);
        return Encoding.UTF8.GetString(writer.ToArray());
    }
}
