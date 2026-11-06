using Surl.Hpack;

namespace Surl.Http2;

[TestClass]
public sealed class Http2RequestStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    // RFC 9113 section 8.1: a response is one HEADERS (and its CONTINUATIONs), zero or more DATA,
    // and optionally one trailing HEADERS carrying the trailer section; the last frame carries
    // END_STREAM. Here the response head, two DATA and trailers, as a gRPC answer is shaped.
    [TestMethod]
    public async Task WriteTrailersAsync_AfterHeadersAndData_EndsTheStreamOnATrailingHeadersAsRfc9113Section81Says()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: false, TestContext.CancellationToken);
        var stream = test.Stream;

        await stream.WriteHeadersAsync([new(":status", "200"), new("content-type", "application/grpc")], endStream: false);
        await stream.WriteDataAsync(Http2TestBytes.Ascii("hello "), endStream: false);
        await stream.WriteDataAsync(Http2TestBytes.Ascii("world"), endStream: false);
        await stream.WriteTrailersAsync([new("grpc-status", "0"), new("grpc-message", "OK")]);

        var frames = Http2TestClient.Frames(test.Connection.WrittenBytes);
        Assert.HasCount(4, frames);
        var head = (Http2HeadersFrame)frames[0];
        Assert.IsTrue(head.EndHeaders);
        Assert.IsFalse(head.EndStream, "The response head leaves the stream open for DATA.");
        var data = frames.Skip(1).Take(2).Cast<Http2DataFrame>().ToArray();
        Assert.IsTrue(data.All(frame => !frame.EndStream && frame.StreamId == 1), "No DATA frame ends the stream.");
        var trailers = (Http2HeadersFrame)frames[3];
        Assert.AreEqual(1, trailers.StreamId);
        Assert.IsTrue(trailers.EndStream, "The trailing HEADERS carries END_STREAM.");
        Assert.IsTrue(trailers.EndHeaders);
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, int.MaxValue);
        var headFields = decoder.Decode(head.FieldBlockFragment).Fields;
        var trailerFields = decoder.Decode(trailers.FieldBlockFragment).Fields;
        Assert.AreEqual(":status", headFields[0].Name);
        Assert.AreEqual("200", headFields[0].Value);
        Assert.AreEqual("hello world", string.Concat(data.Select(frame => Http2TestBytes.Latin1(frame.Data))));
        CollectionAssert.AreEqual(new[] { "grpc-status: 0", "grpc-message: OK" }, trailerFields.Select(field => $"{field.Name}: {field.Value}").ToArray());
        Assert.IsFalse(trailerFields.Any(field => field.Name.StartsWith(':')), "Trailers carry no pseudo-header field.");
    }

    [TestMethod]
    public async Task WriteDataAsync_EndStream_EndsTheStreamOnItsLastFrame()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: false, TestContext.CancellationToken);

        await test.Stream.WriteDataAsync(Http2TestBytes.Ascii("hi"), endStream: true);

        var data = (Http2DataFrame)Http2TestClient.Frames(test.Connection.WrittenBytes).Single();
        Assert.IsTrue(data.EndStream);
        Assert.AreEqual("hi", Http2TestBytes.Latin1(data.Data));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WriteDataAsync_Empty_WritesOneEmptyDataFrameWithoutWaitingForWindow(bool endStream)
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: false, TestContext.CancellationToken, initialWindowSize: 0);

        await test.Stream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream);

        var data = (Http2DataFrame)Http2TestClient.Frames(test.Connection.WrittenBytes).Single();
        Assert.IsEmpty(data.Data);
        Assert.AreEqual(endStream, data.EndStream);
    }

    [TestMethod]
    public async Task ReadBodyAsync_RequestBody_ReadsItsPiecesAndSaysHowItEnded()
    {
        var test = new Http2TestStream(Http2TestStream.Get(contentLength: 4), isBodyAnnounced: true, TestContext.CancellationToken);
        var stream = test.Stream;
        test.Receive("ab");
        test.End();

        var piece = await stream.ReadBodyAsync(TestContext.CancellationToken);
        var end = await stream.ReadBodyAsync(TestContext.CancellationToken);

        Assert.AreEqual("ab", Http2TestBytes.Latin1(piece!));
        Assert.IsNull(end);
        Assert.AreEqual(1, stream.Id);
        Assert.AreEqual("GET", stream.Head.Method);
        Assert.IsTrue(stream.IsRequestBodyAnnounced);
        Assert.IsFalse(stream.IsRequestBodyStillArriving);
        Assert.IsFalse(stream.HasRequestBodyEndedEarly);
        Assert.IsTrue(stream.HasContentLengthMismatch);
        CollectionAssert.AreEqual(new[] { 2 }, test.Replenished);
    }

    [TestMethod]
    public async Task ResetAsync_WritesRstStreamThroughTheConnectionWithItsReason()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: true, TestContext.CancellationToken);

        await test.Stream.ResetAsync(Http2ErrorCode.Cancel, "the handler gave up");

        var reset = (Http2RstStreamFrame)Http2TestClient.Frames(test.Connection.WrittenBytes).Single();
        Assert.AreEqual(Http2ErrorCode.Cancel, reset.ErrorCode);
        Assert.AreEqual((Http2ErrorCode.Cancel, "the handler gave up"), test.Resets.Single());
        Assert.IsTrue(test.Stream.HasRequestBodyEndedEarly);
    }
}
