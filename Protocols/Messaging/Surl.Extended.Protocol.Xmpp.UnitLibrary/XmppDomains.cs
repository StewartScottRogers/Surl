using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The domains one connection's streams may name in <c>to</c> (ADR-0114 decision 1), lower-cased,
/// the first being the one a header with no <c>to</c> opens a stream for.
/// </summary>
internal static class XmppDomains
{
    /// <summary>
    /// Lists the domains served on a connection: the listen URL's host; on a wildcard listen
    /// address, the connection's local address instead; on a loopback listen (<c>localhost</c> or
    /// a loopback address), both <c>localhost</c> and the loopback literal; then every mail domain.
    /// </summary>
    /// <param name="listenUrl">The listen URL that accepted the connection.</param>
    /// <param name="localEndPoint">The connection's local endpoint.</param>
    /// <param name="mailDomains">Every <c>--mail-domain</c>.</param>
    /// <returns>The domains, without duplicates, in that order.</returns>
    public static IReadOnlyList<string> Served(ListenUrl listenUrl, EndPoint localEndPoint, IEnumerable<string> mailDomains)
    {
        var localAddress = (localEndPoint as IPEndPoint)?.Address;
        var domains = ListenDomains(listenUrl.Host.ToLowerInvariant(), localAddress).ToList();
        domains.AddRange(mailDomains.Select(Normalized));

        return domains.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The form a domain is compared in: lower-cased, a trailing dot removed (RFC 7622 section
    /// 3.2), and an IP literal written as <see cref="Literal"/> writes it.
    /// </summary>
    /// <param name="domain">The domain as a client or the configuration wrote it.</param>
    /// <returns>The domain to compare.</returns>
    public static string Normalized(string domain)
    {
        var trimmed = domain.TrimEnd('.').ToLowerInvariant();
        var unbracketed = trimmed.StartsWith('[') && trimmed.EndsWith(']') ? trimmed[1..^1] : trimmed;

        return IPAddress.TryParse(unbracketed, out var address) ? Literal(address) : trimmed;
    }

    /// <summary>
    /// Writes an address as a domainpart (RFC 7622 section 3.2): IPv4 dotted, IPv6 in brackets
    /// with no zone, an IPv4-mapped IPv6 address as its IPv4 address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The IP literal.</returns>
    public static string Literal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{new IPAddress(address.GetAddressBytes())}]"
            : address.ToString();
    }

    private static IEnumerable<string> ListenDomains(string host, IPAddress? localAddress)
    {
        if (!IPAddress.TryParse(host, out var listenAddress))
        {
            return host == "localhost" ? LoopbackDomains(localAddress) : [host];
        }

        if (IsWildcard(listenAddress))
        {
            return localAddress is null ? [] : [Literal(localAddress)];
        }

        return IPAddress.IsLoopback(listenAddress) ? ["localhost", Literal(listenAddress)] : [Literal(listenAddress)];
    }

    private static bool IsWildcard(IPAddress address) => address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);

    // A localhost listen: the loopback address the connection arrived on, when it arrived on one.
    private static IEnumerable<string> LoopbackDomains(IPAddress? localAddress) =>
        localAddress is not null && IPAddress.IsLoopback(localAddress) ? ["localhost", Literal(localAddress)] : ["localhost"];
}
