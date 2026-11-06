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

    [TestMethod]
    public async Task WriteRefusalAsync_AnswerThatDrainsNothingBeforeClose_WritesTheBodyAndLeavesTheRequestBodyUnread()
    {
        var connection = new InMemoryConnection([Ascii("unread body")]);
        var answer = new HttpAnswer(
            HttpStatus.BadRequest,
            [new("Content-Length", "3")],
            new HttpAnswerBody.Bytes(Ascii("abc")),
            IsRefusal: true,
            LeavesRequestBodyUnread: true,
            DrainsBeforeClose: false);

        var keepsConnectionOpen = await Writer(connection).WriteRefusalAsync(answer);

        Assert.IsFalse(keepsConnectionOpen);
        Assert.AreEqual("HTTP/1.1 400 Bad Request\r\nContent-Length: 3\r\nConnection: close\r\n\r\nabc", Latin1(connection.WrittenBytes));
        Assert.AreEqual("unread body", await ReadRestAsync(connection));
    }

    [TestMethod]
    public async Task WriteRefusalAsync_AnswerThatDrainsBeforeClose_ReadsWhatTheClientStillSends()
    {
        var connection = new InMemoryConnection([Ascii("unread body")]);
        var answer = new HttpAnswer(HttpStatus.BadRequest, [new("Content-Length", "0")], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: true);

        await Writer(connection).WriteRefusalAsync(answer);

        Assert.AreEqual(string.Empty, await ReadRestAsync(connection));
    }

    [TestMethod]
    public async Task WriteAsync_ClosingAnswerThatDrainsNothingBeforeClose_HalfClosesAndLeavesTheRequestBodyUnread()
    {
        var connection = new InMemoryConnection([Ascii("unread body")]);
        var answer = new HttpAnswer(
            HttpStatus.Ok,
            [new("Content-Length", "0")],
            HttpAnswerBody.None,
            IsRefusal: false,
            LeavesRequestBodyUnread: true,
            DrainsBeforeClose: false);

        var keepsConnectionOpen = await Writer(connection).WriteAsync(answer, new HttpRequestHead("PUT", "/", new Version(1, 1), [new HttpRequestField("Host", "a")]));

        Assert.IsFalse(keepsConnectionOpen);
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.AreEqual("unread body", await ReadRestAsync(connection));
    }

    // What the client sent that the writer left unread, read until the client half-closes.
    private async Task<string> ReadRestAsync(InMemoryConnection connection)
    {
        var rest = new List<byte>();
        var buffer = new byte[64];
        int read;
        while ((read = await connection.ReadAsync(buffer, TestContext.CancellationToken)) > 0)
        {
            rest.AddRange(buffer.AsSpan(0, read));
        }

        return Latin1([.. rest]);
    }

    private Http1AnswerWriter Writer(InMemoryConnection connection)
    {
        var context = Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken);
        var reader = new HttpConnectionReader(connection, context.Limits.MaxRequestHeadBytes);

        return new Http1AnswerWriter(connection, new HttpUnreadRequestDrainer(reader, context), context);
    }
}
