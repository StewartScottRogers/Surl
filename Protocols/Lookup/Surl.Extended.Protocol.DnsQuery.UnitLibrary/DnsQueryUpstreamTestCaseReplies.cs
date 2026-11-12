using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;&lt;dns&gt;</c> section as the DNS server half plays it
/// (ADR-0123 decision 1): the zones that answer every name with the section's addresses, and the
/// log of the queries asked.
/// </summary>
/// <remarks>
/// <para>
/// It behaves as upstream's <c>dnsd</c> does, because the cases depend on it: every QNAME gets the
/// section's <c>A: </c> and <c>AAAA: </c> addresses (a root zone whose wildcard <c>*</c> holds
/// them, RFC 4592, at <c>dnsd</c>'s fixed TTL of 2580 seconds); an address that does not parse,
/// such as <c>none</c>, or a missing line gives NOERROR with an empty answer for that type, never
/// NXDOMAIN, because the wildcard node always exists. A later line of a name replaces an earlier
/// one, and an <c>AAAA: </c> address may be bracketed, as the test variable <c>%HOST6IP</c> is.
/// </para>
/// <para>
/// <see cref="QueryLog"/> is what a case's <c>&lt;verify&gt;&lt;dns&gt;</c> is checked against, one
/// <c>QNAME &lt;name&gt; QTYPE &lt;type&gt;</c> line per query. One instance serves every flow of one
/// case and is safe to use from several at once. Never compose it into the shipped <c>surl</c>; it is
/// a test seam that <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
public sealed class DnsQueryUpstreamTestCaseReplies
{
    private const uint TimeToLive = 2580;
    private const string AddressLineName = "A";
    private const string IPv6AddressLineName = "AAAA";
    private const ushort HttpsType = 65;

    private readonly Lock gate = new();
    private readonly StringBuilder queryLog = new();

    /// <summary>
    /// Holds a case's <c>&lt;dns&gt;</c> section.
    /// </summary>
    /// <param name="dnsLines">The section's lines, <c>NAME: value</c> each; blank lines are ignored.</param>
    /// <exception cref="NotSupportedException">A line names a line other than <see cref="SupportedLineNames"/>; the message is the name.</exception>
    public DnsQueryUpstreamTestCaseReplies(IEnumerable<string> dnsLines)
    {
        ArgumentNullException.ThrowIfNull(dnsLines);

        string? address = null;
        string? ipv6Address = null;
        foreach (var line in dnsLines.Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            var name = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
            switch (name)
            {
                case AddressLineName:
                    address = value;
                    break;
                case IPv6AddressLineName:
                    ipv6Address = value;
                    break;
                default:
                    throw new NotSupportedException(name);
            }
        }

        Zones = BuildZones(address, ipv6Address);
    }

    /// <summary>
    /// The line names of a <c>&lt;dns&gt;</c> section the DNS server half implements: <c>A</c> and
    /// <c>AAAA</c>. A case using any other (<c>HTTPS</c>, the <c>Delay-</c> lines) is skipped
    /// <c>servercmd-unsupported:&lt;name&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedLineNames { get; } =
        new HashSet<string>([AddressLineName, IPv6AddressLineName], StringComparer.Ordinal);

    /// <summary>The zones that answer every name with the section's addresses.</summary>
    public DnsZoneSet Zones { get; }

    /// <summary>
    /// The query log, one <c>QNAME &lt;name&gt; QTYPE &lt;type&gt;</c> line (name without its trailing
    /// dot; type <c>A</c>, <c>AAAA</c>, <c>HTTPS</c> or <c>&lt;unknown&gt;</c>) in the order the queries
    /// arrived: what the case's <c>&lt;verify&gt;&lt;dns&gt;</c> is checked against.
    /// </summary>
    public byte[] QueryLog
    {
        get
        {
            lock (gate)
            {
                return Encoding.Latin1.GetBytes(queryLog.ToString());
            }
        }
    }

    /// <summary>Logs one query, as <see cref="QueryLog"/> describes.</summary>
    /// <param name="question">The query's question.</param>
    internal void RecordQuery(DnsQuestion question)
    {
        var name = question.Name.ToString().TrimEnd('.');
        var type = question.Type switch
        {
            DnsRecordType.A => "A",
            DnsRecordType.AAAA => "AAAA",
            (DnsRecordType)HttpsType => "HTTPS",
            _ => "<unknown>",
        };

        lock (gate)
        {
            queryLog.Append(CultureInfo.InvariantCulture, $"QNAME {name} QTYPE {type}\n");
        }
    }

    private static DnsZoneSet BuildZones(string? address, string? ipv6Address)
    {
        var zone = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"$TTL {TimeToLive}\n")
            .Append("@ SOA ns. hostmaster. 1 2 3 4 30\n")
            .Append("* TXT anchor\n");
        Append(zone, "A", address, AddressFamily.InterNetwork, static text => text);
        Append(zone, "AAAA", ipv6Address, AddressFamily.InterNetworkV6, static text => text.Trim('[', ']'));

        var zones = new DnsZoneSet();
        zones.Add(".zone", Encoding.ASCII.GetBytes(zone.ToString()));
        return zones;
    }

    private static void Append(StringBuilder zone, string type, string? value, AddressFamily family, Func<string, string> unwrap)
    {
        if (value is not null && IPAddress.TryParse(unwrap(value), out var parsed) && parsed.AddressFamily == family)
        {
            zone.Append(CultureInfo.InvariantCulture, $"* {type} {parsed}\n");
        }
    }
}
