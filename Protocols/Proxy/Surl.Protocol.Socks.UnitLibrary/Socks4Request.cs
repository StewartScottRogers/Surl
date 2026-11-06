using System.Net;

namespace Surl.Protocol.Socks;

/// <summary>
/// A SOCKS4 or SOCKS4a request as read after its version byte (ADR-0121 decision 2).
/// </summary>
/// <param name="Command">
/// <c>CD</c>: <see cref="Socks4Request.ConnectCommand"/> asks to connect; any other code is refused.
/// </param>
/// <param name="Port"><c>DSTPORT</c>, 0 to 65535.</param>
/// <param name="Address"><c>DSTIP</c>, or <see langword="null"/> for a SOCKS4a request, whose <c>DSTIP</c> is <c>0.0.0.x</c> with <c>x</c> not zero.</param>
/// <param name="UserId"><c>USERID</c>, read as Latin-1; empty when curl sent none.</param>
/// <param name="HostName">The SOCKS4a host name, read as Latin-1; <see langword="null"/> for a SOCKS4 request.</param>
internal sealed record Socks4Request(byte Command, int Port, IPAddress? Address, string UserId, string? HostName)
{
    /// <summary>
    /// <c>CD=01</c>, <c>CONNECT</c>.
    /// </summary>
    public const byte ConnectCommand = 0x01;

    /// <summary>
    /// The origin to connect to: <see cref="Address"/> as a literal, or the SOCKS4a host name.
    /// </summary>
    public string Host => Address?.ToString() ?? HostName!;
}
