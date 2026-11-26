using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for the bytes a client sends through a granted SOCKS
/// tunnel (ADR-0182, amendments 6, 7, 9 and 10). SOCKS does not say what protocol the tunnel
/// carries, so the first byte decides. An ASCII letter (a text protocol's command or request line, or
/// the HTTP/2 connection preface) or a digit from 1 to 9 (a numbered IMAP tag) sends the whole tunnel
/// through <see cref="SocksTextTunnelBytesRedactor"/>; <c>0x10</c> (an MQTT <c>CONNECT</c>) through
/// <see cref="SocksTunnelMqttConnectRedactor"/>; <c>0x30</c> (an LDAP message) through
/// <see cref="SocksTunnelLdapBindRedactor"/>. Any other first byte (TLS and other binary protocols)
/// leaves the whole tunnel logged byte for byte, never held back.
/// </summary>
internal sealed class SocksTunnelBytesRedactor : IReceivedBytesRedactor
{
    private const byte MqttConnectFirstByte = 0x10;

    // BER's SEQUENCE is also the digit 0, so a text tunnel starting with that digit is read as LDAP.
    private const byte LdapMessageFirstByte = 0x30;

    private IReceivedBytesRedactor? tunnelled;
    private bool isDecided;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!isDecided && received.Length > 0)
        {
            isDecided = true;
            tunnelled = RedactorFor(received[0]);
        }

        if (tunnelled is null)
        {
            output.Write(received);
        }
        else
        {
            tunnelled.Redact(received, output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => tunnelled?.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        tunnelled?.Flush(output);
    }

    private static IReceivedBytesRedactor? RedactorFor(byte first) => first switch
    {
        MqttConnectFirstByte => new SocksTunnelMqttConnectRedactor(),
        LdapMessageFirstByte => new SocksTunnelLdapBindRedactor(),
        _ => StartsText(first) ? new SocksTextTunnelBytesRedactor() : null,
    };

    private static bool StartsText(byte first) =>
        char.IsAsciiLetter((char)first) || first is >= (byte)'1' and <= (byte)'9';
}
