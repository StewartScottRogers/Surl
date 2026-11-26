using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>SMTP, IMAP and POP3 logins through a granted SOCKS tunnel, as the exchange log shows them (ADR-0182, amendment 9).</summary>
[TestClass]
public sealed class SocksTunnelMailRedactionTests
{
    // One tunnelled exchange: what the origin sent, then what the client sent in answer.
    private static string Logged(params (string OriginSends, string ClientSends)[] turns)
    {
        var redactor = new SocksProtocolServer(new InMemoryOriginConnector())
            .CreateRedactor(new ExchangeRedactionContext(1, "socks5"));
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact([5, 1, 0], output);
        redactor.ObserveSent([5, 0]);
        redactor.Redact(SocksTestKit.Socks5DomainRequest("localhost", 25), output);
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);
        output.Clear();
        foreach (var (originSends, clientSends) in turns)
        {
            redactor.ObserveSent(SocksTestKit.Latin1Bytes(originSends));
            redactor.Redact(SocksTestKit.Latin1Bytes(clientSends), output);
        }

        redactor.Flush(output);

        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void Smtp_AuthPlainInitialResponse_IsLoggedMasked() =>
        Assert.AreEqual(
            "EHLO client\r\nAUTH PLAIN ****\r\n",
            Logged(("", "EHLO client\r\n"), ("250 AUTH PLAIN LOGIN\r\n", "AUTH PLAIN AGFwcABzM2NyZXQtcHc=\r\n")));

    [TestMethod]
    public void Smtp_AuthLoginContinuationsAfter334_AreLoggedMasked() =>
        Assert.AreEqual(
            "EHLO client\r\nAUTH LOGIN\r\n****\r\n****\r\nMAIL FROM:<app@example.com>\r\n",
            Logged(
                ("", "EHLO client\r\n"),
                ("250 AUTH LOGIN\r\n", "AUTH LOGIN\r\n"),
                ("334 VXNlcm5hbWU6\r\n", "YXBw\r\n"),
                ("334 UGFzc3dvcmQ6\r\n", "czNjcmV0LXB3\r\n"),
                ("235 Authenticated\r\n", "MAIL FROM:<app@example.com>\r\n")));

    [TestMethod]
    public void Smtp_AuthLoginWithInitialResponse_HasBothSecretsMasked() =>
        Assert.AreEqual(
            "AUTH LOGIN ****\r\n****\r\n",
            Logged(("220 ready\r\n", "AUTH LOGIN YXBw\r\n"), ("334 UGFzc3dvcmQ6\r\n", "czNjcmV0LXB3\r\n")));

    [TestMethod]
    public void Smtp_334SplitAtEveryByte_StillMasksTheAnswer()
    {
        const string continuation = "334 UGFzc3dvcmQ6\r\n";
        for (var split = 1; split < continuation.Length; split++)
        {
            var logged = Logged(
                ("", "AUTH LOGIN\r\n"),
                (continuation[..split], ""),
                (continuation[split..], "czNjcmV0LXB3\r\n"));

            Assert.AreEqual("AUTH LOGIN\r\n****\r\n", logged, $"split {split}");
        }
    }

    [TestMethod]
    public void Smtp_334SentOneByteAtATimeAfterAnotherReply_StillMasksTheAnswer()
    {
        var turns = "250 OK\r\n334 \r\n".Select(character => (character.ToString(), ""))
            .Prepend(("", "AUTH LOGIN\r\n"))
            .Append(("", "czNjcmV0LXB3\r\n"));

        Assert.AreEqual("AUTH LOGIN\r\n****\r\n", Logged([.. turns]));
    }

    [TestMethod]
    public void Smtp_AnswersEndedByABareLineFeed_AreLoggedMasked() =>
        Assert.AreEqual(
            "AUTH LOGIN\n****\n****\nQUIT\n",
            Logged(
                ("220 ready\r\n", "AUTH LOGIN\n"),
                ("334 VXNlcm5hbWU6\r\n", "YXBw\n"),
                ("334 UGFzc3dvcmQ6\r\n", "czNjcmV0LXB3\n"),
                ("235 Authenticated\r\n", "QUIT\n")));

