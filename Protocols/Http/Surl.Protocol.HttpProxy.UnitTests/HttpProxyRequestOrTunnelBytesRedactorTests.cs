using static Surl.Protocol.HttpProxy.HttpProxyTunnelRedactionTestKit;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyRequestOrTunnelBytesRedactorTests
{
    private const string Secret = "czNjcmV0LXB3";

    [TestMethod]
    [DataRow("pass s3cret\r\n", "pass ****\r\n")]
    [DataRow("PASS \r\n", "PASS \r\n")]
    [DataRow("NOOP\r\nQUIT\r\n", "NOOP\r\nQUIT\r\n")]
    [DataRow("PASS s3cret\n", "PASS ****\n")]
    [DataRow("PASS s3cret", "PASS ****")]
    [DataRow("1 LOGIN alice s3cret\r\n", "1 LOGIN alice ****\r\n")]
    public void Redact_TunnelledCommand_WithholdsOnlyItsSecret(string sent, string expected)
    {
        Assert.AreEqual(Connect + expected, LoggedTunnel(sent));
        Assert.AreEqual(Connect + expected, LoggedTunnelByteByByte(sent));
    }

    [TestMethod]
    public void Redact_TunnelBytesSentWithTheConnectHead_AreWithheldToo()
    {
        var logged = Logged(Connect + "USER alice\r\nPASS s3cret\r\n");

        Assert.AreEqual(Connect + "USER alice\r\nPASS ****\r\n", logged);
    }

    [TestMethod]
    [DataRow("CONNECT o:21 HTTP/1.1\n\n")]
    [DataRow("CONNECT o:21 HTTP/1.1\r\n\n")]
    [DataRow("CONNECT o:21 HTTP/1.1\r\nHost: o:21\n\r\n")]
    [DataRow("CONNECT o:21 HTTP/1.1\r\n\r\n\n")]
    [DataRow("CONNECT o:21 HTTP/1.1\r\nContent-Length: 0\r\n\r\n")]
    [DataRow("CONNECT o:21 HTTP/1.1\r\nContent-Length: 3\r\n\r\n")]
    [DataRow("CONNECT o:21 HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n")]
    public void Redact_ATunnelAfterAnUnusualConnectHead_StillWithholdsThePassword(string head)
    {
        const string Sent = "USER alice\r\nPASS s3cret\r\n";
        const string Expected = "USER alice\r\nPASS ****\r\n";

        Assert.AreEqual(head + Expected, Logged(head + Sent));
        Assert.AreEqual(head + Expected, Logged([.. (head + Sent).Select(character => character.ToString())]));
    }

    [TestMethod]
    public void Redact_AConnectHeadEndedByBareLineFeeds_MasksItsProxyAuthorization()
    {
        const string Head = "CONNECT o:21 HTTP/1.1\r\nProxy-Authorization: Basic ";

        var logged = Logged(Head + Secret + "\n\n");

        Assert.AreEqual(Head + "****\n\n", logged);
    }

    [TestMethod]
    public void Redact_ARefusedConnectRetriedWithALogin_MasksItAndThenTheTunnel()
    {
        const string Retry = "CONNECT origin.test:21 HTTP/1.1\r\nProxy-Authorization: Basic ";

        var logged = Logged(
            Connect,
            "S:HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n",
            Retry + Secret + "\r\n\r\n",
            "S:" + Established,
            "PASS s3cret\r\n");

        Assert.AreEqual(Connect + Retry + "****\r\n\r\nPASS ****\r\n", logged);
    }

    [TestMethod]
    [DataRow("S:+ \r\n")]
    [DataRow("S:+\r\n")]
    [DataRow("S:334\r\n")]
    [DataRow("S:220 ready\r\n334 VXNlcm5hbWU6\r\n")]
    public void Redact_AnAnswerToAContinuation_IsWithheld(string continuation)
    {
        var logged = LoggedTunnel("AUTH LOGIN\r\n", continuation, Secret + "\r\n", "S:235 ok\r\n", "NOOP\r\n");

        Assert.AreEqual(Connect + "AUTH LOGIN\r\n****\r\nNOOP\r\n", logged);
    }

    [TestMethod]
    public void Redact_ABareLineFeedClientAnsweringContinuations_HasEveryAnswerWithheld()
    {
        var logged = LoggedTunnel(
            "AUTH LOGIN\n", "S:334 VXNlcm5hbWU6\r\n", Secret + "\n", "S:334 UGFzc3dvcmQ6\r\n", Secret + "\n", "S:235 ok\r\n", "QUIT\n");

        Assert.AreEqual(Connect + "AUTH LOGIN\n****\n****\nQUIT\n", logged);
    }

    [TestMethod]
    public void Redact_AContinuationSplitBetweenOriginReads_IsStillRecognised()
    {
        var logged = LoggedTunnel("A1 AUTHENTICATE PLAIN\r\n", "S:+", "S: go ", "S:on\r\n", Secret + "\r\n");

        Assert.AreEqual(Connect + "A1 AUTHENTICATE PLAIN\r\n****\r\n", logged);
    }

    [TestMethod]
    [DataRow("A1 LOGIN alice \"s3 cret\"\r\n", "A1 LOGIN alice ****\r\n")]
    [DataRow("A1  login  \"al \\\"ice\"  s3cret\r\n", "A1  login  \"al \\\"ice\"  ****\r\n")]
    [DataRow("A1\tLOGIN\talice\ts3cret\r\n", "A1\tLOGIN\talice\t****\r\n")]
    [DataRow("A1 LOGIN alice\r\n", "A1 LOGIN alice\r\n")]
    [DataRow("A1 LOGIN \"alice\r\n", "A1 LOGIN \"alice\r\n")]
    [DataRow("A1 LOGIN alice {6}\r\ns3cret\r\nA2 NOOP\r\n", "A1 LOGIN alice {6}\r\n****\r\nA2 NOOP\r\n")]
    [DataRow("A1 LOGIN alice {6+}\ns3cret\nA2 NOOP\n", "A1 LOGIN alice {6+}\n****\nA2 NOOP\n")]
    [DataRow("A1 LOGIN alice {9}\r\ns3c\r\nret!\r\nA2 NOOP\r\n", "A1 LOGIN alice {9}\r\n****\r\n****\r\nA2 NOOP\r\n")]
    [DataRow("A1 LOGIN {5}\r\nalice {6}\r\ns3cret\r\nA2 NOOP\r\n", "A1 LOGIN {5}\r\n****\r\n****\r\nA2 NOOP\r\n")]
    [DataRow("A1 LOGIN a x{\r\nA2 NOOP\r\n", "A1 LOGIN a ****\r\nA2 NOOP\r\n")]
    [DataRow("A1 LOGIN a x}\r\nA2 NOOP\r\n", "A1 LOGIN a ****\r\nA2 NOOP\r\n")]
    [DataRow("A1 LOGIN a {}\r\nA2 NOOP\r\n", "A1 LOGIN a ****\r\nA2 NOOP\r\n")]
    [DataRow("A1 LOGIN a {x}\r\nA2 NOOP\r\n", "A1 LOGIN a ****\r\nA2 NOOP\r\n")]
    [DataRow("A1 LOGIN a pw{6}\r\ns3cret\r\n", "A1 LOGIN a ****\r\n****\r\n")]
    [DataRow("A1 LOGIN a {99999999999999999999}\r\nA2 NOOP\r\n", "A1 LOGIN a {99999999999999999999}\r\n****\r\n")]
    public void Redact_TunnelledImapLogin_WithholdsThePasswordInEveryForm(string sent, string expected)
    {
        Assert.AreEqual(Connect + expected, LoggedTunnel(sent));
        Assert.AreEqual(Connect + expected, LoggedTunnelByteByByte(sent));
    }

    [TestMethod]
    [DataRow("AUTH alice \"a b\"\r\nPING\r\n", "AUTH alice ****\r\nPING\r\n")]
    [DataRow("AUTH alice 'a b'\r\nPING\r\n", "AUTH alice ****\r\nPING\r\n")]
    [DataRow("AUTH \"al ice\" \"a b\"\r\nPING\r\n", "AUTH \"al ****\r\nPING\r\n")]
    public void Redact_TunnelledInlineAuthWithAQuotedArgument_LogsNothingAfterTheMask(string sent, string expected)
    {
        Assert.AreEqual(Connect + expected, LoggedTunnel(sent));
        Assert.AreEqual(Connect + expected, LoggedTunnelByteByByte(sent));
    }

    [TestMethod]
    [DataRow("Authorization: Basic " + Secret, "Authorization: Basic ****")]
    [DataRow("proxy-authorization :\t" + Secret, "proxy-authorization :\t****")]
    [DataRow("Authorization: Bearer " + Secret + "\r\n " + Secret + "\r\n\t" + Secret, "Authorization: Bearer ****\r\n ****\r\n\t****")]
    [DataRow("Authorization:\r\n " + Secret, "Authorization:\r\n ****")]
    [DataRow("X-Note: a\r\n b", "X-Note: a\r\n b")]
    public void Redact_ACredentialHeaderAfterAProxyProtocolLine_IsMasked(string header, string expected)
    {
        const string Start = "PROXY TCP4 127.0.0.1 127.0.0.1 50000 80\r\nGET / HTTP/1.1\r\n";

        var logged = LoggedTunnel(Start + header + "\r\n\r\n");

        Assert.AreEqual(Connect + Start + expected + "\r\n\r\n", logged);
    }

    [TestMethod]
    [DataRow("GET / HTTP/1.1")]
    [DataRow("OPTIONS rtsp://origin.test/a RTSP/1.0")]
    public void Redact_ARequestThroughTheTunnel_LeavesItsBodyAsReceived(string requestLine)
    {
        var request = requestLine + "\r\nContent-Length: 13\r\n\r\nPASS s3cret\r\n";

        Assert.AreEqual(Connect + request, LoggedTunnel(request));
        Assert.AreEqual(Connect + request, LoggedTunnelByteByByte(request));
    }

    [TestMethod]
    [DataRow("A1 LOGIN alice HTTP/1.1", "A1 LOGIN alice ****")]
    [DataRow("PASS HTTP/1.1", "PASS ****")]
    [DataRow("PASS s3cret HTTP", "PASS ****")]
    public void Redact_ACommandThatResemblesARequestLine_IsStillWithheld(string sent, string expected) =>
        Assert.AreEqual(Connect + expected + "\r\n", LoggedTunnel(sent + "\r\n"));

    [TestMethod]
    [DataRow("\u0016\u0003\u0001\r\nPASS in-ciphertext\r\n")]
    [DataRow("0 LOGIN alice s3cret\r\n")]
    [DataRow(":1 LOGIN alice s3cret\r\n")]
    public void Redact_ATunnelNotStartingWithALetterOrANonZeroDigit_IsLoggedAsReceived(string sent) =>
        Assert.AreEqual(Connect + sent, LoggedTunnel(sent));

    [TestMethod]
    public void Redact_AFirstLinePast64KiB_IsMaskedAndLaterLinesAreStillJudged()
    {
        var overlong = new string('a', (64 * 1024) + 1);

        var logged = LoggedTunnel(overlong, "\r\nPASS s3cret\r\n");

        Assert.AreEqual(Connect + "****\r\nPASS ****\r\n", logged);
    }
}
