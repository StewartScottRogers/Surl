using System.Buffers;
using Surl.Http2;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for a SOCKS tunnel that opens with text. It redacts line
/// by line (<see cref="TunnelLinesRedactor"/>) until the client sends the HTTP/2 connection
/// preface, wherever it falls, because HTTP/2 carries a credential as an HPACK field no line rule
/// sees (ADR-0182, amendment 7). After the preface, frames are redacted by
/// <see cref="Http2RequestBytesRedactor"/> when the preface opened the tunnel (prior knowledge) or
/// the origin had sent <c>HTTP/1.1 101</c> (an <c>h2c</c> upgrade); otherwise every later byte is
/// withheld (<see cref="SocksWithheldBytesRedactor"/>). The frame redactor withholds a header block
/// carrying a field of <see cref="HttpWithheldHeaderNames.All"/> (amendment 19).
/// </summary>
internal sealed class SocksTextTunnelBytesRedactor : IReceivedBytesRedactor
{
    private IReceivedBytesRedactor current = new TunnelLinesRedactor();
    private SocksHttp2PrefaceFinder? prefaceFinder = new();
    private bool originSwitchedProtocols;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (prefaceFinder is not null && prefaceFinder.TryFind(received, out var length, out var opensTunnel))
        {
            // The preface ends a line, so flushing here leaves the line rule holding none of it.
            current.Redact(received[..length], output);
            current.Flush(output);
            current = RedactorAfterPreface(opensTunnel || originSwitchedProtocols);
            prefaceFinder = null;
            received = received[length..];
        }

        current.Redact(received, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        originSwitchedProtocols |= sent.StartsWith("HTTP/1.1 101"u8);
        current.ObserveSent(sent);
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => current.Flush(output);

    private static IReceivedBytesRedactor RedactorAfterPreface(bool readsFrames)
    {
        if (!readsFrames)
        {
            return new SocksWithheldBytesRedactor();
        }

        // The HTTP/2 redactor expects the preface first; it is already logged, so this copy is dropped.
        var frames = new Http2RequestBytesRedactor(HttpWithheldHeaderNames.ApiKeysAndSessionTokens);
        frames.Redact(Http2ConnectionPreface.Preface, new ArrayBufferWriter<byte>());

        return frames;
    }
}
