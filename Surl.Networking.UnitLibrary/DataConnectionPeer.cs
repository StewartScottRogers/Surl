using System.Net;

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
    /// <returns><see langword="true"/> only when both are IP end points with the same normalized address.</returns>
    public static bool IsPeer(EndPoint controlRemote, EndPoint candidate) =>
        controlRemote is IPEndPoint peer
        && candidate is IPEndPoint other
        && Normalize(peer.Address).Equals(Normalize(other.Address));

    /// <summary>
    /// Says whether an active data connection may go to <paramref name="target"/>.
    /// </summary>
    /// <param name="controlRemote">The control connection's remote end point, curl's address.</param>
    /// <param name="target">The address and port curl named in <c>EPRT</c> or <c>PORT</c>.</param>
    /// <returns><see langword="true"/> for the peer's own address on port <see cref="LowestActivePort"/> or above.</returns>
    public static bool IsAllowedActiveTarget(EndPoint controlRemote, IPEndPoint target) =>
        target.Port >= LowestActivePort && IsPeer(controlRemote, target);
}
