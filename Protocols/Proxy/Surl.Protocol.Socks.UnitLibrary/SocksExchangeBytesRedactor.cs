using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of one SOCKS exchange. Until the server grants the
/// request, the client's bytes are the SOCKS handshake, redacted by
/// <see cref="Socks5PasswordRedactor"/>; once the server has sent a success reply (SOCKS4
/// <c>00 5A</c>, or a SOCKS5 <c>05 00</c> longer than the two-byte method selection), they are the
/// tunnel's, redacted by <see cref="SocksTunnelBytesRedactor"/> (ADR-0182, amendments 2 and 6).
/// </summary>
internal sealed class SocksExchangeBytesRedactor : IReceivedBytesRedactor
{
    private const int Socks5MethodSelectionLength = 2;

    private IReceivedBytesRedactor current = new Socks5PasswordRedactor();
    private bool isTunnelling;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output) => current.Redact(received, output);

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        current.ObserveSent(sent);
        if (!isTunnelling && IsSuccessReply(sent))
        {
            // The handshake redactor holds no bytes back, so nothing is lost by replacing it.
            isTunnelling = true;
            current = new SocksTunnelBytesRedactor();
        }
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => current.Flush(output);

    private static bool IsSuccessReply(ReadOnlySpan<byte> sent) =>
        sent.StartsWith((ReadOnlySpan<byte>)[0x00, 0x5A])
        || (sent.Length > Socks5MethodSelectionLength && sent.StartsWith((ReadOnlySpan<byte>)[SocksExchange.Socks5Version, 0x00]));
}
