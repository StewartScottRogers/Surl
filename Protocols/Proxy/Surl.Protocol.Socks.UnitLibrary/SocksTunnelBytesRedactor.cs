using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for the bytes a client sends through a granted SOCKS
/// tunnel (ADR-0182, amendment 6). SOCKS does not say what protocol the tunnel carries, so the
/// first byte decides: an ASCII letter starts a text protocol's command or request line, and the
/// whole tunnel is then redacted line by line (<see cref="LineBytesRedactor"/> with
/// <see cref="SocksTunnelLineRedactionRule"/>); any other first byte (TLS, MQTT, LDAP and other
/// binary protocols) leaves the whole tunnel logged byte for byte, never held back.
/// </summary>
internal sealed class SocksTunnelBytesRedactor : IReceivedBytesRedactor
{
    private LineBytesRedactor? lines;
    private bool isDecided;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!isDecided && received.Length > 0)
        {
            isDecided = true;
            lines = char.IsAsciiLetter((char)received[0]) ? new LineBytesRedactor(new SocksTunnelLineRedactionRule()) : null;
        }

        if (lines is null)
        {
            output.Write(received);
        }
        else
        {
            lines.Redact(received, output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => lines?.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        lines?.Flush(output);
    }
}
