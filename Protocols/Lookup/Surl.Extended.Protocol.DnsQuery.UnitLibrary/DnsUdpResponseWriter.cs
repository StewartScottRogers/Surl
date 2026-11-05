namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Writes a <see cref="DnsResponse"/> as one UDP datagram no longer than the requester can take
/// (ADR-0092 decision 3; RFC 1035 section 4.2.1, RFC 6891 sections 6.2.3 to 6.2.5, RFC 2181
/// section 9).
/// </summary>
/// <remarks>
/// The limit is 512 bytes without an OPT in the query; with one, its payload size, a value below
/// 512 taken as 512, and never more than <see cref="DnsQueryAnswerer.ServerPayloadSize"/>. A
/// response past it is cut as <see cref="DnsResponseWriter.WriteWithin"/> says.
/// </remarks>
public static class DnsUdpResponseWriter
{
    /// <summary>The most a UDP answer holds without EDNS(0) (RFC 1035 section 4.2.1).</summary>
    public const int ClassicPayloadSize = 512;

    /// <summary>Returns the most bytes a UDP answer to a query with <paramref name="queryOpt"/> may hold.</summary>
    public static int LimitFor(DnsOptRecord? queryOpt) => queryOpt is null
        ? ClassicPayloadSize
        : Math.Clamp((int)queryOpt.PayloadSize, ClassicPayloadSize, DnsQueryAnswerer.ServerPayloadSize);

    /// <summary>Returns the datagram that answers with <paramref name="response"/>, cut to fit as the remarks say.</summary>
    public static byte[] Write(DnsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return DnsResponseWriter.WriteWithin(response, LimitFor(response.QueryOpt));
    }
}
