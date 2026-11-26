using System.Buffers;
using System.Text;
using Surl.Hpack;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// The HTTP/3 redactor of the decrypted-bytes notes withholds <c>authorization</c> and
/// <c>proxy-authorization</c> from request header blocks and decoded header text (ADR-0182, BL-1424), logs a
/// session stream as received when made with its marker, and remembers a bounded number of streams
/// (amendment 35, BL-1775).
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

    private static byte[] RedactDecodedText(byte[] decodedText)
    {
        var output = new ArrayBufferWriter<byte>();
        new Http3RequestBytesRedactor().RedactDecodedText(decodedText, output);

        return output.WrittenSpan.ToArray();
    }

    private static string RedactedHeaderList(params (string Name, string Value)[] fields) =>
        Text(RedactDecodedText(Http3DecodedHeaderText.Bytes(fields.Select(field => new QpackFieldLine(field.Name, field.Value)))));

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
    public void RedactDecodedText_AuthorizationAndProxyAuthorizationLines_HaveTheirValuesStarred()
    {
        var text = ":method: GET\r\n:path: /x\r\nAuthorization: Bearer tok-text\r\nproxy-authorization: Basic abc\r\nx-note: a: b\r\n\r\n";

        var logged = Text(RedactDecodedText(Encoding.UTF8.GetBytes(text)));

        Assert.AreEqual(
            ":method: GET\r\n:path: /x\r\nAuthorization: ****\r\nproxy-authorization: ****\r\nx-note: a: b\r\n\r\n",
            logged);
    }

    [TestMethod]
    public void RedactDecodedText_WithoutALineEndOrColon_IsKept()
    {
        Assert.AreEqual(":a: b\r\nnocolon", Text(RedactDecodedText(":a: b\r\nnocolon"u8.ToArray())));
        Assert.AreEqual(":authorization: x", Text(RedactDecodedText(":authorization: x"u8.ToArray())));
        Assert.AreEqual(":x", Text(RedactDecodedText(":x"u8.ToArray())));
        Assert.AreEqual(string.Empty, Text(RedactDecodedText([])));
    }

    [TestMethod]
    public void RedactDecodedText_AHeaderListWhoseFirstFieldIsAuthorization_ShowsNoByteOfItsValue()
    {
        var logged = RedactedHeaderList(("authorization", "Bearer tok-first"), (":method", "GET"));

        Assert.AreEqual("authorization: ****\r\n:method: GET\r\n\r\n", logged);
    }

    [TestMethod]
    [DataRow("Bearer\r\ntok-after-line-end")]
    [DataRow("Bearer\r\n\r\ntok-after-line-end")]
    [DataRow("Bearer\rtok-after-line-end")]
    [DataRow("Bearer\ntok-after-line-end")]
    public void RedactDecodedText_AnAuthorizationValueHoldingALineEnd_ShowsNoByteOfTheValue(string value)
    {
        var logged = RedactedHeaderList((":method", "GET"), ("authorization", value), ("x-trace", "abc"));

        Assert.AreEqual(":method: GET\r\nauthorization: ****\r\nx-trace: abc\r\n\r\n", logged);
    }

    [TestMethod]
    public void Bytes_ANameAndAValueHoldingCarriageReturnsAndLineFeeds_WritesEachAsABackslashEscapeOnOneLine()
    {
        var text = Http3DecodedHeaderText.Bytes([new QpackFieldLine("x\r\ny", "a\rb\nc\\d")]);

        Assert.AreEqual("x\\r\\ny: a\\rb\\nc\\d\r\n\r\n", Text(text));
    }

    [TestMethod]
    public void Redact_AChunkThatStartsWithAColon_IsReadAsFramesNotAsHeaderText()
    {
        var text = ":authorization: x\r\nauthorization: y\r\n"u8.ToArray();

        CollectionAssert.AreEqual(text, Redact(text));
    }

    [TestMethod]
    public void Redact_AFurtherSecretFieldName_IsWithheldFromTheFrameAndTheDecodedTextWhateverItsCase()
    {
        var redactor = new Http3RequestBytesRedactor(["X-Secret"]);
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("x-secret", "key-1")));
        var frames = new ArrayBufferWriter<byte>();
        var text = new ArrayBufferWriter<byte>();

        redactor.Redact(frame, frames);
        redactor.RedactDecodedText(":method: GET\r\nx-secret: key-1\r\nauthorization: Bearer t\r\n"u8, text);

        CollectionAssert.AreEqual(Join(frame[..2], Stars), frames.WrittenSpan.ToArray());
        Assert.AreEqual(":method: GET\r\nx-secret: ****\r\nauthorization: ****\r\n", Text(text.WrittenSpan.ToArray()));
    }

    [TestMethod]
    public void Redact_WithNoFurtherNames_LogsAnApiKeyFieldAsReceived()
    {
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("x-api-key", "key-1")));
        var text = ":method: GET\r\nx-api-key: key-1\r\n"u8.ToArray();

        CollectionAssert.AreEqual(frame, Redact(frame));
        CollectionAssert.AreEqual(text, RedactDecodedText(text));
    }

    private static byte[] RedactStream(Http3RequestBytesRedactor redactor, long streamId, byte[] received)
    {
        var output = new ArrayBufferWriter<byte>();
        redactor.RedactStream(streamId, received, output);

        return output.WrittenSpan.ToArray();
    }

    private static byte[] RedactStreamInChunks(byte[] received, params int[] cuts) =>
        RedactStreamInChunks(new Http3RequestBytesRedactor(), 0, received, cuts);

    private static byte[] RedactStreamInChunks(Http3RequestBytesRedactor redactor, long streamId, byte[] received, params int[] cuts)
    {
        int[] ends = [.. cuts, received.Length];
        var start = 0;
        var logged = new List<byte>();
        foreach (var end in ends)
        {
            logged.AddRange(RedactStream(redactor, streamId, received[start..end]));
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

    // Sends the same first chunk on `count` streams of one direction: IDs firstStreamId, +4, +8 and so on.
    private static void AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(Http3RequestBytesRedactor redactor, long firstStreamId, int count, byte[] chunk)
    {
        for (var stream = 0; stream < count; stream++)
        {
            CollectionAssert.AreEqual(chunk, RedactStream(redactor, firstStreamId + (stream * 4L), chunk), $"stream {stream}");
        }
    }

    [TestMethod]
    public void RedactStream_AsManyStreamsPartWayThroughAFrameAsItRemembers_StillFollowsTheLowest()
    {
        var redactor = new Http3RequestBytesRedactor();
        var secret = SecretHeaders();
        AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(redactor, 0, Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection, secret[..1]);

        var logged = RedactStream(redactor, 0, secret[1..]);

        CollectionAssert.AreEqual(Join(secret[1..2], Stars), logged);
    }

    [TestMethod]
    public void RedactStream_OneStreamMorePartWayThroughAFrameThanItRemembers_ForgetsOnlyTheLowestStream()
    {
        var redactor = new Http3RequestBytesRedactor();
        var secret = SecretHeaders();
        var data = Frame(Http3FrameType.Data, "body"u8.ToArray());
        AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(redactor, 0, Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection + 1, secret[..1]);

        var forgottenFirst = RedactStream(redactor, 0, data);
        var forgottenSecond = RedactStream(redactor, 0, data);
        var followed = RedactStream(redactor, 4, secret[1..]);
        var higher = RedactStream(redactor, 1_000_000, data);
        var higherSplit = Join(RedactStream(redactor, 1_000_004, secret[..1]), RedactStream(redactor, 1_000_004, secret[1..]));
        var unidirectional = RedactStream(redactor, 2, data);

        CollectionAssert.AreEqual(Stars, forgottenFirst);
        CollectionAssert.AreEqual(Stars, forgottenSecond);
        CollectionAssert.AreEqual(Join(secret[1..2], Stars), followed);
        CollectionAssert.AreEqual(data, higher);
        CollectionAssert.AreEqual(Join(secret[..2], Stars), higherSplit);
        CollectionAssert.AreEqual(data, unidirectional);
    }

    [TestMethod]
    public void RedactStream_StreamsThatReachAFrameBoundaryWithoutAMarker_AreNotRemembered()
    {
        var redactor = new Http3RequestBytesRedactor();
        var data = Frame(Http3FrameType.Data, "body"u8.ToArray());

        for (var stream = 0; stream <= Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection; stream++)
        {
            RedactStream(redactor, stream * 4L, data[..3]);
            RedactStream(redactor, stream * 4L, data[3..]);
        }

        CollectionAssert.AreEqual(data, RedactStream(redactor, 0, data));
        CollectionAssert.AreEqual(data, RedactStream(redactor, 1_000_000, data));
    }

    // The markers WebTransport gives: signal 0x41 starts a bidirectional session stream, type 0x54 a unidirectional one.
    private static Http3RequestBytesRedactor NewSessionRedactor() => new([], 0x41, 0x54);

    // The signal or type as its two-byte variable-length integer, then session ID 0.
    private static byte[] BidirectionalSessionHeader() => [0x40, 0x41, 0x00];

    private static byte[] UnidirectionalSessionHeader() => [0x40, 0x54, 0x00];

    // Session data shaped like what frame reading would withhold: a secret HEADERS frame and one that does not decode.
    private static byte[] SessionStream(byte[] header) =>
        Join(header, SecretHeaders(), Frame(Http3FrameType.Headers, [0xFF, 0xFF, 0xFF]));

    [TestMethod]
    [DataRow(0L, "404100", DisplayName = "Bidirectional, signal 0x41")]
    [DataRow(2L, "405400", DisplayName = "Unidirectional, type 0x54")]
    public void RedactStream_ASessionStreamSplitAnywhereIntoTwoChunks_IsLoggedAsReceived(long streamId, string headerHex)
    {
        var stream = SessionStream(Convert.FromHexString(headerHex));

        for (var cut = 0; cut <= stream.Length; cut++)
        {
            var logged = RedactStreamInChunks(NewSessionRedactor(), streamId, stream, cut);

            CollectionAssert.AreEqual(stream, logged, $"cut at {cut}");
        }
    }

    [TestMethod]
    [DataRow(0L, "404100", DisplayName = "Bidirectional, signal 0x41")]
    [DataRow(2L, "405400", DisplayName = "Unidirectional, type 0x54")]
    public void RedactStream_ASessionStreamArrivingOneByteAtATime_IsLoggedAsReceived(long streamId, string headerHex)
    {
        var stream = SessionStream(Convert.FromHexString(headerHex));

        var logged = RedactStreamInChunks(NewSessionRedactor(), streamId, stream, [.. Enumerable.Range(1, stream.Length - 1)]);

        CollectionAssert.AreEqual(stream, logged);
    }

    [TestMethod]
    public void RedactStream_SessionAndRequestStreamsInterleaved_AreEachReadTheirOwnWay()
    {
        var redactor = NewSessionRedactor();
        var secret = SecretHeaders();
        var session = SessionStream(BidirectionalSessionHeader());

        var sessionFirst = RedactStream(redactor, 4, session[..1]);
        var requestFirst = RedactStream(redactor, 0, secret[..1]);
        var sessionRest = RedactStream(redactor, 4, session[1..]);
        var requestRest = RedactStream(redactor, 0, secret[1..]);

        CollectionAssert.AreEqual(session, Join(sessionFirst, sessionRest));
        CollectionAssert.AreEqual(Join(secret[..2], Stars), Join(requestFirst, requestRest));
    }

    [TestMethod]
    public void RedactStream_WithOnlyABidirectionalSignal_LogsThatSessionStreamAndStillWithholdsOnUnidirectionalStreams()
    {
        var redactor = new Http3RequestBytesRedactor([], 0x41, null);
        var bidirectional = SessionStream(BidirectionalSessionHeader());
        var secret = SecretHeaders();

        CollectionAssert.AreEqual(bidirectional, RedactStream(redactor, 0, bidirectional));
        CollectionAssert.AreEqual(UnidirectionalSessionHeader(), RedactStream(redactor, 2, UnidirectionalSessionHeader()));
        CollectionAssert.AreEqual(Join(secret[..2], Stars), RedactStream(redactor, 2, secret));
    }

    [TestMethod]
    public void RedactStream_WithOnlyAUnidirectionalType_LogsThatSessionStreamAndStillWithholdsOnRequestStreams()
    {
        var redactor = new Http3RequestBytesRedactor([], null, 0x54);
        var unidirectional = SessionStream(UnidirectionalSessionHeader());
        var secret = SecretHeaders();

        CollectionAssert.AreEqual(unidirectional, RedactStream(redactor, 2, unidirectional));
        CollectionAssert.AreEqual(
            Join(BidirectionalSessionHeader(), secret[..2], Stars),
            RedactStream(redactor, 0, Join(BidirectionalSessionHeader(), secret)));
    }

    [TestMethod]
    [DataRow(0L, "405400", DisplayName = "The unidirectional type on a bidirectional stream")]
    [DataRow(2L, "404100", DisplayName = "The bidirectional signal on a unidirectional stream")]
    public void RedactStream_TheOtherDirectionsMarker_DoesNotMakeASessionStream(long streamId, string headerHex)
    {
        var header = Convert.FromHexString(headerHex);
        var secret = SecretHeaders();

        var logged = RedactStream(NewSessionRedactor(), streamId, Join(header, secret));

        CollectionAssert.AreEqual(Join(header, secret[..2], Stars), logged);
    }

    [TestMethod]
    public void RedactStream_ANonSessionUnidirectionalStreamWithMarkersSet_WithholdsEverySecretHeadersChunk()
    {
        var redactor = NewSessionRedactor();
        var secret = SecretHeaders();
        byte[] control = [0x00, 0x04, 0x00];

        var first = RedactStream(redactor, 2, control);
        var second = RedactStream(redactor, 2, secret);
        var third = RedactStream(redactor, 2, secret);

        CollectionAssert.AreEqual(control, first);
        CollectionAssert.AreEqual(Join(secret[..2], Stars), second);
        CollectionAssert.AreEqual(Join(secret[..2], Stars), third);
    }

    [TestMethod]
    public void RedactStream_ANonSessionUnidirectionalStreamThatStartsWithASecret_WithholdsItEveryTime()
    {
        var redactor = NewSessionRedactor();
        var secret = SecretHeaders();

        var first = RedactStream(redactor, 2, secret);
        var second = RedactStream(redactor, 2, secret);

        CollectionAssert.AreEqual(Join(secret[..2], Stars), first);
        CollectionAssert.AreEqual(Join(secret[..2], Stars), second);
    }

    [TestMethod]
    public void RedactStream_ARequestStreamsLaterChunkStartingWithTheSignal_IsStillReadAsFrames()
    {
        var redactor = NewSessionRedactor();
        var data = Frame(Http3FrameType.Data, "body"u8.ToArray());
        var secret = SecretHeaders();

        var first = RedactStream(redactor, 0, data);
        var later = RedactStream(redactor, 0, Join(BidirectionalSessionHeader(), secret));

        CollectionAssert.AreEqual(data, first);
        CollectionAssert.AreEqual(Join(BidirectionalSessionHeader(), secret[..2], Stars), later);
    }

    [TestMethod]
    public void RedactStream_ANonSessionUnidirectionalStreamsLaterChunkStartingWithTheType_IsStillJudgedAlone()
    {
        var redactor = NewSessionRedactor();
        byte[] control = [0x00, 0x04, 0x00];
        var secret = SecretHeaders();

        var first = RedactStream(redactor, 2, control);
        var later = RedactStream(redactor, 2, Join(UnidirectionalSessionHeader(), secret));
        var afterwards = RedactStream(redactor, 2, secret);

        CollectionAssert.AreEqual(control, first);
        CollectionAssert.AreEqual(Join(UnidirectionalSessionHeader(), secret[..2], Stars), later);
        CollectionAssert.AreEqual(Join(secret[..2], Stars), afterwards);
    }

    [TestMethod]
    [DataRow("404100", DisplayName = "The bidirectional signal")]
    [DataRow("405400", DisplayName = "The unidirectional type")]
    public void Redact_AWholeUnitStartingWithASessionMarker_IsStillReadAsFrames(string headerHex)
    {
        var header = Convert.FromHexString(headerHex);
        var secret = SecretHeaders();
        var output = new ArrayBufferWriter<byte>();

        NewSessionRedactor().Redact(Join(header, secret), output);

        CollectionAssert.AreEqual(Join(header, secret[..2], Stars), output.WrittenSpan.ToArray());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "The parameterless constructor")]
    [DataRow(true, DisplayName = "No signal and no type")]
    public void RedactAndRedactStream_APlainRedactor_WithholdsASecretHeadersFrameThatFollowsAnUnknown0x41Frame(bool withNullMarkers)
    {
        Http3RequestBytesRedactor NewPlainRedactor() => withNullMarkers ? new([], null, null) : new();
        var unknown = Frame(0x41, []);
        var secret = SecretHeaders();
        var received = Join(unknown, secret);
        var expected = Join(unknown, secret[..2], Stars);
        var whole = new ArrayBufferWriter<byte>();

        NewPlainRedactor().Redact(received, whole);

        CollectionAssert.AreEqual(expected, whole.WrittenSpan.ToArray());
        for (var cut = 0; cut <= received.Length; cut++)
        {
            CollectionAssert.AreEqual(expected, RedactStreamInChunks(NewPlainRedactor(), 0, received, cut), $"cut at {cut}");
        }
    }

    [TestMethod]
    public void RedactStream_APlainRedactorAndAUnidirectionalStreamStartingWithTheWebTransportType_WithholdsTheSecretAfterIt()
    {
        var secret = SecretHeaders();

        var logged = RedactStream(new Http3RequestBytesRedactor(), 2, Join(UnidirectionalSessionHeader(), secret));

        CollectionAssert.AreEqual(Join(UnidirectionalSessionHeader(), secret[..2], Stars), logged);
    }

    [TestMethod]
    public void RedactStream_ASessionRedactorPastItsRememberedStreams_WritesTheForgottenStreamAsStarsAndStillLogsSessionStreams()
    {
        var redactor = NewSessionRedactor();
        var withSecret = Join(BidirectionalSessionHeader(), SecretHeaders());
        var newest = Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection * 4L;
        AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(redactor, 0, Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection + 1, BidirectionalSessionHeader());

        var forgottenFirst = RedactStream(redactor, 0, withSecret);
        var forgottenSecond = RedactStream(redactor, 0, withSecret);
        var lowestRemembered = RedactStream(redactor, 4, withSecret);
        var newestRemembered = RedactStream(redactor, newest, withSecret);
        var later = RedactStream(redactor, newest + 4, withSecret);

        CollectionAssert.AreEqual(Stars, forgottenFirst);
        CollectionAssert.AreEqual(Stars, forgottenSecond);
        CollectionAssert.AreEqual(withSecret, lowestRemembered);
        CollectionAssert.AreEqual(withSecret, newestRemembered);
        CollectionAssert.AreEqual(withSecret, later);
    }

    [TestMethod]
    public void RedactStream_ASessionRedactor_RemembersRequestStreamsAtAFrameBoundaryToo()
    {
        var redactor = NewSessionRedactor();
        var data = Frame(Http3FrameType.Data, "body"u8.ToArray());
        AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(redactor, 0, Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection + 1, data);

        var forgotten = RedactStream(redactor, 0, data);
        var remembered = RedactStream(redactor, 4, data);

        CollectionAssert.AreEqual(Stars, forgotten);
        CollectionAssert.AreEqual(data, remembered);
    }

    [TestMethod]
    public void RedactStream_PastItsRememberedUnidirectionalStreams_JudgesEachChunkOfTheForgottenStreamAlone()
    {
        var redactor = NewSessionRedactor();
        var secret = SecretHeaders();
        var withSessionHeader = Join(UnidirectionalSessionHeader(), secret);
        var newest = 2 + (Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection * 4L);
        AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(redactor, 2, Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection + 1, UnidirectionalSessionHeader());

        var forgottenFirst = RedactStream(redactor, 2, secret);
        var forgottenSecond = RedactStream(redactor, 2, withSessionHeader);
        var forgottenThird = RedactStream(redactor, 2, secret);
        var lowestRemembered = RedactStream(redactor, 6, secret);
        var newestRemembered = RedactStream(redactor, newest, secret);

        CollectionAssert.AreEqual(Join(secret[..2], Stars), forgottenFirst);
        CollectionAssert.AreEqual(Join(UnidirectionalSessionHeader(), secret[..2], Stars), forgottenSecond);
        CollectionAssert.AreEqual(Join(secret[..2], Stars), forgottenThird);
        CollectionAssert.AreEqual(secret, lowestRemembered);
        CollectionAssert.AreEqual(secret, newestRemembered);
    }

    [TestMethod]
    public void RedactStream_FillingTheUnidirectionalStreams_LeavesBidirectionalStreamsUnaffected()
    {
        var redactor = NewSessionRedactor();
        var secret = SecretHeaders();
        AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(redactor, 2, Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection + 1, UnidirectionalSessionHeader());

        var request = RedactStream(redactor, 0, secret);

        CollectionAssert.AreEqual(Join(secret[..2], Stars), request);
    }

    [TestMethod]
    public void RedactStream_FillingTheBidirectionalStreams_LeavesUnidirectionalStreamsUnaffected()
    {
        var redactor = NewSessionRedactor();
        var secret = SecretHeaders();
        RedactStream(redactor, 2, UnidirectionalSessionHeader());
        AssertTheSameFirstChunkIsLoggedAsReceivedOnStreams(redactor, 0, Http3RequestBytesRedactor.MaxRememberedStreamsPerDirection + 1, BidirectionalSessionHeader());

        var session = RedactStream(redactor, 2, secret);

        CollectionAssert.AreEqual(secret, session);
    }

    [TestMethod]
    public void RedactStream_ARedactorGivenFurtherNamesAndMarkers_WithholdsThemOnARequestStream()
    {
        var redactor = new Http3RequestBytesRedactor(["x-secret"], 0x41, 0x54);
        var frame = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("x-secret", "key-1")));

        CollectionAssert.AreEqual(Join(frame[..2], Stars), RedactStream(redactor, 0, frame));
    }

    [TestMethod]
    public void RedactStreamlessBytes_ADatagramShapedLikeASecretHeadersFrame_WritesItAsReceived()
    {
        var datagram = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("authorization", "Bearer tok-datagram")));
        var output = new ArrayBufferWriter<byte>();

        NewSessionRedactor().RedactStreamlessBytes(datagram, output);

        CollectionAssert.AreEqual(datagram, output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void RedactFramePayload_ADataPayloadShapedLikeASecretHeadersFrame_WritesItAsReceived()
    {
        var payload = Frame(Http3FrameType.Headers, Section(HpackStringCoding.Raw, ("authorization", "Bearer tok-data")));
        var output = new ArrayBufferWriter<byte>();

        new Http3RequestBytesRedactor().RedactFramePayload(payload, output);

        CollectionAssert.AreEqual(payload, output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void RedactStreamlessBytesAndRedactFramePayload_ANullOutput_Throw()
    {
        var redactor = new Http3RequestBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.RedactStreamlessBytes([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.RedactFramePayload([], null!));
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
    public void Constructor_NullFurtherNamesWithMarkers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3RequestBytesRedactor(null!, 0x41, 0x54));

    [TestMethod]
    public void Redact_AndFlush_RejectANullOutput()
    {
        var redactor = new Http3RequestBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.RedactDecodedText([], null!));
    }
}
