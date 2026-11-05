using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Query datagrams written byte by byte from RFC 1035 section 4.1 and RFC 6891 section 6.1.2,
/// and the zone the UDP tests answer from.
/// </summary>
internal static class DnsTestQueries
{
    /// <summary>The flags word of a standard query with RD set.</summary>
    public const ushort RecursionDesired = 0x0100;

    /// <summary>
    /// <c>example.test</c> as <see cref="DnsTestZones.Example"/> has it, plus <c>big</c>, a CNAME to
    /// <c>bigtxt</c>, which holds twelve 40-byte TXT records: about 680 bytes of answer, past 512
    /// and under 1232.
    /// </summary>
    public static DnsZoneSet LoadZonesWithALargeAnswer() => DnsTestZones.Load((
        "example.test.zone",
        DnsTestZones.Example + "\n" + "big CNAME bigtxt\n" + string.Join(
            '\n',
            Enumerable.Range(0, 12).Select(index => $"bigtxt TXT \"{index:D2}{new string('x', 38)}\""))));

    /// <summary>
    /// A query with ID <paramref name="id"/>, flags <paramref name="flags"/>, QDCOUNT
    /// <paramref name="questionCount"/>, one question for <paramref name="name"/> repeated
    /// <paramref name="questionCount"/> times, and <paramref name="additionals"/> (each a whole
    /// record's bytes) in the additional section.
    /// </summary>
    public static byte[] Query(
        string name,
        ushort type,
        ushort id = 0xBEEF,
        ushort flags = RecursionDesired,
        int questionCount = 1,
        ushort questionClass = 1,
        params byte[][] additionals)
    {
        var bytes = new List<byte>
        {
            (byte)(id >> 8), (byte)id, (byte)(flags >> 8), (byte)flags,
            0, (byte)questionCount, 0, 0, 0, 0, 0, (byte)additionals.Length,
        };

        for (var index = 0; index < questionCount; index++)
        {
            bytes.AddRange(Name(name));
            bytes.AddRange([(byte)(type >> 8), (byte)type, (byte)(questionClass >> 8), (byte)questionClass]);
        }

        foreach (var additional in additionals)
        {
            bytes.AddRange(additional);
        }

        return [.. bytes];
    }

    /// <summary>
    /// An OPT record (RFC 6891 section 6.1.2): owner the root unless <paramref name="owner"/> is
    /// given, CLASS the payload size, TTL the extended RCODE 0, the version and DO, no options.
    /// </summary>
    public static byte[] Opt(ushort payloadSize, byte version = 0, bool dnssecOk = false, string? owner = null) =>
    [
        .. owner is null ? [0] : Name(owner),
        0x00, 0x29,
        (byte)(payloadSize >> 8), (byte)payloadSize,
        0x00, version, (byte)(dnssecOk ? 0x80 : 0x00), 0x00,
        0x00, 0x00,
    ];

    /// <summary>A name's wire form, uncompressed (RFC 1035 section 3.1).</summary>
    public static byte[] Name(string name)
    {
        var bytes = new List<byte>();

        foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            bytes.Add((byte)label.Length);
            bytes.AddRange(Encoding.ASCII.GetBytes(label));
        }

        bytes.Add(0);
        return [.. bytes];
    }

    /// <summary>Reads <paramref name="datagram"/>, which must be one whole message.</summary>
    public static DnsMessage ReadWhole(byte[] datagram)
    {
        var outcome = DnsMessageReader.Read(datagram);
        Assert.AreEqual(DnsReadFault.None, outcome.Fault);
        return outcome.Message!;
    }
}
