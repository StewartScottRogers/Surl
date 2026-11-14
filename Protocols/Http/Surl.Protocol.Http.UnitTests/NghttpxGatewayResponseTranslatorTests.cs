using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// ADR-0154 decision 3: the response nghttpx builds from the backend's reply. Case 2403 is upstream's
/// <c>&lt;data&gt;</c> reply and the <c>&lt;stdout&gt;</c> fields it must become.
/// </summary>
[TestClass]
public sealed class NghttpxGatewayResponseTranslatorTests
{
    private const string Case2403Reply =
        "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nContent-Length: 6\r\nConnection: close\r\n"
        + "Content-Type: text/html\r\nFunny-head: yesyes\r\n\r\n-foo-\n";

    [TestMethod]
    public void Translate_Case2403_Http2_GivesTheStdoutFieldsInOrder()
    {
        NghttpxGatewayResponse response = Translate(NghttpxGatewayVersion.Http2, Case2403Reply);

        Assert.AreEqual(200, response.Status);
        CollectionAssert.AreEqual(
            new[]
            {
                "date: Tue, 09 Nov 2010 14:49:00 GMT",
                "content-length: 6",
                "content-type: text/html",
                "funny-head: yesyes",
                "server: nghttpx",
                "via: 1.1 nghttpx",
            },
            Lines(response));
        Assert.AreEqual("-foo-\n", Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    public void Translate_Http3_AddsNoServerField()
    {
        NghttpxGatewayResponse response = Translate(NghttpxGatewayVersion.Http3, Case2403Reply);

        CollectionAssert.AreEqual(
            new[]
            {
                "date: Tue, 09 Nov 2010 14:49:00 GMT",
                "content-length: 6",
                "content-type: text/html",
                "funny-head: yesyes",
                "via: 1.1 nghttpx",
            },
            Lines(response));
    }

    [TestMethod]
    public void Translate_Http3_LeavesABackendServerFieldInPlace()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3, "HTTP/1.1 200 OK\r\nServer: test-server/fake\r\nContent-Length: 0\r\n\r\n");

        CollectionAssert.AreEqual(
            new[] { "server: test-server/fake", "content-length: 0", "via: 1.1 nghttpx" }, Lines(response));
    }

    [TestMethod]
    public void Translate_Http2_ReplacesABackendServerFieldAsCase1700Expects()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http2, "HTTP/1.1 200 OK\r\nServer: test-server/fake\r\nETag: \"x\"\r\n\r\n");

        CollectionAssert.AreEqual(new[] { "etag: \"x\"", "server: nghttpx", "via: 1.1 nghttpx" }, Lines(response));
    }

    [TestMethod]
    public void Translate_DroppedFields_AreNotForwarded()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3,
            "HTTP/1.1 200 OK\r\nConnection: close\r\nKeep-Alive: 1\r\nProxy-Connection: x\r\nUpgrade: h2c\r\nA: b\r\n\r\n");

        CollectionAssert.AreEqual(new[] { "a: b", "via: 1.1 nghttpx" }, Lines(response));
    }

    [TestMethod]
    public void Translate_ExistingVia_GetsNghttpxAppendedAndMovedLast()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3, "HTTP/1.1 200 OK\r\nVia: 1.0 origin\r\nA: b\r\n\r\n");

        CollectionAssert.AreEqual(new[] { "a: b", "via: 1.0 origin, 1.1 nghttpx" }, Lines(response));
    }

    [TestMethod]
    public void Translate_ReplyWithoutContentLength_IsCloseDelimitedAndHasNoContentLengthUnpinned()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3, "HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nuntil close");

        CollectionAssert.AreEqual(new[] { "via: 1.1 nghttpx" }, Lines(response));
        Assert.AreEqual("until close", Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    public void Translate_ChunkedReply_IsDechunkedAndLosesContentLengthUnpinned()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3,
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 99\r\n\r\n3;ext=1\r\nabc\r\n2\r\nde\r\n0\r\n\r\n");

        CollectionAssert.AreEqual(new[] { "via: 1.1 nghttpx" }, Lines(response));
        Assert.AreEqual("abcde", Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nab", "ab")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabc", "abc")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\nabc", "")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3", "")]
    public void Translate_TruncatedOrBrokenChunking_KeepsWhatArrived(string reply, string expectedBody)
    {
        NghttpxGatewayResponse response = Translate(NghttpxGatewayVersion.Http3, reply);

        Assert.AreEqual(expectedBody, Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    public void Translate_BodyLongerThanContentLength_IsCutAtContentLength()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3, "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nabcd");

        Assert.AreEqual("ab", Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    public void Translate_BodyShorterThanContentLength_KeepsWhatArrived()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3, "HTTP/1.1 200 OK\r\nContent-Length: 9\r\n\r\nab");

        Assert.AreEqual("ab", Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    public void Translate_UnreadableContentLength_ReadsToTheClose()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3, "HTTP/1.1 200 OK\r\nContent-Length: x\r\n\r\nabcd");

        Assert.AreEqual("abcd", Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    public void Translate_InterimReplies_AreDropped()
    {
        NghttpxGatewayResponse response = Translate(
            NghttpxGatewayVersion.Http3, "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 404 Not Found\r\nContent-Length: 1\r\n\r\nx");

        Assert.AreEqual(404, response.Status);
        Assert.AreEqual("x", Encoding.ASCII.GetString(response.Body));
    }

    [TestMethod]
    public void Translate_StatusBelowTheInterimRange_IsTheFinalReply()
    {
        Assert.AreEqual(99, Translate(NghttpxGatewayVersion.Http2, "HTTP/1.1 99 Odd\r\n\r\n").Status);
    }

    [TestMethod]
    [DataRow("garbage\r\nA: b\r\n\r\n")]
    [DataRow("HTTP/1.1 abc\r\nA: b\r\n\r\n")]
    public void Translate_UnreadableStatusLine_IsA502(string reply)
    {
        Assert.AreEqual(502, Translate(NghttpxGatewayVersion.Http2, reply).Status);
    }

    [TestMethod]
    public void Translate_LineWithoutColon_IsIgnored()
    {
        NghttpxGatewayResponse response = Translate(NghttpxGatewayVersion.Http3, "HTTP/1.1 200 OK\r\nnonsense\r\nA: b\r\n\r\n");

        CollectionAssert.AreEqual(new[] { "a: b", "via: 1.1 nghttpx" }, Lines(response));
    }

    [TestMethod]
    public void Translate_NoCompleteHead_ReturnsNull()
    {
        Assert.IsNull(NghttpxGatewayResponseTranslator.Translate(NghttpxGatewayVersion.Http2, "HTTP/1.1 200 OK\r\nA: b\r\n"u8));
        Assert.IsNull(NghttpxGatewayResponseTranslator.Translate(NghttpxGatewayVersion.Http2, default));
    }

    private static NghttpxGatewayResponse Translate(NghttpxGatewayVersion version, string reply) =>
        NghttpxGatewayResponseTranslator.Translate(version, Encoding.Latin1.GetBytes(reply))!;

    private static string[] Lines(NghttpxGatewayResponse response) =>
        [.. response.Fields.Select(field => $"{field.Key}: {field.Value}")];
}
