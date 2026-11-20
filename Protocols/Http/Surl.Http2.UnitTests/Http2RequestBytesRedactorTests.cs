using System.Buffers;
using System.Text;
using Surl.Hpack;

namespace Surl.Http2;

/// <summary>
/// The HTTP/2 redactor withholds the payloads of header blocks that carry <c>authorization</c> or
/// <c>proxy-authorization</c> and logs every other byte as received (ADR-0182, BL-1423).
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
        var ping = Http2FrameEncoder.Encode(new Http2DataFrame(1, [1], false));
        var later = Headers(Block(new HpackEncoder(), Plain));

        var (_, bytes) = Redact([Join(Preface, open, ping, later)]);

        Assert.AreEqual(24 + 13 + ping.Length + 13, bytes.Length);
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
    public void Redact_ContinuationsPastTheCap_WithholdTheHeldFramesToo()
    {
        var fragment = new byte[40 * 1024];
        var first = Join(new byte[] { 0x00, 0xA0, 0x00, 1, 0, 0, 0, 0, 1 }, fragment);
        var second = Join(new byte[] { 0x00, 0xA0, 0x00, 9, 0x4, 0, 0, 0, 1 }, fragment);

        var (_, bytes) = Redact([Join(Preface, first, second)]);

        Assert.AreEqual(24 + 13 + 13, bytes.Length);
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
