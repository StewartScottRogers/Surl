using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspExchangeLogRedactionTests
{
    private const string Options = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nAuthorization: Basic YWxpY2U6czNjcmV0\r\n\r\n";
    private const string OptionsLogged = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nAuthorization: Basic ****\r\n\r\n";

    private static string Logged(params byte[][] chunks)
    {
        var redactor = RtspServerHarness.Server().CreateRedactor(new ExchangeRedactionContext(1, "rtsp"));
        var output = new ArrayBufferWriter<byte>();
        redactor.ObserveSent("RTSP/1.0 401 Unauthorized\r\n"u8);
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);

        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    [TestMethod]
    public void Options_WithAuthorization_IsLoggedWithTheCredentialsMasked() =>
        Assert.AreEqual(OptionsLogged, Logged(Latin1(Options)));

    [TestMethod]
    public void Options_SplitAtEveryByte_IsMaskedTheSame()
    {
        var bytes = Latin1(Options);

        Assert.AreEqual(OptionsLogged, Logged(bytes.Select(b => new[] { b }).ToArray()));
    }

    [TestMethod]
    public void InterleavedFrame_ContainingAuthorizationBytes_IsLoggedUnchanged()
    {
        byte[] payload = Latin1("Authorization: Basic YWxpY2U6czNjcmV0\r\n\r\n");
        byte[] frame = [(byte)'$', 0, 0, (byte)payload.Length, .. payload];

        string logged = Logged(Latin1(OptionsAs("1")), frame, Latin1(OptionsAs("2")));

        Assert.AreEqual(LoggedOptionsAs("1") + Encoding.Latin1.GetString(frame) + LoggedOptionsAs("2"), logged);
    }

    [TestMethod]
    public void InterleavedFrame_SplitAtEveryByte_IsLoggedUnchanged()
    {
        byte[] payload = Latin1("Authorization: Basic abc\r\n");
        byte[] frame = [(byte)'$', 1, 0, (byte)payload.Length, .. payload];
        byte[] all = [.. frame, .. Latin1(Options)];

        string logged = Logged(all.Select(b => new[] { b }).ToArray());

        Assert.AreEqual(Encoding.Latin1.GetString(frame) + OptionsLogged, logged);
    }

    [TestMethod]
    public void EmptyInterleavedFrame_EndsAtItsHeader() =>
        Assert.AreEqual("$\u0000\u0000\u0000" + OptionsLogged, Logged([(byte)'$', 0, 0, 0], Latin1(Options)));

    [TestMethod]
    public void DollarSignInsideAHeadLine_IsNotAFrame() =>
        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nX: $\u0001\u0000\u0002\r\n\r\n",
            Logged(Latin1("OPTIONS * RTSP/1.0\r\nX: $\u0001\u0000\u0002\r\n\r\n")));

    [TestMethod]
    public void DollarSignInsideARequestBody_IsNotAFrame()
    {
        const string request = "ANNOUNCE a RTSP/1.0\r\nContent-Length: 3\r\n\r\n$ab";

        Assert.AreEqual(request + OptionsLogged, Logged(Latin1(request), Latin1(Options)));
    }

    [TestMethod]
    public void Redact_WithNoOutput_Throws()
    {
        var redactor = RtspServerHarness.Server().CreateRedactor(new ExchangeRedactionContext(1, "rtsp"));

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact("x"u8, null!));
    }

    private static string OptionsAs(string cseq) =>
        $"OPTIONS * RTSP/1.0\r\nCSeq: {cseq}\r\nAuthorization: Basic YWxpY2U6czNjcmV0\r\n\r\n";

    private static string LoggedOptionsAs(string cseq) =>
        $"OPTIONS * RTSP/1.0\r\nCSeq: {cseq}\r\nAuthorization: Basic ****\r\n\r\n";
}
