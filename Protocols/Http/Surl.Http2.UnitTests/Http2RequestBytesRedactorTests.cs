using System.Buffers;
using System.Text;
using Surl.Hpack;

namespace Surl.Http2;

/// <summary>
/// The HTTP/2 redactor withholds the payloads of header blocks that carry <c>authorization</c>,
/// <c>proxy-authorization</c> or a further field it was made with, every <c>PUSH_PROMISE</c> payload, the <c>DATA</c> payloads of
/// <c>CONNECT</c> streams, and, once it can no longer follow the header blocks, every later
/// <c>HEADERS</c>, <c>CONTINUATION</c>, <c>PUSH_PROMISE</c> and <c>DATA</c> payload (ADR-0182,
/// BL-1423, BL-1714, BL-1728, BL-1729, BL-1733).
/// </summary>
[TestClass]
public sealed class Http2RequestBytesRedactorTests
{
    private const string Secret = "Basic YWxpY2U6czNjcmV0";

    private static byte[] Preface => Http2ConnectionPreface.Preface.ToArray();

    private static byte[] Headers(byte[] block, bool endHeaders = true, int? padding = null, Http2Priority? priority = null) =>
        Http2FrameEncoder.Encode(new Http2HeadersFrame(1, block, true, endHeaders, priority, padding));

    private static byte[] Block(HpackEncoder encoder, params (string Name, string Value)[] fields) =>
        encoder.Encode(fields.Select(field => new HpackHeaderField(field.Name, field.Value)));

    private static (string Text, byte[] Bytes) Redact(IEnumerable<byte[]> chunks, bool flush = true)
    {
        var redactor = new Http2RequestBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        if (flush)
        {
            redactor.Flush(output);
        }

        return (Encoding.Latin1.GetString(output.WrittenSpan), output.WrittenSpan.ToArray());
    }

