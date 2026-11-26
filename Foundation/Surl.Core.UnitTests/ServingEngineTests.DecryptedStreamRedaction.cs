using Surl.Hpack;
using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Qpack;

namespace Surl.Core;

// The decrypted-bytes notes of a stream are redacted by one redactor that follows each stream's bytes
// across the chunks reported (ADR-0182, amendment 21; BL-1762). Proved with the HTTP/3 redactor, whose
// HEADERS frames QUIC may split wherever it likes.
public sealed partial class ServingEngineTests
{
    private const byte Http3Data = 0x00;
    private const byte Http3Headers = 0x01;

    private static readonly byte[] WithheldPayload = "****"u8.ToArray();

    private static byte[] ShortHttp3Frame(byte type, byte[] payload) => [type, checked((byte)payload.Length), .. payload];

    // What a note shows of a withheld HEADERS frame: its two-byte header and four asterisks.
    private static byte[] Withholding(byte[] headersFrame) => [.. headersFrame[..2], .. WithheldPayload];

    private static byte[] AuthorizationHeadersFrame(string value)
    {
        var section = new QpackEncoder(HpackStringCoding.Raw).EncodeFieldSection(
            [new QpackFieldLine(":method", "GET"), new QpackFieldLine("authorization", value)]);
        Assert.IsLessThan(64, section.Length, "the frame helper writes a one-byte length");

        return ShortHttp3Frame(Http3Headers, section);
    }

