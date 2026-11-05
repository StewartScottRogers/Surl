using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2StreamAnswerWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_BytesBody_SendsStatusLowercaseFieldsAndTheBytes()
    {
        var connection = new InMemoryConnection([]);
        var windows = new Http2SendWindows();
        windows.OpenStream(1);
        var writer = new Http2StreamAnswerWriter(new Http2FrameWriter(connection), windows, HttpServerHarness.Context(new RecordingExchangeLog(), TimeProvider.System, TestContext.CancellationToken));
        using var stream = new Http2Stream(1, new Http2RequestBody(false, null, false, 0, _ => Task.CompletedTask, () => Task.CompletedTask), TestContext.CancellationToken);
        var answer = new HttpAnswer(HttpStatus.Ok, [new("Content-Type", "text/plain")], new HttpAnswerBody.Bytes(HttpServerHarness.Ascii("hi")), IsRefusal: false, LeavesRequestBodyUnread: false);

        await writer.WriteAsync(stream, answer);

        var response = Http2TestClient.Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual("content-type", response.Fields[1].Name);
        Assert.AreEqual("hi", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
    }
}
