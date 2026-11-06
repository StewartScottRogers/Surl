using System.Net;
using System.Net.Sockets;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The RDATA of A, four bytes of an IPv4 address (RFC 1035 section 3.4.1), and of AAAA, sixteen
/// bytes of an IPv6 address (RFC 3596 section 2.2). The address family chooses the type.
/// </summary>
public sealed record DnsAddressData : DnsRecordData
{
    /// <summary>Creates the RDATA holding <paramref name="address"/>.</summary>
    public DnsAddressData(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    /// <summary>The address.</summary>
    public IPAddress Address { get; }

    /// <inheritdoc/>
    public override DnsRecordType Type =>
        Address.AddressFamily == AddressFamily.InterNetwork ? DnsRecordType.A : DnsRecordType.AAAA;

    /// <inheritdoc/>
    internal override void WriteTo(DnsMessageWriter writer) => writer.WriteBytes(Address.GetAddressBytes());
}