    [TestMethod]
    public void Imap_TaggedLogin_HasThePasswordMasked() =>
        Assert.AreEqual(
            "a001 CAPABILITY\r\na002 LOGIN app ****\r\na003 SELECT INBOX\r\n",
            Logged(
                ("* OK ready\r\n", "a001 CAPABILITY\r\n"),
                ("a001 OK done\r\n", "a002 LOGIN app s3cret-pw\r\n"),
                ("a002 OK done\r\n", "a003 SELECT INBOX\r\n")));

    [TestMethod]
    [DataRow("a login app s3cret-pw", "a login app ****")]
    [DataRow("a LOGIN  app  two words", "a LOGIN  app  ****")]
    [DataRow("a LOGIN \"an app\" \"s3cret pw\"", "a LOGIN \"an app\" ****")]
    [DataRow("a LOGIN \"a\\\"pp\" s3cret-pw", "a LOGIN \"a\\\"pp\" ****")]
    [DataRow("a LOGIN app {x}s3cret", "a LOGIN app ****")]
    [DataRow("a LOGIN app {3}s3cret}", "a LOGIN app ****")]
    [DataRow("Authorization: Login c2Vj cmV0", "Authorization: Login ****")]
    [DataRow("a\tLOGIN\tapp\ts3cret-pw", "a\tLOGIN\tapp\t****")]
    [DataRow("x\ty LOGIN app s3cret-pw", "x\ty LOGIN app ****")]
    [DataRow("a LOGIN \t s3cret-pw", "a LOGIN \t ****")]
    public void Imap_EachLoginForm_IsLoggedMasked(string sent, string logged) =>
        Assert.AreEqual(logged + "\r\n", Logged(("* OK ready\r\n", sent + "\r\n")));

    [TestMethod]
    [DataRow("{9}")]
    [DataRow("{9+}")]
    public void Imap_LiteralPassword_IsLoggedMasked(string marker) =>
        Assert.AreEqual(
            $"a LOGIN app {marker}\r\n****\r\nb NOOP\r\n",
            Logged(
                ("* OK ready\r\n", $"a LOGIN app {marker}\r\n"),
                ("+ Ready\r\n", "s3cret-pw\r\n"),
                ("a OK done\r\n", "b NOOP\r\n")));

    [TestMethod]
    public void Imap_LiteralUserAndLiteralPasswordOverTwoLines_AreLoggedMasked() =>
        Assert.AreEqual(
            "a LOGIN {3+}\r\n****\r\n****\r\n****\r\nb NOOP\r\n",
            Logged(("* OK ready\r\n", "a LOGIN {3+}\r\napp {11+}\r\ns3cret\r\n-pw\r\nb NOOP\r\n")));

    [TestMethod]
    public void Imap_LiteralPasswordHoldingItsOwnLineEnd_IsWhollyMasked() =>
        Assert.AreEqual(
            "a LOGIN app {2+}\r\n****\r\n****\r\nb NOOP\r\n",
            Logged(("* OK ready\r\n", "a LOGIN app {2+}\r\n\r\n\r\nb NOOP\r\n")));

    [TestMethod]
    public void Imap_LiteralLongerThanALongHolds_MasksEveryLaterLine() =>
        Assert.AreEqual(
            "a LOGIN app {99999999999999999999}\r\n****\r\n****\r\n",
            Logged(("* OK ready\r\n", "a LOGIN app {99999999999999999999}\r\ns3cret-pw\r\nb NOOP\r\n")));

    [TestMethod]
    [DataRow("a LOGIN")]
    [DataRow("a LOGIN app")]
    [DataRow("a LOGIN app ")]
    [DataRow("a LOGIN \"app")]
    [DataRow("a LOGIN {}")]
    [DataRow("a LOGIN 5}")]
    [DataRow("a LOGOUT now please")]
    [DataRow("LOGIN")]
    public void Imap_ALoginWithNoPasswordToWithhold_LeavesBothLinesAsReceived(string line) =>
        Assert.AreEqual($"{line}\r\nb NOOP\r\n", Logged(("* OK ready\r\n", $"{line}\r\nb NOOP\r\n")));

    [TestMethod]
    public void Imap_AuthenticateContinuationAfterPlus_IsLoggedMasked() =>
        Assert.AreEqual(
            "a AUTHENTICATE PLAIN\r\n****\r\nb SELECT INBOX\r\n",
            Logged(
                ("* OK ready\r\n", "a AUTHENTICATE PLAIN\r\n"),
                ("+ \r\n", "AGFwcABzM2NyZXQtcHc=\r\n"),
                ("a OK done\r\n", "b SELECT INBOX\r\n")));

