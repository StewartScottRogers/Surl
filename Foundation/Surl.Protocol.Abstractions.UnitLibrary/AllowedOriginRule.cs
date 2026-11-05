using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which origin end points a proxy may connect to (ADR-0088, decisions 2 and 7): secure by
/// default, only loopback (<c>127.0.0.0/8</c> and <c>::1</c>), widened by
/// <c>--proxy-allow-origin</c> ranges or to every address, and never one of the proxy's own
/// listening end points, so a request cannot loop through the proxy. An IPv4-mapped IPv6 address
/// is compared as IPv4. The composition root builds it; the <see cref="IOriginConnector"/> in
/// <c>Surl.Networking</c> applies it to every address a host resolves to. It is immutable: each
/// <c>With</c> method returns a new rule.
/// </summary>
public sealed class AllowedOriginRule
{
    private readonly bool admitsEveryAddress;
    private readonly IReadOnlyList<(IPAddress Network, int PrefixLength)> ranges;
    private readonly IReadOnlyList<IPEndPoint> proxyEndPoints;

    private AllowedOriginRule(
        bool admitsEveryAddress,
        IReadOnlyList<(IPAddress Network, int PrefixLength)> ranges,
        IReadOnlyList<IPEndPoint> proxyEndPoints)
    {
        this.admitsEveryAddress = admitsEveryAddress;
        this.ranges = ranges;
        this.proxyEndPoints = proxyEndPoints;
    }

    /// <summary>
    /// The default rule: loopback addresses only.
    /// </summary>
    public static AllowedOriginRule LoopbackOnly { get; } = new(false, [], []);

    /// <summary>
    /// The rule <c>--proxy-allow-origin '*'</c> builds: every address, with the proxy's own end
    /// points still refused once added.
    /// </summary>
    public static AllowedOriginRule EveryAddress { get; } = new(true, [], []);

    /// <summary>
    /// Returns this rule also admitting every address in
    /// <paramref name="network"/>/<paramref name="prefixLength"/>.
    /// </summary>
    /// <param name="network">The range's address; an IPv4-mapped one is taken as IPv4.</param>
    /// <param name="prefixLength">How many leading bits must match: 0 to 32 for IPv4, 0 to 128 for IPv6.</param>
    /// <returns>The widened rule.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="prefixLength"/> is outside its family's range.</exception>
    public AllowedOriginRule WithRange(IPAddress network, int prefixLength)
    {
        ArgumentNullException.ThrowIfNull(network);

        var normalized = Normalize(network);
        ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(prefixLength, normalized.GetAddressBytes().Length * 8);

        return new AllowedOriginRule(admitsEveryAddress, [.. ranges, (normalized, prefixLength)], proxyEndPoints);
    }

    /// <summary>
    /// Returns this rule also refusing <paramref name="proxyEndPoint"/>, an end point the proxy
    /// listens on. An unspecified address (<c>0.0.0.0</c> or <c>::</c>) refuses its port on every
    /// address.
    /// </summary>
    /// <param name="proxyEndPoint">The proxy's listening end point.</param>
    /// <returns>The narrowed rule.</returns>
    public AllowedOriginRule WithProxyEndPoint(IPEndPoint proxyEndPoint)
    {
        ArgumentNullException.ThrowIfNull(proxyEndPoint);

        return new AllowedOriginRule(admitsEveryAddress, ranges, [.. proxyEndPoints, proxyEndPoint]);
    }

    /// <summary>
    /// Says whether a proxy may connect to <paramref name="origin"/>.
    /// </summary>
    /// <param name="origin">One address the origin's host resolved to, with the origin's port.</param>
    /// <returns>
    /// <see langword="true"/> when the address is admitted and the end point is not one of the
    /// proxy's own.
    /// </returns>
    public bool Admits(IPEndPoint origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        var address = Normalize(origin.Address);

        return AdmitsAddress(address) && !proxyEndPoints.Any(proxy => IsProxyEndPoint(proxy, address, origin.Port));
    }

    private bool AdmitsAddress(IPAddress address) =>
        admitsEveryAddress
        || IPAddress.IsLoopback(address)
        || ranges.Any(range => IsInRange(address, range.Network, range.PrefixLength));

    private static bool IsProxyEndPoint(IPEndPoint proxy, IPAddress address, int port)
    {
        var proxyAddress = Normalize(proxy.Address);

        return proxy.Port == port
            && (proxyAddress.Equals(address) || proxyAddress.Equals(IPAddress.Any) || proxyAddress.Equals(IPAddress.IPv6Any));
    }

    private static bool IsInRange(IPAddress address, IPAddress network, int prefixLength)
    {
        if (address.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        if (!addressBytes.AsSpan(0, wholeBytes).SequenceEqual(networkBytes.AsSpan(0, wholeBytes)))
        {
            return false;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));

        return remainingBits == 0 || (addressBytes[wholeBytes] & mask) == (networkBytes[wholeBytes] & mask);
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
