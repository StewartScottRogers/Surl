using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// <see cref="GrpcProtocolServer"/> on an in-memory connection: the HTTP/2 preface of RFC 9113 section 3.4
/// written by hand, then a gRPC call whose protobuf bytes come from the encoding guide.
/// </summary>
[TestClass]
public sealed class GrpcProtocolServerTests
{
    private const string Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n";
    private static readonly byte[] Hi = [0x0A, 0x02, 0x68, 0x69];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Schemes_AreGrpcAndGrpcs() =>
        CollectionAssert.AreEqual(new[] { "grpc", "grpcs" }, new GrpcProtocolServer(new AnonymousAuthenticationPolicy()).Schemes.ToArray());

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var server = new GrpcProtocolServer(new AnonymousAuthenticationPolicy());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_PrefaceThenUnaryEcho_AnswersTheCall()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] bytes =
        [
            .. System.Text.Encoding.ASCII.GetBytes(Preface),
            .. GrpcHttp2TestFrames.Start,
            .. client.Call(1, "/surl.test.Echo/Echo", endStream: false),
            .. GrpcHttp2TestFrames.Data(1, GrpcHttp2TestFrames.Message(Hi), endStream: true),
        ];
        var connection = new InMemoryConnection([bytes]);

        await new GrpcProtocolServer(new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context());

        var (fields, body, ended, _) = GrpcHttp2TestFrames.Response(connection.WrittenBytes, 1);
        StringAssert.EndsWith(GrpcHttp2TestFrames.Text(fields), "grpc-status: 0");
        CollectionAssert.AreEqual(GrpcHttp2TestFrames.Message(Hi), body);
        Assert.IsTrue(ended);
    }

    [TestMethod]
    public async Task ServeAsync_ReflectionServiceIsServed()
    {
        var client = new GrpcHttp2TestFrames();
        byte[] bytes =
        [
            .. System.Text.Encoding.ASCII.GetBytes(Preface),
            .. GrpcHttp2TestFrames.Start,
            .. client.Call(1, "/grpc.reflection.v1.ServerReflection/ServerReflectionInfo", endStream: false),
            // ServerReflectionRequest.list_services (field 7, LEN) = "*"
            .. GrpcHttp2TestFrames.Data(1, GrpcHttp2TestFrames.Message([0x3A, 0x01, 0x2A]), endStream: true),
        ];
        var connection = new InMemoryConnection([bytes]);

        await new GrpcProtocolServer(new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context());

        var (fields, body, _, _) = GrpcHttp2TestFrames.Response(connection.WrittenBytes, 1);
        StringAssert.EndsWith(GrpcHttp2TestFrames.Text(fields), "grpc-status: 0");
        StringAssert.Contains(System.Text.Encoding.ASCII.GetString(body), "surl.test.Echo");
    }

    [TestMethod]
    public async Task ServeAsync_NotThePreface_AnswersGoAwayProtocolErrorAndCloses()
    {
        var connection = new InMemoryConnection([System.Text.Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n")]);

        await new GrpcProtocolServer(new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context());

        byte[] goAway = [0, 0, 8, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];
        CollectionAssert.AreEqual(goAway, connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
    }

    private ExchangeContext Context() => new(
        1,
        new ListenUrl("grpc", "127.0.0.1", 80).WithBoundPort(80),
        new IPEndPoint(IPAddress.Loopback, 80),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        new ShortTimersOnlyTimeProvider(),
        TestContext.CancellationToken);

    [TestMethod]
    public void CreateRedactor_AuthorizationHeaderBlock_IsLoggedAsStars()
    {
        var redactor = new GrpcProtocolServer(new AnonymousAuthenticationPolicy()).CreateRedactor(new ExchangeRedactionContext(1, "grpc"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        redactor.Redact(Surl.Http2.Http2ConnectionPreface.Preface.ToArray(), output);
        redactor.Redact(new GrpcHttp2TestFrames().Call(1, "/surl.test.Echo/Echo", true, ("authorization", "Bearer sekret-token")), output);
        redactor.Flush(output);

        Assert.DoesNotContain("sekret", System.Text.Encoding.Latin1.GetString(output.WrittenSpan));
        Assert.Contains("****", System.Text.Encoding.Latin1.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void CreateRedactor_EachApiKeyAndSessionTokenHeaderBlock_IsLoggedWithItsPayloadAsStars()
    {
        var preface = Surl.Http2.Http2ConnectionPreface.Preface.ToArray();
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            var redactor = new GrpcProtocolServer(new AnonymousAuthenticationPolicy()).CreateRedactor(new ExchangeRedactionContext(1, "grpc"));
            var output = new System.Buffers.ArrayBufferWriter<byte>();
            var headers = new GrpcHttp2TestFrames().Call(1, "/surl.test.Echo/Echo", true, (name.ToLowerInvariant(), "sekret-key"));

            redactor.Redact(preface, output);
            redactor.Redact(headers, output);
            redactor.Flush(output);

            byte[] expected = [.. preface, .. headers[..9], .. "****"u8];
            CollectionAssert.AreEqual(expected, output.WrittenSpan.ToArray(), name);
        }
    }
}
