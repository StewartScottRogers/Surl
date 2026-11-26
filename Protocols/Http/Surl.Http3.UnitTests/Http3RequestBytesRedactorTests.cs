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
    public void Redact_AFurtherSecretFieldName_IsWithheldFromTheFrameAndTheDecodedTextWhateverItsCase()
    {
        var redactor = new Http3RequestBytesRedactor(["X-Secret"]);
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("x-secret", "key-1")));
        var frames = new ArrayBufferWriter<byte>();
        var text = new ArrayBufferWriter<byte>();

        redactor.Redact(frame, frames);
        redactor.Redact(":method: GET\r\nx-secret: key-1\r\nauthorization: Bearer t\r\n"u8, text);

        CollectionAssert.AreEqual(Join(frame[..2], Stars), frames.WrittenSpan.ToArray());
        Assert.AreEqual(":method: GET\r\nx-secret: ****\r\nauthorization: ****\r\n", Text(text.WrittenSpan.ToArray()));
    }

    [TestMethod]
    public void Redact_WithNoFurtherNames_LogsAnApiKeyFieldAsReceived()
    {
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("x-api-key", "key-1")));
        var text = ":method: GET\r\nx-api-key: key-1\r\n"u8.ToArray();

        CollectionAssert.AreEqual(frame, Redact(frame));
        CollectionAssert.AreEqual(text, Redact(text));
    }

    private static byte[] RedactStream(Http3RequestBytesRedactor redactor, long streamId, byte[] received)
    {
        var output = new ArrayBufferWriter<byte>();
        redactor.RedactStream(streamId, received, output);

        return output.WrittenSpan.ToArray();
    }

    private static byte[] RedactStreamInChunks(byte[] received, params int[] cuts)
    {
        var redactor = new Http3RequestBytesRedactor();
        int[] ends = [.. cuts, received.Length];
        var start = 0;
        var logged = new List<byte>();
        foreach (var end in ends)
        {
            logged.AddRange(RedactStream(redactor, 0, received[start..end]));
            start = end;
        }

        return [.. logged];
    }

    private static byte[] SecretHeaders() =>
        Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, (":method", "GET"), ("authorization", "Bearer tok-split-secret")));

    [TestMethod]
    public void RedactStream_ASecretHeadersFrameSplitAnywhereIntoTwoChunks_WritesTheHeaderAndStarsOnly()
    {
        var frame = SecretHeaders();

        for (var cut = 0; cut <= frame.Length; cut++)
        {
            CollectionAssert.AreEqual(Join(frame[..2], Stars), RedactStreamInChunks(frame, cut), $"cut at {cut}");
        }
    }

    [TestMethod]
    public void RedactStream_ASecretHeadersFrameArrivingOneByteAtATime_WritesTheHeaderAndStarsOnly()
    {
        var frame = SecretHeaders();

        var logged = RedactStreamInChunks(frame, [.. Enumerable.Range(1, frame.Length - 1)]);

        CollectionAssert.AreEqual(Join(frame[..2], Stars), logged);
    }

    [TestMethod]
    public void RedactStream_ALongFrameHeaderSplitInsideItsLength_StillWithholdsThePayload()
    {
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("authorization", new string('p', 300))));

        var logged = RedactStreamInChunks(frame, 2, 40, 200);

        CollectionAssert.AreEqual(Join(frame[..3], Stars), logged);
    }

    [TestMethod]
    public void RedactStream_TheFrameAfterASplitSecretFrame_IsReadFromItsOwnStart()
    {
        var secret = SecretHeaders();
        var data = Frame(Http3FrameType.Data, "body"u8.ToArray());

        var logged = RedactStreamInChunks(Join(secret, data, secret), 7, secret.Length + 3);

        CollectionAssert.AreEqual(Join(secret[..2], Stars, data, secret[..2], Stars), logged);
    }

    [TestMethod]
    public void RedactStream_ASplitHeadersFrameWithoutASecret_IsWithheldAllTheSame()
    {
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, (":method", "GET"), ("x-trace", "abcdef")));

        CollectionAssert.AreEqual(Join(frame[..2], Stars), RedactStreamInChunks(frame, 5));
    }

    [TestMethod]
    public void RedactStream_ADataFrameSplitWhereItsPayloadReadsAsHeaders_IsLoggedByteForByte()
    {
        var data = Frame(Http3FrameType.Data, [0x61, 0x01, 0x03, 0xFF, 0xFF, 0xFF]);

        CollectionAssert.AreEqual(data, RedactStreamInChunks(data, 3));
    }

    [TestMethod]
    public void RedactStream_ChunksOfTwoStreamsInterleaved_DoNotDisturbEachOther()
    {
        var redactor = new Http3RequestBytesRedactor();
        var secret = SecretHeaders();
        var data = Frame(Http3FrameType.Data, "0123456789"u8.ToArray());

        var first = RedactStream(redactor, 0, secret[..1]);
        var second = RedactStream(redactor, 4, data[..4]);
        var third = RedactStream(redactor, 0, secret[1..9]);
        var fourth = RedactStream(redactor, 4, Join(data[4..], secret[..6]));
        var fifth = RedactStream(redactor, 0, secret[9..]);
        var sixth = RedactStream(redactor, 4, secret[6..]);

        CollectionAssert.AreEqual(Join(secret[..2], Stars), Join(first, third, fifth));
        CollectionAssert.AreEqual(Join(data, secret[..2], Stars), Join(second, fourth, sixth));
    }

    [TestMethod]
    public void RedactStream_AChunkStartingWithAColon_IsReadAsFrameBytesNotAsHeaderText()
    {
        var data = Frame(Http3FrameType.Data, "ab:authorization: x"u8.ToArray());

        CollectionAssert.AreEqual(data, RedactStreamInChunks(data, 4));
    }

    [TestMethod]
    public void RedactStream_AUnidirectionalStream_JudgesEachChunkAlone()
    {
        var redactor = new Http3RequestBytesRedactor();
        var secret = SecretHeaders();
        byte[] control = [0x00, 0x04, 0x00];

        CollectionAssert.AreEqual(control, RedactStream(redactor, 2, control));
        CollectionAssert.AreEqual(Join(secret[..2], Stars), RedactStream(redactor, 2, secret));
    }

    [TestMethod]
    public void RedactStream_MoreStreamsPartWayThroughAFrameThanItFollows_WithholdsEveryLaterStreamChunk()
    {
        var redactor = new Http3RequestBytesRedactor();
        byte[] partHeader = [(byte)Http3FrameType.Headers];
        var data = Frame(Http3FrameType.Data, "body"u8.ToArray());

        for (var stream = 0; stream < Http3RequestBytesRedactor.MaxStreamsPartWayThroughAFrame; stream++)
        {
            CollectionAssert.AreEqual(partHeader, RedactStream(redactor, stream * 4L, partHeader));
        }

        CollectionAssert.AreEqual(data, RedactStream(redactor, 1_000_000, data));
        CollectionAssert.AreEqual(partHeader, RedactStream(redactor, 1_000_004, partHeader));
        CollectionAssert.AreEqual(Stars, RedactStream(redactor, 1_000_004, [0x00]));
        CollectionAssert.AreEqual(Stars, RedactStream(redactor, 1_000_000, data));
        CollectionAssert.AreEqual(data, RedactStream(redactor, 2, data));
    }

    [TestMethod]
    public void RedactStream_StreamsThatReachAFrameBoundary_AreNoLongerCounted()
    {
        var redactor = new Http3RequestBytesRedactor();
        var data = Frame(Http3FrameType.Data, "body"u8.ToArray());

        for (var stream = 0; stream <= Http3RequestBytesRedactor.MaxStreamsPartWayThroughAFrame; stream++)
        {
            RedactStream(redactor, stream * 4L, data[..3]);
            RedactStream(redactor, stream * 4L, data[3..]);
        }

        CollectionAssert.AreEqual(data, RedactStream(redactor, 1_000_000, data));
    }

    [TestMethod]
    public void RedactStream_NoBytes_WritesNothing() =>
        Assert.IsEmpty(RedactStream(new Http3RequestBytesRedactor(), 0, []));

    [TestMethod]
    public void RedactStream_ANullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3RequestBytesRedactor().RedactStream(0, [], null!));

    [TestMethod]
    public void Constructor_NullFurtherNames_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3RequestBytesRedactor(null!));

    [TestMethod]
    public void Redact_AndFlush_RejectANullOutput()
    {
        var redactor = new Http3RequestBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
