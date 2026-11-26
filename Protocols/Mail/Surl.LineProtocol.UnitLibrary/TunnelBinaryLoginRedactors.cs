using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// Chooses, by a tunnel's first byte, the redactor for a binary protocol's login, for the SOCKS server
/// and the HTTP proxy alike (ADR-0182, amendments 10 and 23): <c>0x10</c> is an MQTT <c>CONNECT</c>
/// and <c>0x30</c> a BER <c>SEQUENCE</c>, as every <c>LDAPMessage</c> is.
/// </summary>
public static class TunnelBinaryLoginRedactors
{
    private const byte MqttConnectFirstByte = 0x10;

    // BER's SEQUENCE is also the digit 0, so a text tunnel starting with that digit is read as LDAP.
    private const byte LdapMessageFirstByte = 0x30;

    /// <summary>The redactor for a tunnel that starts with <paramref name="firstByte"/>.</summary>
    /// <param name="firstByte">The first byte the client sends through the tunnel.</param>
    /// <returns>
    /// A new <see cref="TunnelMqttConnectRedactor"/> or <see cref="TunnelLdapBindRedactor"/>, or
    /// <see langword="null"/> when the byte starts neither.
    /// </returns>
    public static IReceivedBytesRedactor? ForFirstByte(byte firstByte) => firstByte switch
    {
        MqttConnectFirstByte => new TunnelMqttConnectRedactor(),
        LdapMessageFirstByte => new TunnelLdapBindRedactor(),
        _ => null,
    };
}
