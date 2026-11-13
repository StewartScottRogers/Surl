using System.IO.Compression;
using System.Net;
using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Grpc.GrpcHttp2TestFrames;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The gRPC server on HTTP/2 (PROTOCOL-HTTP2 "Requests" and "Responses", statuscodes, compression;
/// ADR-0116 decisions 1 to 8), driven through <see cref="Http2ConnectionServer"/> with frames and
/// HPACK-encoded headers written by hand from those documents and RFC 9113. Every protobuf byte is
/// written from the encoding guide: <c>0A 02 68 69</c> is field 1, <c>LEN</c>, "hi".
/// </summary>
[TestClass]
public sealed class GrpcStreamHandlerTests
{
    private const string AcceptEncoding = "grpc-accept-encoding: identity,deflate,gzip";
    private const string ReplyHeaders = ":status: 200\ncontent-type: application/grpc\n" + AcceptEncoding;
    private const string EchoPath = "/surl.test.Echo/Echo";
    private const string ExpandPath = "/surl.test.Echo/Expand";
    private const string CollectPath = "/surl.test.Echo/Collect";
    private const string ChatPath = "/surl.test.Echo/Chat";

    private static readonly byte[] Hi = [0x0A, 0x02, 0x68, 0x69];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var session = UnitTestHttpAuthenticationSession.ProceedingEveryRequest;
        var context = Context();

        Assert.ThrowsExactly<ArgumentNullException>(() => new GrpcStreamHandler(null!, session, context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GrpcStreamHandler([], null!, context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GrpcStreamHandler([], session, null!));
    }

    [TestMethod]
    public async Task ServeStreamAsync_NullStream_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Handler().ServeStreamAsync(null!, TestContext.CancellationToken));

    [TestMethod]
    public async Task ServeStreamAsync_UnaryEcho_RepliesOnceThenTrailersWithGrpcStatus0()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, ended, resetCode) = await CallAsync(client.Call(1, EchoPath, endStream: false), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);
        Assert.IsTrue(ended);
        Assert.IsNull(resetCode);
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnaryEchoWithUnknownFieldAndContentTypeParameter_SkipsTheFieldAndAnswersInGrpcProto()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] request = [.. Hi, 0x48, 0x01];

        var (fields, body, _, _) = await CallAsync(
            client.Request(1, "POST", EchoPath, endStream: false, ("te", "trailers"), ("content-type", "application/grpc+proto; charset=utf-8")),
            Data(1, Message(request), endStream: true));

        Assert.AreEqual(":status: 200\ncontent-type: application/grpc+proto\n" + AcceptEncoding + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_CustomMetadataAsciiAndBinary_IsAcceptedAndNeverEchoed()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, _, _) = await CallAsync(
            client.Call(1, EchoPath, endStream: false, ("user-agent", "grpc-test/1"), ("x-request-id", "42"), ("x-trace-bin", "AAEC")),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);
    }

    [TestMethod]
    [DataRow("content-type: application/grpc+json", "", "", "", "", "/x/y", 12, "content-type not served", DisplayName = "the codec before te")]
    [DataRow("content-type: application/grpc", "", "authorization: Digest a", "", "", "/x/y", 13, "te: trailers required", DisplayName = "te before authentication")]
    [DataRow("content-type: application/grpc", "te: trailers", "authorization: Digest a", "grpc-timeout: 1x", "", "/x/y", 16, "unauthenticated", DisplayName = "authentication before grpc-timeout")]
    [DataRow("content-type: application/grpc", "te: trailers", "", "grpc-timeout: 1x", "grpc-encoding: br", "/x/y", 13, "malformed grpc-timeout", DisplayName = "grpc-timeout before grpc-encoding")]
    [DataRow("content-type: application/grpc", "te: trailers", "", "", "grpc-encoding: br", "/x/y", 12, "grpc-encoding not served", DisplayName = "grpc-encoding before the path")]
    public async Task ServeStreamAsync_SeveralFailures_AnswersTheFirstInDecision3sOrder(
        string contentType, string te, string authorization, string timeout, string encoding, string path, int code, string message)
    {
        var client = new GrpcHttp2TestFrames();
        (string, string)[] fields =
        [
            .. new[] { contentType, te, authorization, timeout, encoding }
                .Where(field => field.Length > 0)
                .Select(field => field.Split(": ", 2))
                .Select(parts => (parts[0], parts[1])),
        ];

        var response = await CallAsync(client.Request(1, "POST", path, endStream: false, fields), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(code, message), Text(response.Fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_ServerStreamingExpandWithCount3_SendsThreeMessagesIndexed0To2()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] request = [.. Hi, 0x10, 0x03];

        var (fields, body, ended, _) = await CallAsync(client.Call(1, ExpandPath, endStream: false), Data(1, Message(request), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(
            (byte[])[.. Message(Hi), .. Message([.. Hi, 0x10, 0x01]), .. Message([.. Hi, 0x10, 0x02])],
            body);
        Assert.IsTrue(ended);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ExpandWithAStatusCode_SendsItInTheTrailersAfterTheMessages()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] request = [.. Hi, 0x10, 0x01, 0x18, 0x09, 0x22, 0x02, 0x6E, 0x6F];

        var (fields, body, _, _) = await CallAsync(client.Call(1, ExpandPath, endStream: false), Data(1, Message(request), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 9\ngrpc-message: no", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientStreamingCollect_RepliesOnceWithTheMessagesConcatenatedAndTheirCount()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, _, _) = await CallAsync(
            client.Call(1, CollectPath, endStream: false),
            Data(1, Message([0x0A, 0x02, 0x61, 0x62]), endStream: false),
            Data(1, Message([0x0A, 0x02, 0x63, 0x64])[..3], endStream: false),
            Data(1, Message([0x0A, 0x02, 0x63, 0x64])[3..], endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message([0x0A, 0x04, 0x61, 0x62, 0x63, 0x64, 0x10, 0x02]), body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_CollectWithNoRequest_RepliesWithAnEmptyMessage()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, _, _) = await CallAsync(client.Call(1, CollectPath, endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message([]), body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_CollectWithAStatusCode_EndsAtOnceAsTrailersOnly()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, _, _) = await CallAsync(
            client.Call(1, CollectPath, endStream: false),
            Data(1, Message(Hi), endStream: false),
            Data(1, Message([0x18, 0x0E]), endStream: true));

        Assert.AreEqual(TrailersOnly(14), Text(fields));
        Assert.IsEmpty(body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_CollectPastTheMessageLimit_IsResourceExhaustedReplyLargerThanTheLimit()
    {
        var client = new GrpcHttp2TestFrames();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 4 };

        var (fields, _, _, _) = await CallAsync(
            Handler(context: Context() with { Limits = limits }),
            client.Call(1, CollectPath, endStream: false),
            Data(1, Message(Hi), endStream: false),
            Data(1, Message(Hi), endStream: false),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(8, "reply larger than the limit"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_BidirectionalChat_RepliesToEachRequestIndexedByPosition()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] delayed = [.. Hi, 0x28, 0x01];

        var (fields, body, _, _) = await CallAsync(
            client.Call(1, ChatPath, endStream: false),
            Data(1, Message(delayed), endStream: false),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual((byte[])[.. Message(Hi), .. Message([.. Hi, 0x10, 0x01])], body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ChatWithAStatusCodeAfterAReply_EndsWithItInTheTrailers()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, _, _) = await CallAsync(
            client.Call(1, ChatPath, endStream: false),
            Data(1, Message(Hi), endStream: false),
            Data(1, Message([0x18, 0x0A]), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 10", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_EchoWithAStatusCodeAndMessage_IsTrailersOnlyWithThePercentEncodedMessage()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] request = [0x18, 0x05, 0x22, 0x04, 0x67, 0x6F, 0x6E, 0x25];

        var (fields, body, ended, _) = await CallAsync(client.Call(1, EchoPath, endStream: false), Data(1, Message(request), endStream: true));

        Assert.AreEqual(TrailersOnly(5, "gon%25"), Text(fields));
        Assert.IsEmpty(body);
        Assert.IsTrue(ended);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x10, 0xE9, 0x07 }, DisplayName = "count 1001")]
    [DataRow(new byte[] { 0x18, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, DisplayName = "status_code -1")]
    [DataRow(new byte[] { 0x18, 0x11 }, DisplayName = "status_code 17")]
    [DataRow(new byte[] { 0x28, 0xE1, 0xD4, 0x03 }, DisplayName = "delay_milliseconds 60001")]
    public async Task ServeStreamAsync_EchoWithAFieldOutOfRange_IsInvalidArgument(byte[] request)
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false), Data(1, Message(request), endStream: true));

        Assert.AreEqual(TrailersOnly(3, "request field out of range"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_EchoWhoseMessageIsNotProtobuf_IsInternalRequestMessageDidNotParse()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false), Data(1, Message([0x0A, 0x05, 0x68]), endStream: true));

        Assert.AreEqual(TrailersOnly(13, "request message did not parse"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_BodyEndingInsideAMessage_IsInternalRequestMessageDidNotParse()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false), Data(1, Message(Hi)[..7], endStream: true));

        Assert.AreEqual(TrailersOnly(13, "request message did not parse"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnaryWithNoRequestMessage_IsUnimplementedRequestCardinalityViolation()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: true));

        Assert.AreEqual(TrailersOnly(12, "request cardinality violation"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnaryWithTwoRequestMessages_IsUnimplementedRequestCardinalityViolation()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false), Data(1, [.. Message(Hi), .. Message(Hi)], endStream: true));

        Assert.AreEqual(TrailersOnly(12, "request cardinality violation"), Text(fields));
    }

    [TestMethod]
    [DataRow("/surl.test.Echo/Missing", "unknown method")]
    [DataRow("/grpc.health.v1.Health/Check", "unknown service")]
    [DataRow("/surl.test.Echo", "unknown service")]
    public async Task ServeStreamAsync_UnknownServiceOrMethod_IsTrailersOnlyUnimplemented(string path, string message)
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, ended, _) = await CallAsync(client.Call(1, path, endStream: false), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(12, message), Text(fields));
        Assert.IsEmpty(body);
        Assert.IsTrue(ended);
    }

    [TestMethod]
    public async Task ServeStreamAsync_Get_Is405WithAllowPostAndNoGrpcStatus()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, ended, _) = await CallAsync(client.Request(1, "GET", EchoPath, endStream: true, ("te", "trailers")));

        Assert.AreEqual(":status: 405\nallow: POST", Text(fields));
        Assert.IsTrue(ended);
    }

    [TestMethod]
    [DataRow("text/plain")]
    [DataRow(null)]
    public async Task ServeStreamAsync_ContentTypeNotGrpc_Is415WithNoGrpcStatus(string? contentType)
    {
        var client = new GrpcHttp2TestFrames();
        (string, string)[] fields = contentType is null ? [("te", "trailers")] : [("te", "trailers"), ("content-type", contentType)];

        var response = await CallAsync(client.Request(1, "POST", EchoPath, endStream: false, fields), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(":status: 415", Text(response.Fields));
        Assert.IsTrue(response.Ended);
    }

    [TestMethod]
    public async Task ServeStreamAsync_GrpcJsonContentType_IsTrailersOnlyUnimplementedContentTypeNotServed()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(
            client.Request(1, "POST", EchoPath, endStream: false, ("te", "trailers"), ("content-type", "application/grpc+json")),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(12, "content-type not served"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_MissingTeTrailers_IsTrailersOnlyInternalTeTrailersRequired()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(
            client.Request(1, "POST", EchoPath, endStream: false, ("content-type", "application/grpc")),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(13, "te: trailers required"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_GrpcTimeoutOf1MillisecondThatPasses_IsDeadlineExceeded()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] request = [.. Hi, 0x28, 0xE0, 0xD4, 0x03];

        var (fields, body, ended, _) = await CallAsync(client.Call(1, EchoPath, endStream: false, ("grpc-timeout", "1m")), Data(1, Message(request), endStream: true));

        Assert.AreEqual(TrailersOnly(4, "deadline exceeded"), Text(fields));
        Assert.IsEmpty(body);
        Assert.IsTrue(ended);
    }

    [TestMethod]
    public async Task ServeStreamAsync_GrpcTimeoutOf0_IsDeadlineExceededAtOnce()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false, ("grpc-timeout", "0S")), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(4, "deadline exceeded"), Text(fields));
    }

    [TestMethod]
    [DataRow("5S")]
    [DataRow("99999999H")]
    public async Task ServeStreamAsync_GrpcTimeoutThatDoesNotPass_IsServed(string timeout)
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false, ("grpc-timeout", timeout)), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_MalformedGrpcTimeout_IsTrailersOnlyInternal()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false, ("grpc-timeout", "1x")), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(13, "malformed grpc-timeout"), Text(fields));
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    public async Task ServeStreamAsync_CompressedRequest_IsDecompressedAndAnsweredUncompressed(string encoding)
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, body, _, _) = await CallAsync(
            client.Call(1, EchoPath, endStream: false, ("grpc-encoding", encoding)),
            Data(1, Message(Compress(Hi, encoding), compressed: true), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnsupportedGrpcEncoding_IsUnimplementedWithGrpcAcceptEncoding()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false, ("grpc-encoding", "br")), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(12, "grpc-encoding not served"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_CompressedFlagWithoutGrpcEncoding_IsInternal()
    {
        var client = new GrpcHttp2TestFrames();

        var (fields, _, _, _) = await CallAsync(client.Call(1, EchoPath, endStream: false), Data(1, Message(Hi, compressed: true), endStream: true));

        Assert.AreEqual(TrailersOnly(13, "compressed flag set without grpc-encoding"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnauthenticatedCall_IsUnauthenticatedWithTheChallengeAsMetadata()
    {
        var client = new GrpcHttp2TestFrames();
        var session = new UnitTestHttpAuthenticationSession(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, ["Basic realm=\"surl\""], null));

        var (fields, body, _, _) = await CallAsync(Handler(session), client.Call(1, EchoPath, endStream: false), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(16, "unauthenticated") + "\nwww-authenticate: Basic realm=\"surl\"", Text(fields));
        Assert.IsEmpty(body);
        Assert.AreEqual(new HttpAuthenticationRequest("POST", EchoPath, false, session.JudgedRequests[0].Fields), session.JudgedRequests[0]);
    }

    [TestMethod]
    public async Task ServeStreamAsync_AuthorizationNeitherBasicNorBearer_IsUnauthenticatedWithoutAskingTheSession()
    {
        var client = new GrpcHttp2TestFrames();
        var session = UnitTestHttpAuthenticationSession.ProceedingEveryRequest;

        var (fields, _, _, _) = await CallAsync(
            Handler(session),
            client.Call(1, EchoPath, endStream: false, ("authorization", "Digest username=\"a\"")),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(16, "unauthenticated"), Text(fields));
        Assert.IsEmpty(session.JudgedRequests);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForbiddenCall_IsPermissionDenied()
    {
        var client = new GrpcHttp2TestFrames();
        var session = new UnitTestHttpAuthenticationSession(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], "alice"));

        var (fields, _, _, _) = await CallAsync(
            Handler(session),
            client.Call(1, EchoPath, endStream: false, ("authorization", "Basic YWxpY2U6c2VjcmV0")),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(7, "permission denied"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_BearerTokenTheSessionAccepts_IsServedAndTheSessionSeesTheAuthorization()
    {
        var client = new GrpcHttp2TestFrames();
        var session = UnitTestHttpAuthenticationSession.ProceedingEveryRequest;

        var (fields, _, _, _) = await CallAsync(
            Handler(session),
            client.Call(1, EchoPath, endStream: false, ("authorization", "Bearer token-1")),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        Assert.Contains(new KeyValuePair<string, string>("authorization", "Bearer token-1"), session.JudgedRequests[0].Fields);
    }

    [TestMethod]
    public async Task ServeStreamAsync_MessagePastTheMessageLimit_IsResourceExhausted()
    {
        var client = new GrpcHttp2TestFrames();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 3 };

        var (fields, _, _, _) = await CallAsync(
            Handler(context: Context() with { Limits = limits }),
            client.Call(1, EchoPath, endStream: false),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(8, "message larger than the limit"), Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_CallPastTheUploadLimit_IsResourceExhaustedCallLargerThanTheLimit()
    {
        var client = new GrpcHttp2TestFrames();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 6 };

        var (fields, _, _, _) = await CallAsync(
            Handler(context: Context() with { Limits = limits }),
            client.Call(1, CollectPath, endStream: false),
            Data(1, [.. Message(Hi), .. Message(Hi)], endStream: true));

        Assert.AreEqual(TrailersOnly(8, "call larger than the limit"), Text(fields));
    }

    [TestMethod]
    [DataRow(0L, 0L)]
    [DataRow(3_000_000_000L, 3_000_000_000L)]
    public async Task ServeStreamAsync_NoOrHugeLimits_IsServed(long maxMessageBytes, long maxUploadBytes)
    {
        var client = new GrpcHttp2TestFrames();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = maxMessageBytes, MaxUploadBytes = maxUploadBytes };

        var (fields, _, _, _) = await CallAsync(
            Handler(context: Context() with { Limits = limits }),
            client.Call(1, EchoPath, endStream: false),
            Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientResetsTheStream_WritesNothingMoreAndLogsCancelled()
    {
        var client = new GrpcHttp2TestFrames();
        var log = new RecordingExchangeLog();

        var (fields, body, _, _) = await CallAsync(
            Handler(log: log),
            client.Call(1, CollectPath, endStream: false),
            Data(1, Message(Hi), endStream: false),
            Cancel(1));

        Assert.IsEmpty(fields);
        Assert.IsEmpty(body);
        Assert.Contains("gRPC call /surl.test.Echo/Collect CANCELLED by the client.", log.Notes);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled()
    {
        var client = new GrpcHttp2TestFrames();
        var log = new RecordingExchangeLog();

        var (fields, _, _, _) = await CallAsync(
            Handler(log: log),
            client.Call(1, ChatPath, endStream: false),
            Data(1, Message(Hi)[..6], endStream: false));

        Assert.IsEmpty(fields);
        Assert.Contains("gRPC call /surl.test.Echo/Chat CANCELLED by the client.", log.Notes);
    }

    // ADR-0116 decision 4: a call still open at shutdown ends UNAVAILABLE "server shutting down",
    // then the connection's GOAWAY NO_ERROR (PROTOCOL-HTTP2 "Connection Management").
    [TestMethod]
    public async Task ServeStreamAsync_ShutdownWhileAChatCallIsOpen_EndsItUnavailableServerShuttingDownThenGoAwayNoError()
    {
        var client = new GrpcHttp2TestFrames();
        var log = new RecordingExchangeLog();

        var written = await ShutDownMidCallAsync(
            log,
            written => Response(written, 1).Body.Length > 0,
            Start,
            client.Call(1, ChatPath, endStream: false),
            Data(1, Message(Hi), endStream: false));

        var (fields, body, ended, resetCode) = Response(written, 1);
        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 14\ngrpc-message: server shutting down", Text(fields));
        Assert.IsNotEmpty(body);
        Assert.IsTrue(ended);
        Assert.IsNull(resetCode);
        CollectionAssert.AreEqual(GoAwayNoError(1), written[^GoAwayNoError(1).Length..]);
        Assert.Contains("gRPC call /surl.test.Echo/Chat ended UNAVAILABLE: the exchange was cancelled while it was open.", log.Notes);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable()
    {
        var client = new GrpcHttp2TestFrames();
        var methodIsWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new GrpcService("test.Probe", [new("Wait", true, false, async (call, cancellationToken) =>
        {
            methodIsWaiting.SetResult();
            await call.ReadRequestAsync(cancellationToken);
            return GrpcStatus.Ok;
        })]);

        // The method's own signal is the only point at which the call is certainly open and
        // has written nothing; the server's WINDOW_UPDATE is written before the stream sees its DATA.
        var written = await ShutDownMidCallAsync(
            new RecordingExchangeLog(),
            methodIsWaiting.Task,
            [service],
            Start,
            client.Call(1, "/test.Probe/Wait", endStream: false));

        var (fields, _, ended, _) = Response(written, 1);
        Assert.AreEqual(TrailersOnly(14, "server shutting down"), Text(fields));
        Assert.IsTrue(ended);
        CollectionAssert.AreEqual(GoAwayNoError(1), written[^GoAwayNoError(1).Length..]);
    }

    // ADR-0116 decision 4: a reply cut off mid-write is RST_STREAM CANCEL (PROTOCOL-HTTP2 "Errors").
    // The client's SETTINGS_INITIAL_WINDOW_SIZE 0 (RFC 9113 section 6.5.2) holds the reply's DATA back.
    [TestMethod]
    public async Task ServeStreamAsync_ShutdownWhileAReplyWaitsForWindow_ResetsTheStreamCancel()
    {
        var client = new GrpcHttp2TestFrames();
        var zeroWindow = Frame(SettingsType, 0, 0, [0x00, 0x04, 0x00, 0x00, 0x00, 0x00]);

        var written = await ShutDownMidCallAsync(
            new RecordingExchangeLog(),
            written => Response(written, 1).Fields.Count > 0,
            zeroWindow,
            client.Call(1, EchoPath, endStream: false),
            Data(1, Message(Hi), endStream: true));

        var (fields, body, ended, resetCode) = Response(written, 1);
        Assert.AreEqual(ReplyHeaders, Text(fields));
        Assert.IsEmpty(body);
        Assert.IsFalse(ended);
        Assert.AreEqual(0x8u, resetCode);
        CollectionAssert.AreEqual(GoAwayNoError(1), written[^GoAwayNoError(1).Length..]);
    }

    [TestMethod]
    public async Task ServeStreamAsync_MethodThatThrowsUnexpectedly_IsUnknownServerErrorAndLogged()
    {
        var client = new GrpcHttp2TestFrames();
        var log = new RecordingExchangeLog();
        var service = new GrpcService("test.Probe", [new("Throw", false, false, (_, _) => throw new InvalidOperationException("boom"))]);

        var (fields, _, _, _) = await CallAsync(Handler(log: log, services: [service]), client.Call(1, "/test.Probe/Throw", endStream: false), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(TrailersOnly(2, "server error"), Text(fields));
        Assert.Contains("gRPC call ended UNKNOWN: the service threw InvalidOperationException: boom", log.Notes);
        Assert.Contains("gRPC call /test.Probe/Throw, metadata: te, content-type", log.Notes);
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnaryMethodReadingTwice_GetsTheOneRequestThenNull()
    {
        var client = new GrpcHttp2TestFrames();
        var service = new GrpcService("test.Probe", [new("ReadTwice", false, false, ReadTwiceAsync)]);

        var (fields, body, _, _) = await CallAsync(Handler(services: [service]), client.Call(1, "/test.Probe/ReadTwice", endStream: false), Data(1, Message(Hi), endStream: true));

        Assert.AreEqual(ReplyHeaders + "\ngrpc-status: 0", Text(fields));
        CollectionAssert.AreEqual(Message(Hi), body);

        static async Task<GrpcStatus> ReadTwiceAsync(GrpcCall call, CancellationToken cancellationToken)
        {
            var first = await call.ReadRequestAsync(cancellationToken);
            var second = await call.ReadRequestAsync(cancellationToken);
            await call.WriteReplyAsync(first!.Value);
            Assert.AreEqual(1048576, call.MaximumMessageLength);
            return second is null ? GrpcStatus.Ok : GrpcStatus.ServerError;
        }
    }

    private static string TrailersOnly(int code, string? message = null) =>
        ":status: 200\ncontent-type: application/grpc\n"
        + $"grpc-status: {code}\n"
        + (message is null ? string.Empty : $"grpc-message: {message}\n")
        + AcceptEncoding;

    private static byte[] Compress(byte[] payload, string encoding)
    {
        using var compressed = new MemoryStream();
        using (Stream compressing = encoding == "gzip"
            ? new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true)
            : new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            compressing.Write(payload);
        }

        return compressed.ToArray();
    }

    private Task<(IReadOnlyList<Hpack.HpackHeaderField> Fields, byte[] Body, bool Ended, uint? ResetCode)> CallAsync(params byte[][] frames) =>
        CallAsync(Handler(), frames);

    private async Task<(IReadOnlyList<Hpack.HpackHeaderField> Fields, byte[] Body, bool Ended, uint? ResetCode)> CallAsync(GrpcStreamHandler handler, params byte[][] frames)
    {
        var connection = new InMemoryConnection([(byte[])[.. Start, .. frames.SelectMany(frame => frame)]]);

        await new Http2ConnectionServer(handler, Context(), connection).ServeAsync();

        return Response(connection.WrittenBytes, 1);
    }

    /// <summary><c>GOAWAY</c> naming <paramref name="lastStreamId"/>, <c>NO_ERROR</c>, no debug data (RFC 9113 section 6.8).</summary>
    private static byte[] GoAwayNoError(int lastStreamId) =>
        Frame(0x7, 0, 0, [0, 0, 0, (byte)lastStreamId, 0, 0, 0, 0]);

    // Serves the frames on a connection the client keeps open, shuts the exchange down once what
    // was written satisfies isAnswering, and returns every byte written by the end.
    private async Task<byte[]> ShutDownMidCallAsync(RecordingExchangeLog log, Func<byte[], bool> isAnswering, params byte[][] frames) =>
        await ShutDownMidCallAsync(log, isAnswering, null, null, frames);

    // The same, but shuts down once untilOpen completes, which the served method completes itself.
    private async Task<byte[]> ShutDownMidCallAsync(RecordingExchangeLog log, Task untilOpen, GrpcService[] services, params byte[][] frames) =>
        await ShutDownMidCallAsync(log, null, untilOpen, services, frames);

    private async Task<byte[]> ShutDownMidCallAsync(RecordingExchangeLog log, Func<byte[], bool>? isAnswering, Task? untilOpen, GrpcService[]? services, byte[][] frames)
    {
        var connection = new InMemoryConnection([(byte[])[.. frames.SelectMany(frame => frame)]], peerHalfClosesWhenExhausted: false);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = Context() with { Log = log, CancellationToken = shutdown.Token, ShutdownToken = shutdown.Token };
        var serving = new Http2ConnectionServer(Handler(context: context, log: log, services: services), context, connection).ServeAsync();

        if (untilOpen is not null)
        {
            await untilOpen.WaitAsync(TestContext.CancellationToken);
        }

        while (isAnswering is not null && !isAnswering(connection.WrittenBytes))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.CancellationToken);
        }

        await shutdown.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);

        return connection.WrittenBytes;
    }

    private GrpcStreamHandler Handler(
        IHttpAuthenticationSession? session = null,
        ExchangeContext? context = null,
        RecordingExchangeLog? log = null,
        GrpcService[]? services = null)
    {
        var exchange = (context ?? Context()) with { Log = log ?? new RecordingExchangeLog() };
        return new GrpcStreamHandler(services ?? [EchoService.Service], session ?? UnitTestHttpAuthenticationSession.ProceedingEveryRequest, exchange);
    }

    private ExchangeContext Context() => new(
        1,
        new ListenUrl("grpc", "127.0.0.1", 80).WithBoundPort(80),
        new IPEndPoint(IPAddress.Loopback, 80),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        new ShortTimersOnlyTimeProvider(),
        TestContext.CancellationToken);
}
