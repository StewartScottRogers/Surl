using System.Buffers;
using Surl.Hpack;
using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The HTTP/2 frames the proxy's redaction tests send, and what a redactor logs of the chunks
/// they give it.
/// </summary>
internal static class HttpProxyRedactionTestBytes
{
    public const string Token = "YXBwOnMzY3JldC1wdw==";

    public const string Credential = "Basic " + Token;

    /// <summary>
    /// A <c>HEADERS</c> frame whose HPACK strings are raw bytes, not Huffman-coded, so a leaked
    /// value is visible in the log as text.
    /// </summary>
    public static byte[] RawHeaders(string name, string value)
    {
        var block = new HpackEncoder(stringCoding: HpackStringCoding.Raw).Encode(
        [
            new HpackHeaderField(":method", "GET"),
            new HpackHeaderField(name, value, HpackFieldIndexing.WithoutIndexing),
        ]);
        var frame = Http2TestClient.Frame(new Http2HeadersFrame(1, block, true, EndHeaders: true, null));
        Assert.Contains(value, ProxyTestKit.Latin1(frame));

        return frame;
    }

    /// <summary>What is logged of a header block that carries a credential: its frame header, then the mask.</summary>
    public static byte[] Withheld(byte[] headersFrame) => [.. headersFrame[..9], .. "****"u8];

    /// <summary>
    /// What <paramref name="redactor"/> logs when the proxy sends <paramref name="proxySends"/>
    /// between the client's chunks <paramref name="before"/> and <paramref name="after"/>.
    /// </summary>
    public static byte[] Logged(IReceivedBytesRedactor redactor, byte[][] before, string proxySends, byte[][] after)
    {
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in before)
        {
            redactor.Redact(chunk, output);
        }

        redactor.ObserveSent(ProxyTestKit.Bytes(proxySends).Span);
        foreach (var chunk in after)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }
}
