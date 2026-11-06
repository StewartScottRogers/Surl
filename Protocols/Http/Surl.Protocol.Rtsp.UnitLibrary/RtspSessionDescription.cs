using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// Writes the RFC 4566 session description <c>DESCRIBE</c> answers for a presentation: one
/// stream whose RTP payloads are the file's bytes, in order (ADR-0074 decision 4).
/// </summary>
internal static class RtspSessionDescription
{
    /// <summary>
    /// The description of the file named <paramref name="fileName"/>, served on a connection
    /// whose local endpoint is <paramref name="localEndPoint"/>, as UTF-8 bytes.
    /// </summary>
    /// <remarks>
    /// The origin line names the local address, <c>IP4</c> or <c>IP6</c> by its family (an
    /// IPv4 address mapped into IPv6 is written as IPv4); an endpoint with no IP address is
    /// written <c>IP4 0.0.0.0</c>. The file name has its control characters dropped, as
    /// ADR-0006 section 3 escapes peer-visible names, and keeps the rest, UTF-8 included.
    /// </remarks>
    /// <param name="fileName">The presentation file's name, without its directory.</param>
    /// <param name="localEndPoint">The connection's local endpoint.</param>
    /// <returns>The description's bytes.</returns>
    public static byte[] Describe(string fileName, EndPoint localEndPoint)
    {
        var (addressType, address, anyAddress) = OriginAddress(localEndPoint);
        var text = new StringBuilder()
            .Append("v=0\r\n")
            .Append("o=- 0 0 IN ").Append(addressType).Append(' ').Append(address).Append("\r\n")
            .Append("s=").Append(WithoutControlCharacters(fileName)).Append("\r\n")
            .Append("c=IN ").Append(addressType).Append(' ').Append(anyAddress).Append("\r\n")
            .Append("t=0 0\r\n")
            .Append("a=control:*\r\n")
            .Append("a=range:npt=0-\r\n")
            .Append("m=application 0 RTP/AVP 96\r\n")
            .Append("a=rtpmap:96 octet-stream/90000\r\n")
            .Append("a=control:*\r\n");

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static (string AddressType, string Address, string AnyAddress) OriginAddress(EndPoint localEndPoint)
    {
        if (localEndPoint is not IPEndPoint { Address: var address })
        {
            return ("IP4", "0.0.0.0", "0.0.0.0");
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        // An IPv6 address is written without its scope ID: SDP has no "%" in an address.
        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? ("IP6", new IPAddress(address.GetAddressBytes()).ToString(), "::")
            : ("IP4", address.ToString(), "0.0.0.0");
    }

    private static string WithoutControlCharacters(string name) =>
        string.Concat(name.Where(character => !char.IsControl(character)));
}
