using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using static Surl.HttpServing.HttpServingHarness;

namespace Surl.HttpServing;

/// <summary>
/// <see cref="Http1AnswerWriter"/> turns an <see cref="HttpAnswer"/> into HTTP/1.x bytes; the
/// recorded fixtures in the server's own tests pin every answer the server gives today, and
/// these pin the body the answerer does not give yet.
/// </summary>
[TestClass]
public sealed class Http1AnswerWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_BytesBody_WritesTheHeadThenTheBytesAndKeepsAnHttp11ConnectionOpen()
    {
        var connection = new InMemoryConnection([]);
        var answer = new HttpAnswer(
            HttpStatus.Ok,
            [new("Content-Length", "3")],
            new HttpAnswerBody.Bytes(Ascii("abc")),
            IsRefusal: false,
            LeavesRequestBodyUnread: false);

        var keepsConnectionOpen = await Writer(connection).WriteAsync(answer, new HttpRequestHead("GET", "/", new Version(1, 1), [new HttpRequestField("Host", "a")]));

        Assert.IsTrue(keepsConnectionOpen);
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc", Latin1(connection.WrittenBytes));
    }

    private Http1AnswerWriter Writer(InMemoryConnection connection)
    {
        var context = Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken);
        var reader = new HttpConnectionReader(connection, context.Limits.MaxRequestHeadBytes);

        return new Http1AnswerWriter(connection, new HttpUnreadRequestDrainer(reader, context), context);
    }
}