    [TestMethod]
    public void Imap_AuthenticateInitialResponse_IsLoggedMasked() =>
        Assert.AreEqual(
            "a AUTHENTICATE PLAIN ****\r\n",
            Logged(("* OK ready\r\n", "a AUTHENTICATE PLAIN AGFwcABzM2NyZXQtcHc=\r\n")));

    [TestMethod]
    public void Pop3_AuthPlainInitialResponse_IsLoggedMasked() =>
        Assert.AreEqual(
            "CAPA\r\nAUTH PLAIN ****\r\n",
            Logged(("+OK ready\r\n", "CAPA\r\n"), ("+OK\r\n.\r\n", "AUTH PLAIN AGFwcABzM2NyZXQtcHc=\r\n")));

    [TestMethod]
    public void Pop3_AuthContinuationAfterPlus_IsLoggedMasked() =>
        Assert.AreEqual(
            "AUTH PLAIN\r\n****\r\nSTAT\r\n",
            Logged(
                ("+OK ready\r\n", "AUTH PLAIN\r\n"),
                ("+ \r\n", "AGFwcABzM2NyZXQtcHc=\r\n"),
                ("+OK logged in\r\n", "STAT\r\n")));

    [TestMethod]
    [DataRow("220 ready\r\n", "EHLO client\r\nMAIL FROM:<app@example.com>\r\nRCPT TO:<to@example.com>\r\nDATA\r\n")]
    [DataRow("* OK ready\r\n", "a SELECT INBOX\r\nb FETCH 1 BODY[]\r\nc LOGOUT\r\n")]
    [DataRow("+OK ready\r\n", "USER app\r\nLIST\r\nRETR 1\r\nQUIT\r\n")]
    [DataRow("220 ready\r\n", "AUTH TLS\r\n")]
    public void ALineOfEachProtocolWithNoSecret_IsLoggedAsReceived(string greeting, string sent) =>
        Assert.AreEqual(sent, Logged((greeting, sent)));

    [TestMethod]
    [DataRow("a AUTHENTICATE PLAIN", "+\r\n")]
    [DataRow("a AUTHENTICATE PLAIN", "+\n")]
    [DataRow("AUTH PLAIN", "+OK hello\r\n+\r\n")]
    [DataRow("AUTH LOGIN", "334\r\n")]
    public void ABareContinuation_MasksTheAnswer(string command, string continuation) =>
        Assert.AreEqual(
            $"{command}\r\n****\r\n",
            Logged(("", command + "\r\n"), (continuation, "czNjcmV0LXB3\r\n")));

    [TestMethod]
    public void ABareContinuationSplitBeforeItsLineEnd_MasksTheAnswer() =>
        Assert.AreEqual(
            "AUTH LOGIN\r\n****\r\n",
            Logged(("", "AUTH LOGIN\r\n"), ("334", ""), ("\r", ""), ("\n", "czNjcmV0LXB3\r\n")));

    [TestMethod]
    [DataRow("+OK\r\n")]
    [DataRow("+")]
    [DataRow("3345\r\n")]
    [DataRow("\r\n")]
    public void AServerLineThatIsNoContinuation_LeavesTheNextLineAsReceived(string originSends) =>
        Assert.AreEqual("NOOP\r\nSTAT\r\n", Logged(("", "NOOP\r\n"), (originSends, "STAT\r\n")));

    [TestMethod]
    [DataRow("1 LOGIN app s3cret-pw", "1 LOGIN app ****")]
    [DataRow("9  LOGIN app s3cret-pw", "9  LOGIN app ****")]
    public void Imap_NumberedTag_HasThePasswordMasked(string sent, string logged) =>
        Assert.AreEqual(logged + "\r\n", Logged(("* OK ready\r\n", sent + "\r\n")));

    [TestMethod]
    public void Imap_LiteralPasswordOfBareLineFeeds_IsMaskedToItsEnd() =>
        Assert.AreEqual(
            "a LOGIN app {8+}\n****\n****\n****\n****\nb NOOP\n",
            Logged(("* OK ready\r\n", "a LOGIN app {8+}\n\n\n\nsecret\nb NOOP\n")));

    [TestMethod]
    public void ALineEndSplitBetweenReads_IsLoggedOnce() =>
        Assert.AreEqual("NOOP\r\nQUIT\r\n", Logged(("", "NOOP\r"), ("", "\nQUIT\r\n")));
}
