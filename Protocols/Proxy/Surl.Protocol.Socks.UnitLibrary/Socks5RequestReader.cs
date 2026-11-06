using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Surl.Protocol.Socks;

/// <summary>
/// Reads a SOCKS5 request: <c>VER</c>, <c>CMD</c>, <c>RSV</c>, <c>ATYP</c>, <c>DST.ADDR</c> and
/// <c>DST.PORT</c> (RFC 1928 section 4; ADR-0121 decision 3). A domain name's length byte bounds
/// it at 255 bytes.
/// </summary>
internal static class Socks5RequestReader
{
    private const byte IPv4AddressType = 0x01;
    private const byte DomainNameAddressType = 0x03;
    private const byte IPv6AddressType = 0x04;

    /// <summary>
    /// Reads the request.
    /// </summary>
    /// <param name="reader">curl's connection, positioned after the method selection or the RFC 1929 sub-negotiation.</param>
    /// <param name="cancellationToken">Cuts the read off: the handshake timeout or the exchange's token.</param>
    /// <returns>How the read ended, and the request when it was read whole.</returns>
    public static async Task<(Socks5ReadOutcome Outcome, Socks5Request? Request)> ReadAsync(SocksClientReader reader, CancellationToken cancellationToken)
    {
        var header = await reader.ReadBytesAsync(4, cancellationToken);
        if (header is null)
        {
            return (Socks5ReadOutcome.Truncated, null);
        }

        if (header[0] != SocksExchange.Socks5Version)
        {
            return (Socks5ReadOutcome.WrongVersion, null);
        }

        var (outcome, address, domainName) = await ReadAddressAsync(reader, header[3], cancellationToken);
        if (outcome != Socks5ReadOutcome.Read)
        {
            return (outcome, null);
        }

        var port = await reader.ReadBytesAsync(2, cancellationToken);

        return port is null
            ? (Socks5ReadOutcome.Truncated, null)
            : (Socks5ReadOutcome.Read, new Socks5Request(header[1], address, domainName, BinaryPrimitives.ReadUInt16BigEndian(port)));
    }

    private static async Task<(Socks5ReadOutcome Outcome, IPAddress? Address, string? DomainName)> ReadAddressAsync(
        SocksClientReader reader, byte addressType, CancellationToken cancellationToken)
    {
        switch (addressType)
        {
            case IPv4AddressType:
            case IPv6AddressType:
                var address = await reader.ReadBytesAsync(addressType == IPv4AddressType ? 4 : 16, cancellationToken);

                return address is null ? (Socks5ReadOutcome.Truncated, null, null) : (Socks5ReadOutcome.Read, new IPAddress(address), null);
            case DomainNameAddressType:
                var name = await reader.ReadLengthPrefixedAsync(cancellationToken);

                return name is null ? (Socks5ReadOutcome.Truncated, null, null) : (Socks5ReadOutcome.Read, null, Encoding.Latin1.GetString(name));
            default:
                return (Socks5ReadOutcome.AddressTypeNotSupported, null, null);
        }
    }
}
