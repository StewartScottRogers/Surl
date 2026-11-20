using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

[TestClass]
public sealed class SmtpExchangeLogRedactionTests
{
    private static string Logged(IEnumerable<(string Sent, string Received)> turns)
    {
        var redactor = new SmtpProtocolServer(new AnonymousAuthenticationPolicy(), SmtpTestExchange.NoSaslMechanisms(), SmtpTestExchange.AnonymousStore(new ManualTimeProvider()))
            .CreateRedactor(new ExchangeRedactionContext(1, "smtp"));
        var output = new ArrayBufferWriter<byte>();
        foreach (var (sent, received) in turns)
        {
            redactor.ObserveSent(Encoding.ASCII.GetBytes(sent));
            redactor.Redact(Encoding.ASCII.GetBytes(received), output);
        }

        redactor.Flush(output);

        return Encoding.ASCII.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void AuthPlainWithInitialResponse_IsLoggedWithTheResponseMasked() =>
        Assert.AreEqual("AUTH PLAIN ****\r\n", Logged([("220 hi\r\n", "AUTH PLAIN AGFsaWNlAHMzY3JldA==\r\n")]));

    [TestMethod]
    public void AuthLoginExchange_IsLoggedWithBothAnswersMasked() =>
        Assert.AreEqual(
            "AUTH LOGIN\r\n****\r\n****\r\n",
            Logged([("220 hi\r\n", "AUTH LOGIN\r\n"), ("334 VXNlcm5hbWU6\r\n", "YWxpY2U=\r\n"), ("334 UGFzc3dvcmQ6\r\n", "czNjcmV0\r\n")]));

    [TestMethod]
    public void AuthAfterStartTls_IsMaskedTheSame() =>
        Assert.AreEqual(
            "STARTTLS\r\nEHLO c\r\nAUTH PLAIN ****\r\n",
            Logged([("220 hi\r\n", "STARTTLS\r\n"), ("220 Ready\r\n", "EHLO c\r\n"), ("250 ok\r\n", "AUTH PLAIN AGFsaWNlAHMzY3JldA==\r\n")]));

    [TestMethod]
    public void DataBodyLineLookingLikeAuth_IsLoggedUnchanged() =>
        Assert.AreEqual(
            "DATA\r\nAUTH PLAIN x\r\n.\r\n",
            Logged([("220 hi\r\n", "DATA\r\n"), ("354 go\r\n", "AUTH PLAIN x\r\n.\r\n")]));

    [TestMethod]
    public void AuthAfterTheDataBodyEnds_IsMaskedAgain() =>
        Assert.AreEqual(
            ".\r\nAUTH PLAIN ****\r\n",
            Logged([("354 go\r\n", ".\r\n"), ("250 ok\r\n", "AUTH PLAIN abc\r\n")]));
}
