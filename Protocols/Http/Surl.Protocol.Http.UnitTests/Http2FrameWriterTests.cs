using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2FrameWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(20000, 2)]
    [DataRow(40000, 3)]
    public async Task WriteHeaderBlockAsync_BlockLongerThanOneFrame_GoesOnInContinuationsEndingTheLastOne(int valueLength, int frameCount)
    {
        var connection = new InMemoryConnection([]);
        var writer = new Http2FrameWriter(connection);
        HpackHeaderField[] fields = [new(":status", "200"), new("x-long", new string('~', valueLength), HpackFieldIndexing.WithoutIndexing)];

        await writer.WriteHeaderBlockAsync(1, fields, endStream: true, TestContext.CancellationToken);

        var frames = Http2TestClient.Frames(connection.WrittenBytes);
        Assert.HasCount(frameCount, frames);
        var headers = (Http2HeadersFrame)frames[0];
        Assert.IsTrue(headers.EndStream);
        Assert.IsFalse(headers.EndHeaders);
        Assert.HasCount(Http2FrameWriter.MaxPayloadLength, headers.FieldBlockFragment);
        var continuations = frames.Skip(1).Cast<Http2ContinuationFrame>().ToArray();
        Assert.IsTrue(continuations[^1].EndHeaders);
        Assert.IsTrue(continuations[..^1].All(continuation => !continuation.EndHeaders));
        Assert.AreEqual(valueLength, Http2TestClient.Responses(connection.WrittenBytes)[1].Fields[1].Value.Length);
    }
}
