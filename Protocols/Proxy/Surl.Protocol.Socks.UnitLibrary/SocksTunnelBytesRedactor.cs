using System.Buffers;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for the bytes a client sends through a granted SOCKS
/// tunnel (ADR-0182, amendments 6, 7, 9, 10, 23 and 25). SOCKS does not say what protocol the tunnel
/// carries, so the first byte decides. An ASCII letter (a text protocol's command or request line, or
/// the HTTP/2 connection preface) or a digit from 1 to 9 (a numbered IMAP tag) sends the tunnel
/// through <see cref="SocksTextTunnelBytesRedactor"/>. <c>0x10</c> (an MQTT <c>CONNECT</c>) or
/// <c>0x30</c> (an LDAP message), as the first byte or as the byte after a first line starting
/// <c>PROXY </c>, sends that byte and every later one through the redactor
/// <see cref="TunnelBinaryLoginStartFinder"/> gives, which the HTTP proxy shares. Any other first byte
/// (TLS and other binary protocols) leaves the whole tunnel logged byte for byte, never held back.
/// </summary>
internal sealed class SocksTunnelBytesRedactor : IReceivedBytesRedactor
{
    private readonly TunnelBinaryLoginStartFinder binaryLogin = new();
    private IReceivedBytesRedactor? tunnelled;
    private bool isDecided;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!isDecided && received.Length > 0)
        {
            isDecided = true;
            tunnelled = StartsText(received[0]) ? new SocksTextTunnelBytesRedactor() : null;
        }

        if (binaryLogin.TryFind(received, out var loginStart, out var loginRedactor))
        {
            // Only PROXY lines come before it, and the text redactor holds nothing past a line feed.
            RedactTunnelled(received[..loginStart], output);
            tunnelled = loginRedactor;
            received = received[loginStart..];
        }

        RedactTunnelled(received, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => tunnelled?.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        tunnelled?.Flush(output);
    }

    private static bool StartsText(byte first) =>
        char.IsAsciiLetter((char)first) || first is >= (byte)'1' and <= (byte)'9';

    private void RedactTunnelled(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (tunnelled is null)
        {
            output.Write(received);
        }
        else
        {
            tunnelled.Redact(received, output);
        }
    }
}
