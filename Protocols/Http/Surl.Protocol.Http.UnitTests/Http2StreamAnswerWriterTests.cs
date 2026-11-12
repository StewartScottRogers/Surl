using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2StreamAnswerWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeStreamAsync_BytesBody_SendsStatusLowercaseFieldsAndTheBytes()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: false, TestContext.CancellationToken);
        var answer = new HttpAnswer(HttpStatus.Ok, [new("Content-Type", "text/plain")], new HttpAnswerBody.Bytes(HttpServerHarness.Ascii("hi")), IsRefusal: false, LeavesRequestBodyUnread: false);
        var writer = new Http2StreamAnswerWriter(new FixedAnswerHandler(answer), Context());

        await writer.ServeStreamAsync(test.Stream, TestContext.CancellationToken);

        var response = Http2TestClient.Responses(test.Connection.WrittenBytes)[1];
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual("content-type", response.Fields[1].Name);
        Assert.AreEqual("hi", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
    }

    [TestMethod]
    public async Task ServeStreamAsync_BodyThatDoesNotAddUpToItsContentLength_ResetsTheStreamProtocolErrorUnanswered()
    {
        var test = new Http2TestStream(Http2TestStream.Get(contentLength: 5), isBodyAnnounced: true, TestContext.CancellationToken);
        test.Receive("abc");
        test.End();
        var answer = new HttpAnswer(HttpStatus.Ok, [], HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: false);
        var writer = new Http2StreamAnswerWriter(new FixedAnswerHandler(answer, readsBody: true), Context());

        await writer.ServeStreamAsync(test.Stream, TestContext.CancellationToken);

        var response = Http2TestClient.Responses(test.Connection.WrittenBytes)[1];
        Assert.AreEqual(Http2ErrorCode.ProtocolError, response.ResetCode);
        Assert.IsEmpty(response.HeaderBlocks);
        Assert.AreEqual("its DATA did not add up to its content-length", test.Resets.Single().Reason);
    }

    [TestMethod]
    public async Task ServeStreamAsync_AnswerThatDropsTheExchange_ResetsTheStreamInternalErrorUnanswered()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: false, TestContext.CancellationToken);
        var answer = new HttpAnswer(HttpStatus.Ok, [], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false, DropsExchange: true);
        var writer = new Http2StreamAnswerWriter(new FixedAnswerHandler(answer), Context());

        await writer.ServeStreamAsync(test.Stream, TestContext.CancellationToken);

        var response = Http2TestClient.Responses(test.Connection.WrittenBytes)[1];
        Assert.AreEqual(Http2ErrorCode.InternalError, response.ResetCode);
        Assert.IsEmpty(response.HeaderBlocks);
        Assert.AreEqual("a fault rule dropped the exchange", test.Resets.Single().Reason);
    }
    private ExchangeContext Context() => HttpServerHarness.Context(new RecordingExchangeLog(), TimeProvider.System, TestContext.CancellationToken);

    private sealed class FixedAnswerHandler(HttpAnswer answer, bool readsBody = false) : IHttpRequestHandler
    {
        public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            if (readsBody)
            {
                await request.Body.DiscardAsync(null, cancellationToken);
            }

            return answer;
        }
    }
}
