using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WsExchangeLogRedactionTests
{
    private const string Head = "GET /x HTTP/1.1\r\nHost: h\r\nUpgrade: websocket\r\nAuthorization: Basic YWxpY2U6czNjcmV0\r\n\r\n";
    private const string MaskedHead = "GET /x HTTP/1.1\r\nHost: h\r\nUpgrade: websocket\r\nAuthorization: Basic ****\r\n\r\n";

    private static string Logged(params string[] chunks)
    {
        var server = WsServerHarness.Server();
        var redactor = server.CreateRedactor(new ExchangeRedactionContext(1, "ws"));
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.Latin1.GetBytes(chunk), output);
        }

        redactor.ObserveSent("HTTP/1.1 101 Switching Protocols\r\n\r\n"u8);
        redactor.Flush(output);
        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void UpgradeGet_AuthorizationCredentials_AreLoggedMasked() =>
        Assert.AreEqual(MaskedHead, Logged(Head));

    [TestMethod]
    public void UpgradeGet_SplitAcrossReads_IsMaskedTheSame() =>
        Assert.AreEqual(MaskedHead, Logged(Head[..50], Head[50..70], Head[70..]));

    [TestMethod]
    public void FrameAfter101_ContainingAuthorizationText_IsLoggedUnchanged() =>
        Assert.AreEqual(MaskedHead + "\x81\x0eAuthorization: x\r\n\r\nAuthorization: Basic QQ==\r\n",
            Logged(Head + "\x81\x0eAuthorization: x\r\n\r\n", "Authorization: Basic QQ==\r\n"));

    [TestMethod]
    public void FrameInTheReadThatEndsTheHead_IsLoggedUnchanged() =>
        Assert.AreEqual(MaskedHead + "Authorization: Basic QQ==\r\n\r\n",
            Logged(Head + "Authorization: Basic QQ==\r\n\r\n"));

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    [DataRow("\r\n\n\r\n")]
    [DataRow("\r\r\n")]
    public void UpgradeGetAfterEmptyLines_AuthorizationCredentials_AreLoggedMasked(string emptyLines) =>
        Assert.AreEqual(emptyLines + MaskedHead, Logged(emptyLines + Head));

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    public void UpgradeGetAfterEmptyLine_OneByteAtATime_IsMaskedTheSame(string emptyLine) =>
        Assert.AreEqual(emptyLine + MaskedHead,
            Logged([.. (emptyLine + Head).Select(character => character.ToString())]));

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    public void FrameAfterHeadThatFollowsAnEmptyLine_IsLoggedUnchanged(string emptyLine) =>
        Assert.AreEqual(emptyLine + MaskedHead + "Authorization: Basic QQ==\r\n\r\nAuthorization: Basic QQ==\r\n",
            Logged(emptyLine + Head + "Authorization: Basic QQ==\r\n\r\n", "Authorization: Basic QQ==\r\n"));

    [TestMethod]
    public void Redact_NullOutput_Throws()
    {
        var redactor = WsServerHarness.Server()
            .CreateRedactor(new ExchangeRedactionContext(1, "ws"));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
    }
}
