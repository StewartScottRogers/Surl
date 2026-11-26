using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ws.WsServerHarness;

namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WsExchangeLogRedactionTests
{
    private const string Head = "GET /x HTTP/1.1\r\nHost: h\r\nUpgrade: websocket\r\nAuthorization: Basic YWxpY2U6czNjcmV0\r\n\r\n";
    private const string MaskedHead = "GET /x HTTP/1.1\r\nHost: h\r\nUpgrade: websocket\r\nAuthorization: Basic ****\r\n\r\n";
    private const string Answer401 = "HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n";

    // What is logged for the chunks received before the server answers 101, then those received after it.
    private static string Logged(string[] beforeThe101, params string[] afterThe101)
    {
        var redactor = Redactor();
        var output = new ArrayBufferWriter<byte>();
        Receive(redactor, output, beforeThe101);
        redactor.ObserveSent(Latin1Bytes(Recorded101Head));
        Receive(redactor, output, afterThe101);
        redactor.Flush(output);
        return Latin1(output.WrittenSpan.ToArray());
    }

    private static IReceivedBytesRedactor Redactor() =>
        Server().CreateRedactor(new ExchangeRedactionContext(1, "ws"));

    private static void Receive(IReceivedBytesRedactor redactor, ArrayBufferWriter<byte> output, params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.Latin1.GetBytes(chunk), output);
        }
    }

    private static string[] OneByteAtATime(string text) => [.. text.Select(character => character.ToString())];

    [TestMethod]
    public void UpgradeGet_AuthorizationCredentials_AreLoggedMasked() =>
        Assert.AreEqual(MaskedHead, Logged([Head]));

    [TestMethod]
    public void UpgradeGet_SplitAcrossReads_IsMaskedTheSame() =>
        Assert.AreEqual(MaskedHead, Logged([Head[..50], Head[50..70], Head[70..]]));

    // The first head is the one pinned upstream curl sends with --anyauth (Fixtures/anyauth-401-101);
    // curl closes on the 401, so the retry on the same connection is that head with a login added.
    [TestMethod]
    [DataRow(false, DisplayName = "the retry in one read")]
    [DataRow(true, DisplayName = "one byte at a time")]
    public void SecondUpgradeGetAfterA401_AuthorizationCredentials_AreLoggedMasked(bool oneByteAtATime)
    {
        var first = Latin1(RecordedFixture.ReadRequestBytes("anyauth-401-101"));
        var retry = first.Replace("Accept: */*\r\n", "Accept: */*\r\nAuthorization: Basic dGVzdGVyOnNlY3JldA==\r\n", StringComparison.Ordinal);
        var redactor = Redactor();
        var output = new ArrayBufferWriter<byte>();

        Receive(redactor, output, oneByteAtATime ? OneByteAtATime(first) : [first]);
        redactor.ObserveSent(Latin1Bytes(Answer401));
        Receive(redactor, output, oneByteAtATime ? OneByteAtATime(retry) : [retry]);
        redactor.ObserveSent(Latin1Bytes(Recorded101Head));
        Receive(redactor, output, "Authorization: Basic QQ==\r\n");
        redactor.Flush(output);

        Assert.AreEqual(
            first + retry.Replace("dGVzdGVyOnNlY3JldA==", "****", StringComparison.Ordinal) + "Authorization: Basic QQ==\r\n",
            Latin1(output.WrittenSpan.ToArray()));
    }

    [TestMethod]
    public void SecondUpgradeGetReadWithTheFirst_BeforeAnyAnswer_IsMasked() =>
        Assert.AreEqual(MaskedHead + MaskedHead, Logged([Head + Head]));

    [TestMethod]
    public void FrameAfter101_ContainingAuthorizationText_IsLoggedUnchanged() =>
        Assert.AreEqual(MaskedHead + "\x81\x0eAuthorization: x\r\n\r\nAuthorization: Basic QQ==\r\n",
            Logged([Head], "\x81\x0eAuthorization: x\r\n\r\n", "Authorization: Basic QQ==\r\n"));

    [TestMethod]
    public void BytesReadWithTheHead_BeforeThe101_AreMaskedAsAnotherHead() =>
        Assert.AreEqual(MaskedHead + "Authorization: Basic ****\r\n\r\n",
            Logged([Head + "Authorization: Basic QQ==\r\n\r\n"]));

    [TestMethod]
    public void UnfinishedLineHeldWhenThe101IsSent_IsLoggedBeforeTheFramesAfterIt() =>
        Assert.AreEqual(MaskedHead + "\x82\x04chatAuthorization: Basic QQ==\r\n",
            Logged([Head + "\x82\x04ch"], "at", "Authorization: Basic QQ==\r\n"));

    [TestMethod]
    public void UnfinishedLineHeldWhenTheExchangeEnds_IsLoggedAtFlush() =>
        Assert.AreEqual(MaskedHead + "GET /y", Logged([Head + "GET /y"]));

    [TestMethod]
    [DataRow("\r\r\n")]
    [DataRow("\r\r\r\n")]
    public void FieldAfterALineOfCarriageReturns_IsLoggedMasked(string line)
    {
        const string requestLine = "GET /x HTTP/1.1\r\n";

        Assert.AreEqual(requestLine + line + MaskedHead[requestLine.Length..],
            Logged([requestLine + line + Head[requestLine.Length..]]));
    }

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    [DataRow("\r\n\n\r\n")]
    [DataRow("\r\r\n")]
    public void UpgradeGetAfterEmptyLines_AuthorizationCredentials_AreLoggedMasked(string emptyLines) =>
        Assert.AreEqual(emptyLines + MaskedHead, Logged([emptyLines + Head]));

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    public void UpgradeGetAfterEmptyLine_OneByteAtATime_IsMaskedTheSame(string emptyLine) =>
        Assert.AreEqual(emptyLine + MaskedHead, Logged(OneByteAtATime(emptyLine + Head)));

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    public void FrameAfterHeadThatFollowsAnEmptyLine_IsLoggedUnchanged(string emptyLine) =>
        Assert.AreEqual(emptyLine + MaskedHead + "Authorization: Basic QQ==\r\n\r\nAuthorization: Basic QQ==\r\n",
            Logged([emptyLine + Head], "Authorization: Basic QQ==\r\n\r\n", "Authorization: Basic QQ==\r\n"));

    [TestMethod]
    public void Redact_NullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Redactor().Redact([], null!));
}