    private static byte[] Join(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private static readonly (string, string)[] Plain = [(":method", "GET"), (":path", "/"), ("host", "h")];

    private static byte[] WithPayloadWithheld(byte[] frame) => Join(frame[..9], "****"u8.ToArray());

    private static byte[] Repeat(byte[] frame, int count) => Join([.. Enumerable.Repeat(frame, count)]);

    private static byte[] OpenEmptyHeaders => Headers([], endHeaders: false);

    private static byte[] EmptyContinuation(bool endHeaders = false) =>
        Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, [], endHeaders));

    [TestMethod]
    public void Redact_HuffmanCodedAuthorization_WithholdsThePayload()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Huffman);
        var block = Block(encoder, (":method", "GET"), ("authorization", Secret));
        var stream = Join(Preface, Headers(block));

        var (_, bytes) = Redact([stream]);

        Assert.AreEqual(stream.Length - block.Length + 4, bytes.Length);
        CollectionAssert.AreEqual(Join(Preface, Headers(block)[..9], "****"u8.ToArray()), bytes);
    }

    [TestMethod]
    public void Redact_LiteralAuthorization_NeverLogsTheValue()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Raw);
        var stream = Join(Preface, Headers(Block(encoder, ("authorization", Secret))));

        var (text, _) = Redact([stream]);

        Assert.DoesNotContain("YWxpY2U6", text);
    }

    [TestMethod]
    public void Redact_IndexedAuthorizationOnSecondRequest_IsWithheldFromTheDynamicTable()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Raw);
        var first = Headers(Block(encoder, ("authorization", Secret)));
        var second = Headers(Block(encoder, ("authorization", Secret)));
        var third = Headers(Block(encoder, (":path", "/x")));

        var (_, bytes) = Redact([Join(Preface, first, second, third)]);

        // Each secret block is "****" behind its 9-byte header; the plain third is whole.
        Assert.AreEqual(24 + 13 + 13 + third.Length, bytes.Length);
    }

    [TestMethod]
    public void Redact_BlockWithoutSecret_IsLoggedUnchanged()
    {
        var stream = Join(Preface, Headers(Block(new HpackEncoder(), Plain)));

        var (_, bytes) = Redact([stream]);

        CollectionAssert.AreEqual(stream, bytes);
    }

    [TestMethod]
    public void Redact_ProxyAuthorizationSplitOverContinuation_WithholdsBothFrames()
    {
        var block = Block(new HpackEncoder(stringCoding: HpackStringCoding.Raw), (":method", "GET"), ("Proxy-Authorization", Secret));
        var head = Http2FrameEncoder.Encode(new Http2HeadersFrame(1, block[..3], true, false));
        var tail = Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, block[3..], true));

        var (text, bytes) = Redact([Join(Preface, head, tail)]);

        Assert.DoesNotContain("YWxpY2U6", text);
        Assert.AreEqual(24 + 13 + 13, bytes.Length);
    }

    [TestMethod]
    public void Redact_PaddedPrioritisedHeadersWithSecret_IsWithheld()
    {
        var block = Block(new HpackEncoder(stringCoding: HpackStringCoding.Raw), ("authorization", Secret));
        var frame = Headers(block, padding: 3, priority: new Http2Priority(false, 0, 15));

        var (text, _) = Redact([Join(Preface, frame)]);

        Assert.DoesNotContain("YWxpY2U6", text);
    }

    [TestMethod]
    public void Redact_ApiKeyOnARedactorMadeWithNoFurtherNames_IsLoggedAsReceived()
    {
        var stream = Join(Preface, Headers(Block(new HpackEncoder(), ("x-api-key", Secret))));

        var (_, bytes) = Redact([stream]);

        CollectionAssert.AreEqual(stream, bytes);
    }

    [TestMethod]
    public void Redact_FurtherNameInAnotherCase_WithholdsThePayload()
    {
        var frame = Headers(Block(new HpackEncoder(), ("x-api-key", Secret)));

        var bytes = RedactWithFurtherName("X-Api-Key", Join(Preface, frame));

        CollectionAssert.AreEqual(Join(Preface, WithPayloadWithheld(frame)), bytes);
    }

    [TestMethod]
    public void Redact_CredentialOnARedactorMadeWithFurtherNames_IsStillWithheld()
    {
        var frame = Headers(Block(new HpackEncoder(), ("authorization", Secret)));

        var bytes = RedactWithFurtherName("X-Api-Key", Join(Preface, frame));

        CollectionAssert.AreEqual(Join(Preface, WithPayloadWithheld(frame)), bytes);
    }

    private static byte[] RedactWithFurtherName(string name, byte[] received)
    {
        var redactor = new Http2RequestBytesRedactor([name]);
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(received, output);
        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }

    [TestMethod]
    public void Constructor_NullFurtherNames_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http2RequestBytesRedactor(null!));

    [TestMethod]
    public void Redact_BytesInOneByteChunks_GiveTheSameDumpAsOneChunk()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Raw);
        var data = Http2FrameEncoder.Encode(new Http2DataFrame(1, [1, 2, 3], true));
        var stream = Join(Preface, Headers(Block(encoder, ("authorization", Secret))), data, Headers(Block(encoder, Plain)));

        var whole = Redact([stream]).Bytes;
        var pieces = Redact(stream.Select(value => new[] { value })).Bytes;

        CollectionAssert.AreEqual(whole, pieces);
    }

    [TestMethod]
    public void Redact_DataAndOtherFrames_PassThroughAsTheyArrive()
    {
        var data = Http2FrameEncoder.Encode(new Http2DataFrame(1, Encoding.ASCII.GetBytes("payload"), false));
        var redactor = new Http2RequestBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(Join(Preface, data[..12]), output);

        Assert.AreEqual(24 + 12, output.WrittenCount);
    }

    private static readonly (string, string)[] Connect = [(":method", "CONNECT"), (":authority", "origin:80")];

    private static byte[] HeadersOn(int streamId, byte[] block) =>
        Http2FrameEncoder.Encode(new Http2HeadersFrame(streamId, block, false, true));

    private static byte[] DataOn(int streamId, string text) =>
        Http2FrameEncoder.Encode(new Http2DataFrame(streamId, Encoding.ASCII.GetBytes(text), false));

    [TestMethod]
    public void Redact_DataOnAConnectStream_IsWithheld()
    {
        var connect = HeadersOn(1, Block(new HpackEncoder(), Connect));
        var data = DataOn(1, "Authorization: Basic YWxpY2U6czNjcmV0\r\n");

        var (_, bytes) = Redact([Join(Preface, connect, data, data)]);

        CollectionAssert.AreEqual(
            Join(Preface, connect, WithPayloadWithheld(data), WithPayloadWithheld(data)),
            bytes);
    }

    [TestMethod]
    public void Redact_EmptyDataOnAConnectStream_WritesTheHeaderAlone()
    {
        var connect = HeadersOn(1, Block(new HpackEncoder(), Connect));
        var empty = DataOn(1, string.Empty);

        var (_, bytes) = Redact([Join(Preface, connect, empty)]);

        CollectionAssert.AreEqual(Join(Preface, connect, empty), bytes);
    }

    [TestMethod]
    public void Redact_DataOnAnotherStreamBesideAConnect_IsLoggedAsReceived()
    {
        var encoder = new HpackEncoder();
        var connect = HeadersOn(1, Block(encoder, Connect));
        var post = HeadersOn(3, Block(encoder, (":method", "POST"), (":path", "/")));
        var data = DataOn(3, "upload");

        var (_, bytes) = Redact([Join(Preface, connect, post, data)]);

        CollectionAssert.AreEqual(Join(Preface, connect, post, data), bytes);
    }

    [TestMethod]
    public void Redact_DataOnAConnectStreamDifferingOnlyInTheReservedStreamBit_IsWithheld()
    {
        var connect = HeadersOn(1, Block(new HpackEncoder(), Connect));
        var data = DataOn(1, "tunnelled");
        data[5] |= 0x80;

        var (_, bytes) = Redact([Join(Preface, connect, data)]);

        CollectionAssert.AreEqual(Join(Preface, connect, WithPayloadWithheld(data)), bytes);
    }

    [TestMethod]
    public void Redact_DataAfterAConnectBlockWithheldForItsCredential_IsWithheld()
    {
        var block = Block(
            new HpackEncoder(stringCoding: HpackStringCoding.Raw),
            (":method", "CONNECT"),
            ("proxy-authorization", Secret));
        var connect = HeadersOn(1, block);
        var data = DataOn(1, "tunnelled");

        var (_, bytes) = Redact([Join(Preface, connect, data)]);

        CollectionAssert.AreEqual(Join(Preface, WithPayloadWithheld(connect), WithPayloadWithheld(data)), bytes);
    }

    [TestMethod]
    public void Redact_DataAfterAConnectSplitOverContinuation_IsWithheld()
    {
        var block = Block(new HpackEncoder(), Connect);
        var head = Headers(block[..1], endHeaders: false);
        var tail = Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, block[1..], true));
        var data = DataOn(1, "tunnelled");

        var (_, bytes) = Redact([Join(Preface, head, tail, data)]);

        CollectionAssert.AreEqual(Join(Preface, head, tail, WithPayloadWithheld(data)), bytes);
    }

    [TestMethod]
    public void Redact_DataOnAConnectStreamInOneByteChunks_IsWithheld()
    {
        var connect = HeadersOn(1, Block(new HpackEncoder(), Connect));
        var data = DataOn(1, "tunnelled");
        var stream = Join(Preface, connect, data);

        var (_, bytes) = Redact(stream.Select(value => new[] { value }));

        CollectionAssert.AreEqual(Join(Preface, connect, WithPayloadWithheld(data)), bytes);
    }

    [TestMethod]
    public void Redact_DataAfterTheDecoderIsLost_IsWithheld()
    {
        var bad = Headers([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]);
        var data = DataOn(3, "perhaps a tunnel");
        var empty = DataOn(5, string.Empty);

        var (_, bytes) = Redact([Join(Preface, bad, data, empty)]);

        CollectionAssert.AreEqual(Join(Preface, WithPayloadWithheld(bad), WithPayloadWithheld(data), empty), bytes);
    }

    [TestMethod]
    public void Redact_DataOnAnyStreamAfterMoreConnectStreamsThanAreRemembered_IsWithheld()
    {
        var encoder = new HpackEncoder();
        var connects = Enumerable.Range(0, 4097)
            .Select(index => HeadersOn((2 * index) + 1, Block(encoder, Connect)))
            .ToArray();
        var data = DataOn(100_001, "upload");

        var remembered = Redact([Join(Preface, Join(connects[..4096]), data)]).Bytes;
        var overflowed = Redact([Join(Preface, Join(connects), data)]).Bytes;

        CollectionAssert.AreEqual(data, remembered[^data.Length..]);
        CollectionAssert.AreEqual(WithPayloadWithheld(data), overflowed[^13..]);
    }

    [TestMethod]
    public void Redact_BlockThatDoesNotDecode_WithholdsItAndEveryLaterBlock()
    {
        var bad = Headers([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]);
        var good = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, bad, good)]);

        Assert.AreEqual(24 + 13 + 13, bytes.Length);
    }

    [TestMethod]
    public void Redact_PaddingLongerThanThePayload_WithholdsTheBlock()
    {
        var frame = Join(new byte[] { 0, 0, 2, 1, 0x4 | 0x8, 0, 0, 0, 1 }, [9, 0]);

        var (_, bytes) = Redact([Join(Preface, frame)]);

        Assert.AreEqual(24 + 13, bytes.Length);
    }

    [TestMethod]
    public void Redact_PaddedFrameWithNoPayload_WithholdsTheBlock()
    {
        var frame = new byte[] { 0, 0, 0, 1, 0x4 | 0x8, 0, 0, 0, 1 };

        var (_, bytes) = Redact([Join(Preface, frame)]);

        Assert.AreEqual(24 + 13, bytes.Length);
    }

    [TestMethod]
    public void Redact_OtherFrameInsideAnOpenBlock_WithholdsTheHeldFramesAndLaterBlocks()
    {
        var block = Block(new HpackEncoder(), Plain);
        var open = Http2FrameEncoder.Encode(new Http2HeadersFrame(1, block, true, false));
        var data = Http2FrameEncoder.Encode(new Http2DataFrame(1, [1], false));
        var later = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, open, data, later)]);

        CollectionAssert.AreEqual(
            Join(Preface, WithPayloadWithheld(open), WithPayloadWithheld(data), WithPayloadWithheld(later)),
            bytes);
    }

    [TestMethod]
    public void Redact_ContinuationWithNoOpenBlock_WithholdsItAndEveryLaterBlock()
    {
        var continuation = Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, Block(new HpackEncoder(), Plain), true));
        var later = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, continuation, later)]);

        CollectionAssert.AreEqual(
            Join(Preface, WithPayloadWithheld(continuation), WithPayloadWithheld(later)),
            bytes);
    }

    [TestMethod]
    public void Redact_HeadersInsideAnOpenBlock_WithholdsTheBlockAndEveryLaterBlock()
    {
        var block = Block(new HpackEncoder(), Plain);
        var open = Headers(block[..3], endHeaders: false);
        var inside = Headers(block[3..]);
        var later = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, open, inside, later)]);

        CollectionAssert.AreEqual(
            Join(Preface, WithPayloadWithheld(open), WithPayloadWithheld(inside), WithPayloadWithheld(later)),
            bytes);
    }

    [TestMethod]
    public void Redact_ContinuationOnAnotherStream_WithholdsTheBlockAndEveryLaterBlock()
    {
        var block = Block(new HpackEncoder(), Plain);
        var open = Headers(block[..3], endHeaders: false);
        var otherStream = Http2FrameEncoder.Encode(new Http2ContinuationFrame(3, block[3..], true));
        var later = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, open, otherStream, later)]);

        CollectionAssert.AreEqual(
            Join(Preface, WithPayloadWithheld(open), WithPayloadWithheld(otherStream), WithPayloadWithheld(later)),
            bytes);
    }

    [TestMethod]
    public void Redact_PushPromise_WithholdsItAndEveryLaterBlock()
    {
        var promise = Http2FrameEncoder.Encode(new Http2PushPromiseFrame(1, 2, Block(new HpackEncoder(), Plain), false));
        var continuation = Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, Block(new HpackEncoder(), Plain), true));
        var later = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, promise, continuation, later)]);

        CollectionAssert.AreEqual(
            Join(Preface, WithPayloadWithheld(promise), WithPayloadWithheld(continuation), WithPayloadWithheld(later)),
            bytes);
    }

    [TestMethod]
    public void Redact_PushPromiseInsideAnOpenBlock_WithholdsTheHeldFramesToo()
    {
        var open = Headers(Block(new HpackEncoder(), Plain), endHeaders: false);
        var promise = Http2FrameEncoder.Encode(new Http2PushPromiseFrame(1, 2, Block(new HpackEncoder(), Plain), true));
        var later = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, open, promise, later)]);

        CollectionAssert.AreEqual(
            Join(Preface, WithPayloadWithheld(open), WithPayloadWithheld(promise), WithPayloadWithheld(later)),
            bytes);
    }

    [TestMethod]
    public void Redact_ContinuationDifferingOnlyInTheReservedStreamBit_ContinuesTheBlock()
    {
        var block = Block(new HpackEncoder(), Plain);
        var head = Headers(block[..3], endHeaders: false);
        var tail = Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, block[3..], true));
        tail[5] |= 0x80;
        var stream = Join(Preface, head, tail);

        var (_, bytes) = Redact([stream]);

        CollectionAssert.AreEqual(stream, bytes);
    }

    [TestMethod]
    public void Redact_BlockLargerThanTheCap_IsWithheldAsItArrives()
    {
        var big = new byte[70 * 1024];
        var header = new byte[] { 0x01, 0x18, 0x00, 1, 0x4, 0, 0, 0, 1 };
        var redactor = new Http2RequestBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(Join(Preface, header, big), output);

        Assert.AreEqual(24 + 13, output.WrittenCount);
    }

    [TestMethod]
    public void Redact_ContinuationAfterAnOverTheCapHeadersFrame_WithholdsItsPayload()
    {
        var big = new byte[70 * 1024];
        var header = new byte[] { 0x01, 0x18, 0x00, 0x1, 0x0, 0, 0, 0, 1 };
        var block = Block(new HpackEncoder(stringCoding: HpackStringCoding.Raw), Plain);
        var continuation = Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, block, true));

        var (_, bytes) = Redact([Join(Preface, header, big, continuation)]);

        CollectionAssert.AreEqual(
            Join(Preface, header, "****"u8.ToArray(), continuation[..9], "****"u8.ToArray()),
            bytes);
    }

    [TestMethod]
    public void Redact_ContinuationsPastTheCap_WithholdTheHeldFramesToo()
    {
        var fragment = new byte[40 * 1024];
        var first = Join(new byte[] { 0x00, 0xA0, 0x00, 1, 0, 0, 0, 0, 1 }, fragment);
        var second = Join(new byte[] { 0x00, 0xA0, 0x00, 9, 0x4, 0, 0, 0, 1 }, fragment);

        var (_, bytes) = Redact([Join(Preface, first, second)]);

        Assert.AreEqual(24 + 13 + 13, bytes.Length);
    }

    [TestMethod]
    public void Redact_EmptyContinuationsPastTheCap_WritesTheHeldFramesWithheldBeforeFlush()
    {
        var open = OpenEmptyHeaders;
        var empty = EmptyContinuation();
        var redactor = new Http2RequestBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        // 7,281 frame headers are 65,529 bytes, the most whole empty frames the 65,536 cap holds.
        redactor.Redact(Join(Preface, open, Repeat(empty, 7280)), output);

        Assert.AreEqual(24, output.WrittenCount);

        redactor.Redact(empty, output);

        Assert.AreEqual(94_690, output.WrittenCount);
        CollectionAssert.AreEqual(
            Join(Preface, WithPayloadWithheld(open), Repeat(WithPayloadWithheld(empty), 7281)),
            output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void Redact_HeaderBlockFramesAfterEmptyContinuationsPassTheCap_WithholdsTheirPayloads()
    {
        var closing = Http2FrameEncoder.Encode(new Http2ContinuationFrame(1, Block(new HpackEncoder(), Plain), true));
        var later = Headers(Block(new HpackEncoder(), Plain));
        var stream = Join(Preface, OpenEmptyHeaders, Repeat(EmptyContinuation(), 7281), closing, later);

        var (_, bytes) = Redact([stream]);

        Assert.AreEqual(94_716, bytes.Length);
        CollectionAssert.AreEqual(
            Join(WithPayloadWithheld(closing), WithPayloadWithheld(later)),
            bytes[^26..]);
    }

    [TestMethod]
    public void Redact_EmptyContinuationsEndingTheBlockAtTheCap_AreLoggedAsReceived()
    {
        // 7,281 frames in all, the most the cap holds.
        var stream = Join(Preface, OpenEmptyHeaders, Repeat(EmptyContinuation(), 7279), EmptyContinuation(endHeaders: true));

        var (_, bytes) = Redact([stream]);

        Assert.AreEqual(65_553, bytes.Length);
        CollectionAssert.AreEqual(stream, bytes);
    }

    [TestMethod]
    public void Redact_HeadersFrameWhoseFrameHeaderTakesItPastTheCap_IsWithheldAtOnce()
    {
        // A 65,528-byte payload: 65,537 bytes with its frame header.
        byte[] header = [0x00, 0xFF, 0xF8, 0x01, 0x04, 0, 0, 0, 1];

        var (_, bytes) = Redact([Join(Preface, header)], flush: false);

        Assert.AreEqual(37, bytes.Length);
        CollectionAssert.AreEqual(Join(Preface, WithPayloadWithheld(header)), bytes);
    }

    [TestMethod]
    public void Redact_HeadersFrameThatExactlyFillsTheCap_IsHeld()
    {
        // A 65,527-byte payload: 65,536 bytes with its frame header.
        byte[] header = [0x00, 0xFF, 0xF7, 0x01, 0x04, 0, 0, 0, 1];

        var (_, bytes) = Redact([Join(Preface, header)], flush: false);

        Assert.AreEqual(24, bytes.Length);
    }

    [TestMethod]
    public void Flush_HeaderBlockLeftOpen_WritesHeadersAndWithholdsPayload()
    {
        var open = Http2FrameEncoder.Encode(new Http2HeadersFrame(1, [0x82, 0x84], true, false));

        var (_, bytes) = Redact([Join(Preface, open)]);

        Assert.AreEqual(24 + 13, bytes.Length);
    }

    [TestMethod]
    public void Flush_FrameCutShortInsideItsPayload_WritesItsHeaderAndWithholdsThePayload()
    {
        var open = Http2FrameEncoder.Encode(new Http2HeadersFrame(1, [0x82, 0x84, 0x86], true, true));

        var (_, bytes) = Redact([Join(Preface, open[..11])]);

        Assert.AreEqual(24 + 13, bytes.Length);
    }

    [TestMethod]
    public void Flush_FrameHeaderCutShort_WritesTheBytesReceived()
    {
        var (_, bytes) = Redact([Join(Preface, [0, 0, 5])]);

        Assert.AreEqual(24 + 3, bytes.Length);
    }

    [TestMethod]
    public void Flush_NothingHeld_WritesNothing()
    {
        var (_, bytes) = Redact([Preface]);

        Assert.AreEqual(24, bytes.Length);
    }

    [TestMethod]
    public void ObserveSent_AnyBytes_ChangesNothing()
    {
        var redactor = new Http2RequestBytesRedactor();
        redactor.ObserveSent("anything"u8);
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(Preface, output);

        Assert.AreEqual(24, output.WrittenCount);
    }

    [TestMethod]
    public void Redact_EmptyHeadersFrame_IsLoggedAsReceived()
    {
        var frame = Headers([]);

        var (_, bytes) = Redact([Join(Preface, frame)]);

        CollectionAssert.AreEqual(Join(Preface, frame), bytes);
    }

    [TestMethod]
    public void Redact_And_Flush_NullOutput_Throw()
    {
        var redactor = new Http2RequestBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
