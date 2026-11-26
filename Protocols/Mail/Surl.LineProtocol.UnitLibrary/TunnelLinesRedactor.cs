using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> the SOCKS server and the HTTP proxy share for the lines of
/// a text protocol a client sends through a tunnel: a <see cref="LineBytesRedactor"/> over
/// <see cref="TunnelLineRedactionRule"/> that ends a line at a bare line feed too (ADR-0182,
/// amendments 9, 16 and 20).
/// </summary>
/// <remarks>
/// Each line is judged as soon as it ends, so a SASL answer ended by a bare line feed is judged while
/// the server's continuation before it is still the last thing the server sent. One instance serves
/// one tunnel.
/// </remarks>
public sealed class TunnelLinesRedactor : IReceivedBytesRedactor
{
    private const byte LineFeed = (byte)'\n';

    private readonly LineBytesRedactor _lines = new(new TunnelLineRedactionRule());

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        int end;
        while ((end = received.IndexOf(LineFeed) + 1) > 0)
        {
            _lines.Redact(received[..end], output);

            // Bytes still held after a line feed are a line it ended without a carriage return.
            if (_lines.HeldByteCount > 0)
            {
                _lines.Flush(output);
            }

            received = received[end..];
        }

        _lines.Redact(received, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => _lines.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => _lines.Flush(output);
}
