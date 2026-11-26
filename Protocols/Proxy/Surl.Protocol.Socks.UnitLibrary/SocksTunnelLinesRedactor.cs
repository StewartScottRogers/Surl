using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for the lines a client sends through a SOCKS tunnel: a
/// <see cref="LineBytesRedactor"/> over <see cref="SocksTunnelLineRedactionRule"/> that ends a line at
/// a bare line feed too (ADR-0182, amendment 9).
/// </summary>
/// <remarks>
/// Each line is judged as soon as it ends, so a SASL answer ended by a bare line feed is judged while
/// the server's continuation before it is still the last thing the server sent.
/// </remarks>
internal sealed class SocksTunnelLinesRedactor : IReceivedBytesRedactor
{
    private const byte LineFeed = (byte)'\n';

    private readonly LineBytesRedactor lines = new(new SocksTunnelLineRedactionRule());

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
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
