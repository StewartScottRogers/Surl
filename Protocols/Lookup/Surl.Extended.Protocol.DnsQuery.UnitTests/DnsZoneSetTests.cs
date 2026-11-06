using System.Text;
using static Surl.Extended.Protocol.DnsQuery.DnsTestZones;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Loading zone files (RFC 1035 section 5; ADR-0092 decision 1): what each loads as, and the
/// fault, with its line, each malformed file is refused with.
/// </summary>
[TestClass]
public sealed class DnsZoneSetTests
{
    [TestMethod]
    public void Add_ZoneFileWithEveryServedType_LoadsTheZoneAtItsFileName()
    {
        var zones = LoadExample();

        Assert.AreEqual("example.test.", zones.Apexes.Single().ToString());
    }

    [TestMethod]
    public void Add_CrlfLinesOriginTabsAndClassBeforeTtl_ReadsEachRecordIntoItsSet()
    {
        var text = "$ttl 60\r\n@ SOA ns hostmaster 1 2 3 4 30\r\n$ORIGIN sub\r\nwww IN 120 A 192.0.2.1\r\n$origin example.test.\r\n@ CLASS1 TXT x\r\n\tTXT\ty\r\n";

        var zones = Load(("example.test.zone", text));

        Assert.AreEqual(120u, zones.Answer(Question("www.sub.example.test", DnsRecordType.A)).Answers.Single().TimeToLive);
        Assert.HasCount(2, zones.Answer(Question("example.test", DnsRecordType.TXT)).Answers);
        Assert.AreEqual(DnsResponseCode.Refused, zones.Answer(Question("test", DnsRecordType.A)).ResponseCode);
    }

    [TestMethod]
    public void Add_RootZoneWithDelegations_LoadsAndRefersEachName()
    {
        var zones = Load((".zone", "$TTL 60\n@ SOA ns. hostmaster. 1 2 3 4 30\n@ NS ns.\ns NS ns.\nns A 192.0.2.53"));

        var answer = zones.Answer(Question("www.s", DnsRecordType.A));

        Assert.AreEqual(".", zones.Apexes.Single().ToString());
        Assert.AreEqual("s. NS", answer.Authorities.Select(record => $"{record.Name} {record.Type}").Single());
    }

    [TestMethod]
    public void Add_NoTtlDirective_TakesTheLastTtlWritten()
    {
        var zones = Load(("example.test.zone", "@ 90 SOA ns hostmaster 1 2 3 4 30\nwww A 192.0.2.1"));

        Assert.AreEqual(90u, zones.Answer(Question("www.example.test", DnsRecordType.A)).Answers.Single().TimeToLive);
    }

    [TestMethod]
    public void Add_NamesWithEscapesAndRoot_ReadsEachLabelByte()
    {
        var zones = Load(("example.test.zone", Minimal(@"a\.b CNAME \065bc.", @"root PTR .")));

        var escaped = zones.Answer(new DnsQuestion(new DnsName([Encoding.ASCII.GetBytes("a.b"), .. DnsName.Parse("example.test").Labels.Select(label => label.ToArray())]), DnsRecordType.CNAME, DnsClasses.Internet));

        Assert.AreEqual("Abc.", ((DnsDomainNameData)escaped.Answers.Single().Data).Name.ToString());
        Assert.AreEqual(".", ((DnsDomainNameData)zones.Answer(Question("root.example.test", DnsRecordType.PTR)).Answers.Single().Data).Name.ToString());
    }

    [TestMethod]
    public void Add_CharacterStringsWithUtf8AndSurrogatePairs_KeepsTheirBytes()
    {
        var zones = Load(("example.test.zone", Minimal("t TXT \"café \U0001F600\"")));

        var text = (DnsTextData)zones.Answer(Question("t.example.test", DnsRecordType.TXT)).Answers.Single().Data;

        Assert.AreEqual("café \U0001F600", Encoding.UTF8.GetString(text.Strings.Single().Span));
    }

