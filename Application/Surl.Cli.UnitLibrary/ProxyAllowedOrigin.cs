using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Surl.Cli;

/// <summary>
/// One <c>--proxy-allow-origin</c> argument (ADR-0088 decisions 2 and 7): an address range the proxy may also
/// connect to besides loopback, or <see cref="EveryAddress"/> for <c>*</c>. <c>Surl.Composition</c> widens
/// <c>AllowedOriginRule.LoopbackOnly</c> by each one.
/// </summary>
public sealed record ProxyAllowedOrigin
{
    private ProxyAllowedOrigin(IPAddress? network, int prefixLength)
    {
        Network = network;
        PrefixLength = prefixLength;
    }

    /// <summary><c>--proxy-allow-origin '*'</c>: every origin address, a loosening option.</summary>
    public static ProxyAllowedOrigin EveryAddress { get; } = new(null, 0);

    /// <summary>
    /// The range's address, an IPv4 or IPv6 address and never an IPv4-mapped IPv6 one; <see langword="null"/>
    /// for <see cref="EveryAddress"/>.
    /// </summary>
    public IPAddress? Network { get; }

    /// <summary>How many leading bits of <see cref="Network"/> must match: 0 to 32 for IPv4, 0 to 128 for IPv6.</summary>
    public int PrefixLength { get; }

    /// <summary>Whether this is <see cref="EveryAddress"/>.</summary>
    [MemberNotNullWhen(false, nameof(Network))]
    public bool AdmitsEveryAddress => Network is null;

    /// <summary>The range <paramref name="network"/>/<paramref name="prefixLength"/>.</summary>
    /// <param name="network">The range's address; an IPv4-mapped IPv6 address is taken as IPv4.</param>
    /// <param name="prefixLength">How many leading bits must match: 0 to 32 for IPv4, 0 to 128 for IPv6.</param>
    /// <returns>The range.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="prefixLength"/> is outside its family's range.</exception>
    public static ProxyAllowedOrigin Range(IPAddress network, int prefixLength)
    {
        ArgumentNullException.ThrowIfNull(network);

        var normalized = network.IsIPv4MappedToIPv6 ? network.MapToIPv4() : network;
        ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(prefixLength, HighestPrefixLength(normalized));

        return new ProxyAllowedOrigin(normalized, prefixLength);
    }

    /// <summary>The longest prefix <paramref name="address"/>'s family has: 32 for IPv4, 128 for IPv6.</summary>
    /// <param name="address">An IPv4 or IPv6 address.</param>
    /// <returns>Its family's bit count.</returns>
    internal static int HighestPrefixLength(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;

    /// <summary>Names the range as <c>--proxy-allow-origin</c> takes it: <c>*</c> or <c>address/prefix</c>.</summary>
    /// <returns>The range as text.</returns>
    public override string ToString() => AdmitsEveryAddress ? "*" : $"{Network}/{PrefixLength}";
}
