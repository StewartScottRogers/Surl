using System.Buffers;
using System.Text;
using Surl.Hpack;
using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The http server's redactor moves to the HTTP/2 redactor after the prior-knowledge preface and
/// after <c>Upgrade: h2c</c>'s <c>101</c>, and never logs an HTTP/2 <c>authorization</c> value
/// (ADR-0182, decision 7, BL-1423).
/// </summary>
[TestClass]
public sealed class Http2ExchangeRedactionTests
{
    private static readonly ExchangeRedactionContext RedactionContext = new(1, "http");

    private static byte[] AuthorizedHeaders() =>
        Http2FrameEncoder.Encode(new Http2HeadersFrame(
            1,
            new HpackEncoder(stringCoding: HpackStringCoding.Raw).Encode(
                [new HpackHeaderField(":method", "GET"), new HpackHeaderField("authorization", "Bearer tok-h2-secret")]),
            true,
            true));

    private static byte[] Join(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    [TestMethod]
    public void CreateRedactor_PriorKnowledgeHttp2_WithholdsTheAuthorizationBlockAndPinsTheDump()
    {
        var redactor = Server().CreateRedactor(RedactionContext);
        var output = new ArrayBufferWriter<byte>();
        var frame = AuthorizedHeaders();

        redactor.Redact(Join(Http2ConnectionPreface.Preface.ToArray(), frame), output);
        redactor.Flush(output);

        CollectionAssert.AreEqual(
            Join(Http2ConnectionPreface.Preface.ToArray(), frame[..9], "****"u8.ToArray()),
            output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void CreateRedactor_PrefaceArrivingInSmallPieces_StillSwitchesToHttp2()
    {
        var redactor = Server().CreateRedactor(RedactionContext);
        var output = new ArrayBufferWriter<byte>();
        var stream = Join(Http2ConnectionPreface.Preface.ToArray(), AuthorizedHeaders());

        foreach (var value in stream)
        {
            redactor.Redact([value], output);
        }

        redactor.Flush(output);

        Assert.DoesNotContain("tok-h2-secret", Encoding.Latin1.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void CreateRedactor_H2cUpgrade_RedactsTheUpgradeHeadThenTheHttp2Frames()
    {
        var redactor = Server().CreateRedactor(RedactionContext);
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(Encoding.ASCII.GetBytes(
            "GET / HTTP/1.1\r\nHost: h\r\nAuthorization: Basic c2VjcmV0\r\nConnection: Upgrade, HTTP2-Settings\r\nUpgrade: h2c\r\nHTTP2-Settings: AAMAAABk\r\n\r\n"), output);
        redactor.ObserveSent("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n"u8);
        redactor.Redact(Join(Http2ConnectionPreface.Preface.ToArray(), AuthorizedHeaders()), output);
        redactor.Flush(output);
        var logged = Encoding.Latin1.GetString(output.WrittenSpan);

        Assert.DoesNotContain("c2VjcmV0", logged);
        Assert.DoesNotContain("tok-h2-secret", logged);
        Assert.Contains("Authorization: Basic ****", logged);
        Assert.Contains("PRI * HTTP/2.0", logged);
    }

    [TestMethod]
    public void ObserveSent_ConnectionAlreadyHttp2_IgnoresBytesThatStartAsAnUpgradeAnswer()
    {
        var redactor = Server().CreateRedactor(RedactionContext);
        var output = new ArrayBufferWriter<byte>();
        var frame = AuthorizedHeaders();

        redactor.Redact(Http2ConnectionPreface.Preface.ToArray(), output);
        redactor.ObserveSent("HTTP/1.1 101 Switching Protocols\r\n\r\n"u8);
        redactor.Redact(frame, output);
        redactor.Flush(output);

        CollectionAssert.AreEqual(
            Join(Http2ConnectionPreface.Preface.ToArray(), frame[..9], "****"u8.ToArray()),
            output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void CreateRedactor_Http1RequestThatStartsWithP_IsRedactedAsHttp1()
    {
        var redactor = Server().CreateRedactor(RedactionContext);
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(Encoding.ASCII.GetBytes("PUT /a HTTP/1.1\r\nHost: h\r\nAuthorization: Bearer tok1\r\nContent-Length: 0\r\n\r\n"), output);
        redactor.ObserveSent("HTTP/1.1 200 OK\r\n\r\n"u8);
        redactor.Flush(output);

        Assert.Contains("Authorization: Bearer ****", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void Flush_ConnectionEndedBeforeAnythingWasDecided_WritesTheBytesReceived()
    {
        var redactor = Server().CreateRedactor(RedactionContext);
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact("PR"u8, output);
        redactor.Flush(output);

        Assert.AreEqual("PR", Encoding.ASCII.GetString(output.WrittenSpan));
    }
}
