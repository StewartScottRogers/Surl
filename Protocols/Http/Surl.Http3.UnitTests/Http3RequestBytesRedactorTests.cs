using System.Buffers;
using System.Text;
using Surl.Hpack;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// The HTTP/3 redactor of the decrypted-bytes notes withholds <c>authorization</c> and
/// <c>proxy-authorization</c> from request header blocks and decoded header text (ADR-0182, BL-1424).
/// </summary>
[TestClass]
public sealed class Http3RequestBytesRedactorTests
{
    private static readonly byte[] Stars = "****"u8.ToArray();

    private static byte[] Varint(ulong value)
    {
        var bytes = new byte[QuicVariableLengthInteger.EncodedLength(value)];
        QuicVariableLengthInteger.Write(bytes, value);

        return bytes;
    }

    private static byte[] Frame(ulong type, byte[] payload) => Join(Varint(type), Varint((ulong)payload.Length), payload);

    private static byte[] Section(HpackStringCoding coding, params (string Name, string Value)[] fields) =>
        new QpackEncoder(coding).EncodeFieldSection(fields.Select(field => new QpackFieldLine(field.Name, field.Value)));

    private static byte[] Join(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private static byte[] Redact(byte[] received)
    {
        var redactor = new Http3RequestBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(received, output);
        redactor.Flush(output);
        redactor.ObserveSent([1, 2, 3]);

        return output.WrittenSpan.ToArray();
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [TestMethod]
    [DataRow(HpackStringCoding.Raw)]
    [DataRow(HpackStringCoding.Huffman)]
    public void Redact_HeadersFrameWithAuthorization_WritesTheHeaderAndStarsForThePayload(HpackStringCoding coding)
    {
        var frame = Frame(
            Http3FrameType.Headers,
            Section(coding, (":method", "GET"), ("authorization", "Bearer tok-h3-secret")));

        var logged = Redact(frame);

        CollectionAssert.AreEqual(Join(frame[..2], Stars), logged);
        Assert.IsFalse(Text(logged).Contains("tok-h3-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_ProxyAuthorizationWithALiteralNameAndALongPayload_IsWithheldToo()
    {
        var frame = Frame(
            Http3FrameType.Headers,
            Section(HpackStringCoding.Raw, (":method", "GET"), ("proxy-authorization", new string('p', 300))));

        var logged = Redact(frame);

        CollectionAssert.AreEqual(Join(frame[..3], Stars), logged);
    }

    [TestMethod]
    public void Redact_HeadersWithoutASecret_AreLoggedByteForByte()
    {
        var frame = Join(
            Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, (":method", "GET"), ("x-trace", "abc"))),
            Frame(Http3FrameType.Data, "body"u8.ToArray()),
            Frame(0x21, [9]));

        CollectionAssert.AreEqual(frame, Redact(frame));
    }

    [TestMethod]
    public void Redact_ASecretInTheSecondFrame_WithholdsOnlyThatPayload()
    {
        var plain = Frame(Http3FrameType.Data, "body"u8.ToArray());
        var secret = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("authorization", "x")));

        CollectionAssert.AreEqual(Join(plain, secret[..2], Stars), Redact(Join(plain, secret)));
    }

    [TestMethod]
    public void Redact_HeadersPayloadThatRunsPastTheChunk_IsWithheldFromItsStart()
    {
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("authorization", "Bearer abcdef")));

        CollectionAssert.AreEqual(Join(frame[..2], Stars), Redact(frame[..^3]));
    }

    [TestMethod]
    public void Redact_HeadersPayloadThatDoesNotDecode_IsWithheld()
    {
        var frame = Frame(Http3FrameType.Headers, [0xFF, 0xFF, 0xFF]);

        CollectionAssert.AreEqual(Join(frame[..2], Stars), Redact(frame));
    }

    [TestMethod]
    public void Redact_ADataFrameThatRunsPastTheChunk_IsLoggedAsReceived()
    {
        var frame = Frame(Http3FrameType.Data, "0123456789"u8.ToArray());

        CollectionAssert.AreEqual(frame[..6], Redact(frame[..6]));
    }

    [TestMethod]
    public void Redact_ATruncatedFrameHeader_IsLoggedAsReceived()
    {
        CollectionAssert.AreEqual(new byte[] { 0x40 }, Redact([0x40]));
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x40 }, Redact([0x00, 0x40]));
    }

    [TestMethod]
    public void Redact_NoBytes_WritesNothing() => Assert.AreEqual(0, Redact([]).Length);

    [TestMethod]
    public void Redact_DecodedHeaderText_StarsAuthorizationAndProxyAuthorizationValues()
    {
        var text = ":method: GET\r\n:path: /x\r\nAuthorization: Bearer tok-text\r\nproxy-authorization: Basic abc\r\nx-note: a: b\r\n\r\n";

        var logged = Text(Redact(Encoding.UTF8.GetBytes(text)));

        Assert.AreEqual(
            ":method: GET\r\n:path: /x\r\nAuthorization: ****\r\nproxy-authorization: ****\r\nx-note: a: b\r\n\r\n",
            logged);
    }

    [TestMethod]
    public void Redact_DecodedHeaderTextWithoutALineEndOrColon_IsKept()
    {
        Assert.AreEqual(":a: b\r\nnocolon", Text(Redact(Encoding.UTF8.GetBytes(":a: b\r\nnocolon"))));
        Assert.AreEqual(":authorization: x", Text(Redact(Encoding.UTF8.GetBytes(":authorization: x"))));
        Assert.AreEqual(":x", Text(Redact(Encoding.UTF8.GetBytes(":x"))));
    }

    [TestMethod]
    public void Redact_AndFlush_RejectANullOutput()
    {
        var redactor = new Http3RequestBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
