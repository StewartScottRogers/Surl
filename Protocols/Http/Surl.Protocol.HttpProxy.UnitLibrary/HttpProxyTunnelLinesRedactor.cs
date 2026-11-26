using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for the lines of a text protocol a client sends through
/// a <c>CONNECT</c> tunnel: a <see cref="LineBytesRedactor"/> over
/// <see cref="HttpProxyTunnelLineRedactionRule"/> that ends a line at a bare line feed too
/// (ADR-0182, amendment 16).
/// </summary>
/// <remarks>
/// Each line is judged as soon as it ends, so a SASL answer ended by a bare line feed is judged while
/// the server's continuation before it is still the last thing the server sent.
/// </remarks>
internal sealed class HttpProxyTunnelLinesRedactor : IReceivedBytesRedactor
{
    private const byte LineFeed = (byte)'\n';

    private readonly LineBytesRedactor lines = new(new HttpProxyTunnelLineRedactionRule());

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        int end;
        while ((end = received.IndexOf(LineFeed) + 1) > 0)
        {
            lines.Redact(received[..end], output);

            // Bytes still held after a line feed are a line it ended without a carriage return.
            if (lines.HeldByteCount > 0)
            {
                lines.Flush(output);
            }

            received = received[end..];
        }

        lines.Redact(received, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => lines.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => lines.Flush(output);
}