    [TestMethod]
    public void Add_GenericFormForEveryLength_ReadsOpaqueAndEmptyData()
    {
        var zones = Load(("example.test.zone", Minimal(@"e TYPE65000 \# 0", @"s TYPE65001 \# 4 0102 0304")));

        var empty = (DnsOpaqueData)zones.Answer(Question("e.example.test", (DnsRecordType)65000)).Answers.Single().Data;
        var split = (DnsOpaqueData)zones.Answer(Question("s.example.test", (DnsRecordType)65001)).Answers.Single().Data;

        Assert.AreEqual(0, empty.Bytes.Length);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, split.Bytes.ToArray());
    }

    [TestMethod]
    [DataRow("www A 192.0.2.1 \"unclosed", "line 3: an unclosed quote")]
    [DataRow("www A 192.0.2.1 )", "line 3: a ')' without '('")]
    [DataRow("www TXT ( a\n b", "line 3: a '(' without ')'")]
    [DataRow("$INCLUDE other.zone", "line 3: $INCLUDE is not served")]
    [DataRow("$ORIGIN a b", "line 3: $ORIGIN takes one value")]
    [DataRow("$TTL", "line 3: $TTL takes one value")]
    [DataRow("$GENERATE 1-2 a A 192.0.2.1", "line 3: an unknown directive $GENERATE")]
    [DataRow("$TTL 1h", "line 3: a bad TTL 1h")]
    [DataRow("www 2147483648 A 192.0.2.1", "line 3: a TTL past 2147483647")]
    [DataRow("www 99999999999 A 192.0.2.1", "line 3: a TTL past 2147483647")]
    [DataRow("www 60 IN", "line 3: no type")]
    [DataRow("www \"60\" A 192.0.2.1", "line 3: a quoted \"60\" where a word belongs")]
    [DataRow("www 60 60 A 192.0.2.1", "line 3: an unknown type 60")]
    [DataRow("www IN IN A 192.0.2.1", "line 3: an unknown type IN")]
    [DataRow("www.elsewhere.invalid. A 192.0.2.1", "line 3: an owner outside the zone")]
    [DataRow("www CH A 192.0.2.1", "line 3: a class other than IN")]
    [DataRow("www CLASS3 A 192.0.2.1", "line 3: a class other than IN")]
    [DataRow("www HS A 192.0.2.1", "line 3: a class other than IN")]
    [DataRow("www CS A 192.0.2.1", "line 3: a class other than IN")]
    [DataRow("www NONE A 192.0.2.1", "line 3: a class other than IN")]
    [DataRow("www DNSKEY 1", "line 3: an unknown type DNSKEY")]
    [DataRow("www TYPE70000 1", "line 3: an unknown type TYPE70000")]
    [DataRow("www TYPE 1", "line 3: an unknown type TYPE")]
    [DataRow("www ANY 1", "line 3: a meta-type ANY as a record")]
    [DataRow("www TYPE41 \\# 0", "line 3: a meta-type OPT as a record")]
    [DataRow("www AXFR 1", "line 3: a meta-type AXFR as a record")]
    [DataRow("www IXFR 1", "line 3: a meta-type IXFR as a record")]
    [DataRow("\"www\" A 192.0.2.1", "line 3: a quoted \"www\" where a word belongs")]
    [DataRow("www A 192.0.2", "line 3: bad RDATA for A")]
    [DataRow("www A 192.0.2.1000", "line 3: bad RDATA for A")]
    [DataRow("www A 192.0.2.256", "line 3: bad RDATA for A")]
    [DataRow("www A 192.0.2.1 192.0.2.2", "line 3: bad RDATA for A")]
    [DataRow("www AAAA 192.0.2.1", "line 3: bad RDATA for AAAA")]
    [DataRow("www AAAA fe80::1%1", "line 3: bad RDATA for AAAA")]
    [DataRow("www AAAA ::zz", "line 3: bad RDATA for AAAA")]
    [DataRow("www AAAA ::ffff:192.0.2.1:1", "line 3: bad RDATA for AAAA")]
    [DataRow("www MX 10", "line 3: bad RDATA for MX")]
    [DataRow("www MX ten mail", "line 3: bad RDATA for MX")]
    [DataRow("www MX 65536 mail", "line 3: bad RDATA for MX")]
    [DataRow("www SRV 1 2 3", "line 3: bad RDATA for SRV")]
    [DataRow("www CAA 0 is-sue x", "line 3: bad RDATA for CAA")]
    [DataRow("www CAA 0 \"issue\" x", "line 3: bad RDATA for CAA")]
    [DataRow("www CNAME a..b", "line 3: bad RDATA for CNAME")]
    [DataRow("www TXT", "line 3: bad RDATA for TXT")]
    [DataRow("www TXT \\", "line 3: bad RDATA for TXT")]
    [DataRow("www TXT \\25", "line 3: bad RDATA for TXT")]
    [DataRow("www TXT \\25x", "line 3: bad RDATA for TXT")]
    [DataRow("www TYPE65000 1 2", "line 3: bad RDATA for TYPE65000")]
    [DataRow("www TYPE65000 \\#", "line 3: bad RDATA for TYPE65000")]
    [DataRow("www TYPE65000 \\# 2 ABCDEF", "line 3: bad RDATA for TYPE65000")]
    [DataRow("www TYPE65000 \\# 1 G0", "line 3: bad RDATA for TYPE65000")]
    [DataRow("www A \\# 3 C00002", "line 3: bad RDATA for A")]
    [DataRow("a..b A 192.0.2.1", "line 3: a bad name a..b")]
    [DataRow(".www A 192.0.2.1", "line 3: a bad name .www")]
    [DataRow("café A 192.0.2.1", "line 3: a bad name café")]
    [DataRow("caf\\é A 192.0.2.1", "line 3: a bad name caf\\é")]
    [DataRow("www\\", "line 3: a bad name www\\")]
    [DataRow("www\\2 A 192.0.2.1", "line 3: a bad name www\\2")]
    [DataRow("www\\999 A 192.0.2.1", "line 3: a bad name www\\999")]
    [DataRow("$ORIGIN a..b", "line 3: a bad name a..b")]
    [DataRow("@ SOA ns hostmaster 1 2 3 4 5", "line 3: a second SOA")]
    [DataRow("www SOA ns hostmaster 1 2 3 4 5", "line 3: an SOA below the apex")]
    [DataRow("www SOA ns hostmaster 1 2 3 4", "line 3: bad RDATA for SOA")]
    [DataRow("@ CNAME www", "line 3: a CNAME beside other data")]
    [DataRow("www CNAME a\nwww CNAME b", "line 4: a second CNAME")]
    [DataRow("www CNAME a\nwww A 192.0.2.1", "line 4: a CNAME beside other data")]
    [DataRow("www A 192.0.2.1\nwww CNAME a", "line 4: a CNAME beside other data")]
    [DataRow("www A 192.0.2.1\nwww 61 A 192.0.2.2", "line 4: records of one set with different TTLs")]
    [DataRow("www A 192.0.2.1\nWWW A 192.0.2.1", "line 4: a record written twice")]
    [DataRow("*.x NS ns", "line 3: a wildcard NS")]
    public void Add_MalformedLine_RefusesTheZoneNamingTheLine(string line, string fault)
    {
        var message = FaultOf(Minimal(line));

        Assert.AreEqual(fault, message);
    }

    [TestMethod]
    public void Add_BlankOwnerOnTheFirstRecord_RefusesTheZone()
    {
        var message = FaultOf("$TTL 60\n  SOA ns hostmaster 1 2 3 4 30");

        Assert.AreEqual("line 2: a blank owner with no owner before it", message);
    }

    [TestMethod]
    public void Add_RecordWithoutAnyTtl_RefusesTheZone()
    {
        var message = FaultOf("@ SOA ns hostmaster 1 2 3 4 30");

        Assert.AreEqual("line 1: no TTL, and no $TTL before it", message);
    }

    [TestMethod]
    public void Add_NoSoa_RefusesTheZoneWithoutALine()
    {
        var exception = Assert.ThrowsExactly<DnsZoneFileException>(
            () => new DnsZoneSet().Add("example.test.zone", Encoding.UTF8.GetBytes("$TTL 60\nwww A 192.0.2.1")));

        Assert.AreEqual("no SOA at the apex", exception.Message);
        Assert.IsNull(exception.LineNumber);
        Assert.AreEqual("no SOA at the apex", exception.Fault);
    }

    [TestMethod]
    public void Add_RecordDataPast65535Bytes_RefusesTheZone()
    {
        var strings = string.Join(' ', Enumerable.Repeat(new string('x', 255), 260));

        var message = FaultOf(Minimal($"big TXT {strings}"));

        Assert.AreEqual("line 3: an RDATA past 65535 bytes", message);
    }

    [TestMethod]
    public void Add_ByteOrderMark_RefusesTheZone()
    {
        byte[] content = [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(Minimal())];

        var message = FaultOf("example.test.zone", content);

        Assert.AreEqual("line 1: a byte order mark", message);
    }

    [TestMethod]
    public void Add_InvalidUtf8_RefusesTheZoneNamingTheLine()
    {
        byte[] content = [.. Encoding.UTF8.GetBytes(Minimal("www TXT ")), 0xC3, 0x28];

        var message = FaultOf("example.test.zone", content);

        Assert.AreEqual("line 3: not UTF-8", message);
    }

    [TestMethod]
    public void Add_SecondZoneForOneApex_RefusesIt()
    {
        var zones = Load(("example.test.zone", Minimal()));

        var message = FaultOf("EXAMPLE.test.zone", Encoding.UTF8.GetBytes(Minimal()), zones);

        Assert.AreEqual("a second zone for EXAMPLE.test.", message);
        Assert.HasCount(1, zones.Apexes);
    }

    [TestMethod]
    [DataRow("example.test.txt", "a file name not ending in .zone")]
    [DataRow("a..b.zone", "a file name that is not a domain name")]
    public void Add_FileNameThatIsNoApex_RefusesIt(string fileName, string fault)
    {
        var message = FaultOf(fileName, Encoding.UTF8.GetBytes(Minimal()));

        Assert.AreEqual(fault, message);
    }

    [TestMethod]
    public void Add_PastTheZoneCount_RefusesIt()
    {
        var zones = new DnsZoneSet(maxZones: 1);
        zones.Add("a.test.zone", Encoding.UTF8.GetBytes(Minimal()));

        var message = FaultOf("b.test.zone", Encoding.UTF8.GetBytes(Minimal()), zones);

        Assert.AreEqual("past the zones' bounds", message);
    }

    [TestMethod]
    public void Add_PastTheRecordCount_RefusesIt()
    {
        var zones = new DnsZoneSet(maxRecords: 2);
        zones.Add("a.test.zone", Encoding.UTF8.GetBytes(Minimal("www A 192.0.2.1")));

        var message = FaultOf("b.test.zone", Encoding.UTF8.GetBytes(Minimal()), zones);

        Assert.AreEqual("past the zones' bounds", message);
    }

    [TestMethod]
    public void Add_PastTheTotalBytes_RefusesIt()
    {
        var zones = new DnsZoneSet(maxTotalBytes: 10);

        var message = FaultOf("a.test.zone", Encoding.UTF8.GetBytes(Minimal()), zones);

        Assert.AreEqual("past the zones' bounds", message);
        Assert.IsEmpty(zones.Apexes);
    }

    [TestMethod]
    public void Constructor_NegativeBound_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DnsZoneSet(maxZones: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DnsZoneSet(maxRecords: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DnsZoneSet(maxTotalBytes: -1));
    }

    [TestMethod]
    public void Add_NullArgument_Throws()
    {
        var zones = new DnsZoneSet();

        Assert.ThrowsExactly<ArgumentNullException>(() => zones.Add(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => zones.Add("a.zone", null!));
    }
}
