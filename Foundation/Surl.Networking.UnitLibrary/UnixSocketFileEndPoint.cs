using System.Net;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// A Unix domain socket address bound without handing its file to .NET (ADR-0094, decision 5).
/// <see cref="Socket.Bind"/> given a <see cref="UnixDomainSocketEndPoint"/> makes
/// <see cref="Socket.Dispose()"/> delete the file at its path, whatever stands there by then;
/// given this endpoint, which serializes to the same address, the socket leaves the file to
/// <see cref="UnixSocketConnectionListener"/>, which deletes it only while it is still the one
/// the bind created. Endpoints the socket reads back (its local and accepted peers') are
/// <see cref="UnixDomainSocketEndPoint"/>s.
/// </summary>
/// <param name="address">The address to bind.</param>
internal sealed class UnixSocketFileEndPoint(UnixDomainSocketEndPoint address) : EndPoint
{
    /// <summary>
    /// <see cref="AddressFamily.Unix"/>.
    /// </summary>
    public override AddressFamily AddressFamily => AddressFamily.Unix;

    /// <summary>
    /// The address the socket binds, as <see cref="UnixDomainSocketEndPoint"/> serializes it.
    /// </summary>
    /// <returns>The serialized address.</returns>
    public override SocketAddress Serialize() => address.Serialize();

    /// <summary>
    /// Reads an address the socket reports as the <see cref="UnixDomainSocketEndPoint"/> it is.
    /// </summary>
    /// <param name="socketAddress">The address the socket reported.</param>
    /// <returns>The endpoint.</returns>
    public override EndPoint Create(SocketAddress socketAddress) => address.Create(socketAddress);

    /// <summary>
    /// The address's path or name, as <see cref="UnixDomainSocketEndPoint"/> writes it.
    /// </summary>
    /// <returns>The path or name.</returns>
    public override string ToString() => address.ToString();
}
