using Surl.Http2;
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

    [TestMethod]
    public async Task ServeStreamAsync_SignedRequestWithAnAuthority_ShowsTheLoginCheckTheAuthorityAsHost()
    {
        // The aws-sigv4-get fixture's signed fields (measured from pinned upstream curl over HTTP/1.1);
        // curl sends its Host as :authority over HTTP/2 (ADR-0043 amendment, BL-1548).
        const string Authorization = "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=b09d624160cf3e4eb31422de27d9396e3f9b96a7c7e51024bf516fe1323e0d9b";
        const string EmptyBodyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        KeyValuePair<string, string>[] fields =
        [
            new("authorization", Authorization),
            new("x-amz-date", "20260929T210307Z"),
            new("x-amz-content-sha256", EmptyBodyHash),
        ];
        var head = new Http2RequestHead("GET", "/x", "127.0.0.1:18122", fields, null, false);
        var test = new Http2TestStream(head, isBodyAnnounced: false, TestContext.CancellationToken);
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        var contentStore = new Surl.Content.ContentStore(HttpServerHarness.Root, HttpServerHarness.StandardFileSystem(), Surl.Content.ContentExposureOptions.ServeEverythingInsideTheRoot);
        var answerer = new HttpRequestAnswerer(Context(), contentStore, policy.StartHttpConnection(null));
        var writer = new Http2StreamAnswerWriter(answerer, Context());

        await writer.ServeStreamAsync(test.Stream, TestContext.CancellationToken);

        var judged = policy.JudgedRequests.Single();
        KeyValuePair<string, string>[] expected = [.. fields, new KeyValuePair<string, string>("host", "127.0.0.1:18122")];
        CollectionAssert.AreEqual(expected, judged.Fields.ToArray());
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
