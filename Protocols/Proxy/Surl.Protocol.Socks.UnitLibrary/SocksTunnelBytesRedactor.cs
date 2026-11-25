using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for the bytes a client sends through a granted SOCKS
/// tunnel (ADR-0182, amendments 6 and 7). SOCKS does not say what protocol the tunnel carries, so
/// the first byte decides. An ASCII letter (a text protocol's command or request line, or the HTTP/2
/// connection preface) or a digit from 1 to 9 (a numbered IMAP tag) sends the whole tunnel through
/// <see cref="SocksTextTunnelBytesRedactor"/>; any other first byte (TLS, MQTT, LDAP and other binary
/// protocols) leaves the whole tunnel logged byte for byte, never held back (amendment 9).
/// </summary>
internal sealed class SocksTunnelBytesRedactor : IReceivedBytesRedactor
{
    private SocksTextTunnelBytesRedactor? text;
    private bool isDecided;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!isDecided && received.Length > 0)
        {
            isDecided = true;
            text = StartsText(received[0]) ? new SocksTextTunnelBytesRedactor() : null;
        }

        if (text is null)
        {
            output.Write(received);
        }
        else
        {
            text.Redact(received, output);
        }
    }

    // 0x30, the digit 0, is left out: it opens LDAP's BER sequence.
    private static bool StartsText(byte first) =>
        char.IsAsciiLetter((char)first) || first is >= (byte)'1' and <= (byte)'9';

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => text?.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        text?.Flush(output);
    }
}
