using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// The address rules for FTP data connections (ADR-0052, decisions 6 and 9): a passive listener
/// takes a connection only from the control connection's peer (RFC 2577 section 4, port
/// stealing), and an active connection goes only to the peer's own address on a port of 1024 or
/// more (RFC 2577 section 3, bounce attacks). An IPv4-mapped IPv6 address is compared as IPv4.
/// </summary>
internal static class DataConnectionPeer
{
    /// <summary>
    /// The lowest port an active data connection may go to.
    /// </summary>
    public const int LowestActivePort = 1024;

    /// <summary>
    /// <paramref name="address"/> as IPv4 when it is an IPv4-mapped IPv6 address; otherwise itself.
    /// </summary>
    /// <param name="address">The address to normalize.</param>
    /// <returns>The address to compare, bind or announce.</returns>
    public static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    /// <summary>
    /// Says whether <paramref name="candidate"/> has the address of <paramref name="controlRemote"/>.
    /// </summary>
    /// <param name="controlRemote">The control connection's remote end point, curl's address.</param>
    /// <param name="candidate">The end point a data connection comes from or goes to.</param>
    /// <returns>
    /// <see langword="true"/> when both are IP end points with the same normalized address, or when
    /// <paramref name="controlRemote"/> is a Unix socket peer and <paramref name="candidate"/> is an IP end
    /// point on a loopback address.
    /// </returns>
    /// <remarks>
    /// A control connection that came over a Unix domain socket has no IP address, and its peer is on
    /// this machine by definition, so its peer's address is any loopback address (ADR-0094,
    /// decision 2; BL-605).
    /// </remarks>
    public static bool IsPeer(EndPoint controlRemote, EndPoint candidate) =>
        candidate is IPEndPoint other
        && (controlRemote is IPEndPoint peer
            ? Normalize(peer.Address).Equals(Normalize(other.Address))
            : controlRemote is UnnamedUnixSocketPeer or UnixDomainSocketEndPoint && IPAddress.IsLoopback(Normalize(other.Address)));

    /// <summary>
    /// Says whether an active data connection may go to <paramref name="target"/>.
    /// </summary>
    /// <param name="controlRemote">The control connection's remote end point, curl's address.</param>
    /// <param name="target">The address and port curl named in <c>EPRT</c> or <c>PORT</c>.</param>
    /// <returns><see langword="true"/> for the peer's own address on port <see cref="LowestActivePort"/> or above.</returns>
    public static bool IsAllowedActiveTarget(EndPoint controlRemote, IPEndPoint target) =>
        target.Port >= LowestActivePort && IsPeer(controlRemote, target);
}
