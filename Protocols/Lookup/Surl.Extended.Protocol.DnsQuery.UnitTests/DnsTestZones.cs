using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>The zone files the zone and lookup tests load, written by hand from RFC 1035 section 5.</summary>
internal static class DnsTestZones
{
    /// <summary>
    /// <c>example.test.zone</c>: every served type, a CNAME, a wildcard, a loop, a chain leaving
    /// every zone, a delegation with glue, and empty non-terminals.
    /// </summary>
    public const string Example = """
        ; example.test, written by hand from RFC 1035 section 5
        $TTL 3600
        @           IN  SOA ns1 hostmaster (
                            2026110401 ; serial
                            7200 900 1209600
                            300 )      ; minimum
                    IN  NS  ns1
                        MX  10 mail
                        CAA 0 issue "ca.example"
        ns1             A     192.0.2.53
                        AAAA  2001:db8::53
        mail        600 IN A  192.0.2.25
        www         IN 300 CNAME web
        web             A     192.0.2.80
        text            TXT   "hello world" "say \"hi\"" plain \065\.
        _sip._tcp       SRV   10 60 5060 sip
        sip             A     192.0.2.60
        ptr             PTR   www.example.test.
        opaque          TYPE65280 \# 3 ABCDEF
        known           TYPE1 \# 4 C0000207
        *.wild          TXT   "wildcard"
        loop1           CNAME loop2
        loop2           CNAME loop1
        away            CNAME elsewhere.invalid.
        sub             NS    ns.sub
        ns.sub          A     192.0.2.99
        a.b.c           A     192.0.2.7
        """;

    /// <summary>Loads <paramref name="files"/>, each a file name and its text, into one set.</summary>
    public static DnsZoneSet Load(params (string FileName, string Text)[] files)
    {
        var zones = new DnsZoneSet();

        foreach (var (fileName, text) in files)
        {
            zones.Add(fileName, Encoding.UTF8.GetBytes(text));
        }

        return zones;
    }

    /// <summary>The set holding <see cref="Example"/> alone.</summary>
    public static DnsZoneSet LoadExample() => Load(("example.test.zone", Example));

    /// <summary>A minimal zone - a $TTL and an SOA at the apex - followed by <paramref name="lines"/>.</summary>
    public static string Minimal(params string[] lines) =>
        string.Join('\n', new[] { "$TTL 60", "@ SOA ns hostmaster 1 2 3 4 30" }.Concat(lines));

    /// <summary>Returns the fault <see cref="DnsZoneSet.Add"/> reports for <paramref name="text"/> in <c>example.test.zone</c>.</summary>
    public static string FaultOf(string text) => FaultOf("example.test.zone", Encoding.UTF8.GetBytes(text));

    /// <summary>Returns the fault <see cref="DnsZoneSet.Add"/> reports for the file given.</summary>
    public static string FaultOf(string fileName, byte[] content, DnsZoneSet? zones = null) =>
        Assert.ThrowsExactly<DnsZoneFileException>(() => (zones ?? new DnsZoneSet()).Add(fileName, content)).Message;

    /// <summary>The question for <paramref name="name"/> and <paramref name="type"/> in class IN.</summary>
    public static DnsQuestion Question(string name, DnsRecordType type, ushort recordClass = DnsClasses.Internet) =>
        new(DnsName.Parse(name), type, recordClass);
}
