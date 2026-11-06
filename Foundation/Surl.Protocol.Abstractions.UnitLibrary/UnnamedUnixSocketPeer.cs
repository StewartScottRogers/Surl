using System.Net;
using System.Net.Sockets;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// The remote endpoint of a Unix domain socket connection whose client bound no name, as curl
/// never does (ADR-0094, decision 6). It has no address, so code that needs an IP address finds
/// no <see cref="IPEndPoint"/>, and the exchange log names it <c>unix peer</c>.
/// </summary>
public sealed class UnnamedUnixSocketPeer : EndPoint
{
    private UnnamedUnixSocketPeer()
    {
    }

    /// <summary>
    /// The one instance every unnamed Unix socket peer shares.
    /// </summary>
    public static UnnamedUnixSocketPeer Instance { get; } = new();

    /// <summary>
    /// <see cref="AddressFamily.Unix"/>.
    /// </summary>
    public override AddressFamily AddressFamily => AddressFamily.Unix;

    /// <summary>
    /// Returns <c>unix peer</c>.
    /// </summary>
    /// <returns><c>unix peer</c>.</returns>
    public override string ToString() => "unix peer";
}
