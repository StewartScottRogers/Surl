using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of one SOCKS exchange. The client's bytes are the SOCKS
/// handshake, redacted by <see cref="SocksHandshakeBytesRedactor"/>, up to the last byte of the
/// request, whether or not the server has answered it yet; every byte after it is the tunnel's,
/// redacted by <see cref="SocksTunnelBytesRedactor"/> (ADR-0182, amendments 2, 6 and 12). A success
/// reply (SOCKS4 <c>00 5A</c>, or a SOCKS5 <c>05 00</c> longer than the two-byte method selection)
/// starts the tunnel too, should the handshake redactor not have found the request's end. What the
/// server sends is shown to the tunnel's redactor only after that reply, so a handshake reply is
/// never read as the origin's.
/// </summary>
/// <param name="listenerOffersGssApi">Whether the server selects method <c>01</c> when a greeting offers it.</param>
internal sealed class SocksExchangeBytesRedactor(bool listenerOffersGssApi) : IReceivedBytesRedactor
{
    private const int Socks5MethodSelectionLength = 2;

    private readonly SocksHandshakeBytesRedactor handshake = new(listenerOffersGssApi);
    private SocksTunnelBytesRedactor? tunnel;
    private bool isGranted;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (tunnel is null)
        {
            var handshakeLength = handshake.Redact(received, output);
            if (!handshake.HasReadRequest)
            {
                return;
            }

            tunnel = new SocksTunnelBytesRedactor();
            received = received[handshakeLength..];
        }

        tunnel.Redact(received, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        if (isGranted)
        {
            tunnel!.ObserveSent(sent);
        }
        else if (IsSuccessReply(sent))
        {
            isGranted = true;
            tunnel ??= new SocksTunnelBytesRedactor();
        }
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        // The handshake redactor holds no bytes back.
        ArgumentNullException.ThrowIfNull(output);
        tunnel?.Flush(output);
    }

    private static bool IsSuccessReply(ReadOnlySpan<byte> sent) =>
        sent.StartsWith((ReadOnlySpan<byte>)[0x00, 0x5A])
        || (sent.Length > Socks5MethodSelectionLength && sent.StartsWith((ReadOnlySpan<byte>)[SocksExchange.Socks5Version, 0x00]));
}