    [TestMethod]
    public async Task ServeAsync_Http3HeadersFrameSplitAcrossTwoStreamChunks_NotesNoByteOfTheSecretField()
    {
        var frame = AuthorizationHeadersFrame("Bearer tok-split");
        var cut = frame.Length - 6;

        var (notes, published) = await ServeDecryptedReportsAsync(sink =>
        {
            sink.Received(ExchangeBytesLayer.Decrypted, 0, frame.AsSpan(0, cut));
            sink.Received(ExchangeBytesLayer.Decrypted, 0, frame.AsSpan(cut));
        });

        CollectionAssert.AreEqual(Withholding(frame), notes[0]);
        Assert.IsEmpty(notes[1]);
        CollectionAssert.AreEqual(frame, published.SelectMany(chunk => chunk).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_Http3ChunksOfTwoStreamsInterleaved_RedactsEachStreamByItsOwnFrames()
    {
        var secret = AuthorizationHeadersFrame("Bearer tok-interleaved");
        var body = ShortHttp3Frame(Http3Data, "0123456789"u8.ToArray());

        var (notes, _) = await ServeDecryptedReportsAsync(sink =>
        {
            sink.Received(ExchangeBytesLayer.Decrypted, 0, secret.AsSpan(0, 8));
            sink.Received(ExchangeBytesLayer.Decrypted, 4, body.AsSpan(0, 5));
            sink.Received(ExchangeBytesLayer.Decrypted, 0, secret.AsSpan(8));
            sink.Received(ExchangeBytesLayer.Decrypted, 4, body.AsSpan(5));
        });

        CollectionAssert.AreEqual(Withholding(secret), notes[0]);
        CollectionAssert.AreEqual(body[..5], notes[1]);
        Assert.IsEmpty(notes[2]);
        CollectionAssert.AreEqual(body[5..], notes[3]);
    }

    [TestMethod]
    public async Task ServeAsync_Http3FramePayloadReportedWithinASplitStream_IsRedactedAloneAndLeavesTheStreamsPlaceAlone()
    {
        var secret = AuthorizationHeadersFrame("Bearer tok-payload");

        var (notes, published) = await ServeDecryptedReportsAsync(sink =>
        {
            sink.Received(ExchangeBytesLayer.Decrypted, 0, secret.AsSpan(0, 8));
            sink.ReceivedFramePayload(ExchangeBytesLayer.Decrypted, 0, "hello"u8);
            sink.Received(ExchangeBytesLayer.Decrypted, 0, secret.AsSpan(8));
        });

        CollectionAssert.AreEqual("hello"u8.ToArray(), notes[1]);
        Assert.IsEmpty(notes[2]);
        CollectionAssert.AreEqual("hello"u8.ToArray(), published[1]);
    }

    [TestMethod]
    public async Task ServeAsync_Http3DecodedTextReportedWithinASplitStream_IsRedactedByTheDecodedTextRules()
    {
        var secret = AuthorizationHeadersFrame("Bearer tok-decoded");

        var (notes, _) = await ServeDecryptedReportsAsync(sink =>
        {
            sink.Received(ExchangeBytesLayer.Decrypted, 0, secret.AsSpan(0, 8));
            sink.Received(ExchangeBytesLayer.Decoded, 0, ":method: GET\r\nauthorization: Bearer t\r\n"u8);
        });

        CollectionAssert.AreEqual(":method: GET\r\nauthorization: ****\r\n"u8.ToArray(), notes[1]);
    }

    // A QUIC datagram is session data, never an HTTP/3 frame (ADR-0182, amendment 37; BL-1788).
    [TestMethod]
    public async Task ServeAsync_WebTransportDatagramShapedLikeAHeadersFrame_IsNotedAsReceived()
    {
        var datagram = AuthorizationHeadersFrame("Bearer tok-datagram");

        var (notes, published) = await ServeDecryptedReportsAsync(
            sink => sink.Received(ExchangeBytesLayer.Decrypted, null, datagram));

        CollectionAssert.AreEqual(datagram, notes[0]);
        CollectionAssert.AreEqual(datagram, published[0]);
    }

    [TestMethod]
    public async Task ServeAsync_Http3DataPayloadShapedLikeAHeadersFrame_IsNotedAsReceived()
    {
        var payload = AuthorizationHeadersFrame("Bearer tok-data");

        var (notes, published) = await ServeDecryptedReportsAsync(
            sink => sink.ReceivedFramePayload(ExchangeBytesLayer.Decrypted, 0, payload));

        CollectionAssert.AreEqual(payload, notes[0]);
        CollectionAssert.AreEqual(payload, published[0]);
    }

    [TestMethod]
    public async Task ServeAsync_Http3SecretHeadersFrameOnARequestStreamAfterADatagramAndADataPayload_IsStillWithheld()
    {
        var secret = AuthorizationHeadersFrame("Bearer tok-request-stream");

        var (notes, _) = await ServeDecryptedReportsAsync(sink =>
        {
            sink.Received(ExchangeBytesLayer.Decrypted, null, secret);
            sink.ReceivedFramePayload(ExchangeBytesLayer.Decrypted, 0, secret);
            sink.Received(ExchangeBytesLayer.Decrypted, 0, secret);
        });

        CollectionAssert.AreEqual(Withholding(secret), notes[2]);
    }

    [TestMethod]
    public async Task ServeAsync_ServerWhoseRedactorHasNoStreamlessOrFramePayloadRules_RedactsBothLikeAnyChunk()
    {
        var logs = new FakeExchangeLogFactory();
        var server = new FakeRedactingConnectionServer(
            (connection, context) =>
            {
                context.DecryptedBytes.Received(ExchangeBytesLayer.Decrypted, null, "pw secret\n"u8);
                context.DecryptedBytes.ReceivedFramePayload(ExchangeBytesLayer.Decrypted, 0, "pw secret\n"u8);
                return Task.CompletedTask;
            },
            "http");

        await ServeOneAsync(server, logs, new SurlEventStream(new ManualTimeProvider()), new InMemoryConnection([]));

        var notes = logs.LogOf(1).Entries.Select(entry => entry.Text).Where(text => text.Contains("bytes ")).ToArray();
        Assert.HasCount(2, notes);
        StringAssert.EndsWith(notes[0], "8 bytes: " + Convert.ToHexString("pw ****\n"u8));
        StringAssert.EndsWith(notes[1], "8 bytes: " + Convert.ToHexString("pw ****\n"u8));
    }

    [TestMethod]
    public async Task ServeAsync_Http3DecodedTextWhoseFirstFieldIsAuthorization_NotesNoByteOfItsValue()
    {
        var text = "authorization: Bearer tok-first\r\n:method: GET\r\n\r\n"u8.ToArray();

        var (notes, published) = await ServeDecryptedReportsAsync(
            sink => sink.Received(ExchangeBytesLayer.Decoded, 0, text));

        CollectionAssert.AreEqual("authorization: ****\r\n:method: GET\r\n\r\n"u8.ToArray(), notes[0]);
        CollectionAssert.AreEqual(text, published[0]);
    }

    [TestMethod]
    public async Task ServeAsync_ServerWhoseRedactorHasNoDecodedTextRules_RedactsDecodedTextLikeAnyChunk()
    {
        var logs = new FakeExchangeLogFactory();
        var server = new FakeRedactingConnectionServer(
            (connection, context) =>
            {
                context.DecryptedBytes.Received(ExchangeBytesLayer.Decoded, 0, "pw secret\n"u8);
                return Task.CompletedTask;
            },
            "http");

        await ServeOneAsync(server, logs, new SurlEventStream(new ManualTimeProvider()), new InMemoryConnection([]));

        var note = logs.LogOf(1).Entries.Select(entry => entry.Text).Single(text => text.Contains("bytes "));
        StringAssert.EndsWith(note, "8 bytes: " + Convert.ToHexString("pw ****\n"u8));
    }

    [TestMethod]
    public async Task ServeAsync_ServerThatDoesNotRedactReportsAFramePayloadAndStreamlessBytes_NotesThemAsReceived()
    {
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer(
            (connection, context) =>
            {
                context.DecryptedBytes.ReceivedFramePayload(ExchangeBytesLayer.Decrypted, 0, "ab"u8);
                context.DecryptedBytes.Received(ExchangeBytesLayer.Decrypted, null, "cd"u8);
                context.DecryptedBytes.Received(ExchangeBytesLayer.Decoded, 0, "ef"u8);
                return Task.CompletedTask;
            },
            "http");

        await ServeOneAsync(server, logs, new SurlEventStream(new ManualTimeProvider()), new InMemoryConnection([]));

        var notes = logs.LogOf(1).Entries.Select(entry => entry.Text).Where(text => text.Contains("bytes ")).ToArray();
        Assert.HasCount(3, notes);
        StringAssert.EndsWith(notes[0], "stream 0, 2 bytes: 6162");
        StringAssert.EndsWith(notes[1], "2 bytes: 6364");
        StringAssert.EndsWith(notes[2], "2 bytes: 6566");
    }

    [TestMethod]
    public async Task ServeAsync_Http3WireBytesReportedToTheSink_AreRedactedAsAWholeUnit()
    {
        var secret = AuthorizationHeadersFrame("Bearer tok-wire");

        var (notes, published) = await ServeDecryptedReportsAsync(
            sink => sink.Received(ExchangeBytesLayer.Wire, null, secret));

        CollectionAssert.AreEqual(Withholding(secret), notes[0]);
        CollectionAssert.AreEqual(secret, published[0]);
    }

    [TestMethod]
    public async Task ServeAsync_ServerThatDoesNotRedactReportsWireBytesToTheSink_NotesThemAsReceived()
    {
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer(
            (connection, context) =>
            {
                context.DecryptedBytes.Received(ExchangeBytesLayer.Wire, null, "gh"u8);
                return Task.CompletedTask;
            },
            "http");

        await ServeOneAsync(server, logs, new SurlEventStream(new ManualTimeProvider()), new InMemoryConnection([]));

        var note = logs.LogOf(1).Entries.Select(entry => entry.Text).Single(text => text.Contains("bytes "));
        StringAssert.EndsWith(note, "2 bytes: 6768");
    }

    // Serves one exchange whose server redacts with the HTTP/3 redactor and makes the given reports, and
    // returns the bytes each note shows and the bytes each event carries, in the order reported.
    private static async Task<(byte[][] Notes, byte[][] Published)> ServeDecryptedReportsAsync(Action<IDecryptedBytesSink> report)
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        var logs = new FakeExchangeLogFactory();
        var server = new Http3RedactingConnectionServer(report);
        await ServeOneAsync(server, logs, stream, new InMemoryConnection([]));

        var notes = logs.LogOf(1).Entries.Select(entry => entry.Text)
            .Where(text => text.Contains(" bytes: ", StringComparison.Ordinal) || text.Contains(" bytes received", StringComparison.Ordinal))
            .Select(text => Convert.FromHexString(text[(text.LastIndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToArray();
        var published = Drain(subscription).OfType<ExchangeBytesEvent>().Select(bytes => bytes.Bytes.ToArray()).ToArray();

        return (notes, published);
    }

    private sealed class Http3RedactingConnectionServer(Action<IDecryptedBytesSink> report)
        : IConnectionProtocolServer, IExchangeBytesRedactingServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["http"];

        public Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            report(context.DecryptedBytes);

            return Task.CompletedTask;
        }

        public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) => new Http3RequestBytesRedactor();
    }
}
