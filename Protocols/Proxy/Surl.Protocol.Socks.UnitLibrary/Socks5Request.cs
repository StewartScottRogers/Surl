using System.Net;

namespace Surl.Protocol.Socks;

/// <summary>
/// A SOCKS5 request as read (RFC 1928 section 4; ADR-0121 decision 3).
/// </summary>
/// <param name="Command"><c>CMD</c>: <see cref="ConnectCommand"/> asks to connect; any other code is answered <c>07</c>.</param>
/// <param name="Address"><c>DST.ADDR</c> for <c>ATYP 01</c> or <c>04</c>; <see langword="null"/> for a domain name.</param>
/// <param name="DomainName"><c>DST.ADDR</c> for <c>ATYP 03</c>, read as Latin-1; <see langword="null"/> for an address.</param>
/// <param name="Port"><c>DST.PORT</c>, 0 to 65535.</param>
internal sealed record Socks5Request(byte Command, IPAddress? Address, string? DomainName, int Port)
{
    /// <summary><c>CMD=01</c>, <c>CONNECT</c>.</summary>
    public const byte ConnectCommand = 0x01;

    /// <summary>
    /// The origin to connect to: <see cref="Address"/> as a literal (IPv6 without brackets), or the domain name.
    /// </summary>
    public string Host => Address?.ToString() ?? DomainName!;
}
