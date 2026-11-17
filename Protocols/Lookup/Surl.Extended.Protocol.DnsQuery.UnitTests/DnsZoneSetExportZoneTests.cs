namespace Surl.Extended.Protocol.DnsQuery;

// BL-1220: a test reads a seeded zone back as master-file text (ADR-0164 decision 9).
[TestClass]
public sealed class DnsZoneSetExportZoneTests
{
    private const string Seeded = """
        $TTL 60
        @ SOA ns hostmaster 1 2 3 4 30
        @ NS ns
        ns A 192.0.2.1
        ns AAAA 2001:db8::1
        www CNAME ns
        @ MX 10 mail
        mail A 192.0.2.2
        _sip._tcp SRV 1 2 5060 ns
        @ TXT "v=spf1" "a;b \"q\" \\ x"
        @ CAA 0 issue "ca.example"
        4.2.0.192.in-addr PTR ns
        weird TYPE65280 \# 2 ABCD
        empty TYPE65281 \# 0
        a\.b\ c A 192.0.2.3
        """;

    [TestMethod]
    public void ExportZone_SeededZone_WritesOneAbsoluteRecordPerLine()
    {
        var zones = new DnsZoneSet();
        zones.SeedZone("example.test.", Seeded);

        var text = zones.ExportZone("EXAMPLE.test");

        Assert.IsNotNull(text);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(14, lines.Length);
        Assert.AreEqual("example.test. 60 IN SOA ns.example.test. hostmaster.example.test. 1 2 3 4 30", lines[0]);
        Assert.Contains("ns.example.test. 60 IN AAAA 2001:db8::1", lines);
        Assert.Contains("example.test. 60 IN MX 10 mail.example.test.", lines);
        Assert.Contains("_sip._tcp.example.test. 60 IN SRV 1 2 5060 ns.example.test.", lines);
        Assert.Contains("example.test. 60 IN TXT \"v=spf1\" \"a\\;b\\032\\\"q\\\"\\032\\\\\\032x\"", lines);
        Assert.Contains("example.test. 60 IN CAA 0 issue \"ca.example\"", lines);
        Assert.Contains("weird.example.test. 60 IN TYPE65280 \\# 2 ABCD", lines);
        Assert.Contains("empty.example.test. 60 IN TYPE65281 \\# 0", lines);
        Assert.Contains("a\\.b\\032c.example.test. 60 IN A 192.0.2.3", lines);
    }

    [TestMethod]
    public void ExportZone_WhatItWrote_SeedsAnotherSetToTheSameText()
    {
        var zones = new DnsZoneSet();
        zones.SeedZone("example.test", Seeded);
        var first = zones.ExportZone("example.test");

        var again = new DnsZoneSet();
        again.SeedZone("example.test", first!);

        Assert.AreEqual(first, again.ExportZone("example.test"));
    }

    [TestMethod]
    public void ExportZone_RootApexAndSpecialBytes_AreEscaped()
    {
        var zones = new DnsZoneSet();
        zones.SeedZone(".", "$TTL 5\n@ SOA a b 1 2 3 4 5\n\\@x\\(y\\)\\$ TXT \"\\200\"\n");

        var text = zones.ExportZone(".");

        Assert.AreEqual(". 5 IN SOA a. b. 1 2 3 4 5\n\\@x\\(y\\)\\$. 5 IN TXT \"\\200\"\n", text);
    }

    [TestMethod]
    public void ExportZone_NoZoneWithTheApexOrNotADomainName_IsNull()
    {
        var zones = new DnsZoneSet();
        zones.SeedZone("example.test", Seeded);

        Assert.IsNull(zones.ExportZone("other.test"));
        Assert.IsNull(zones.ExportZone("caf\u00e9.test"));
    }

    [TestMethod]
    public void ExportZone_NullApex_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsZoneSet().ExportZone(null!));

    [TestMethod]
    public void ExportZone_DotInALabelIsEscapedAndDotInACharacterStringIsNot()
    {
        var zones = new DnsZoneSet();
        zones.SeedZone("example.test", "$TTL 5\n@ SOA a b 1 2 3 4 5\na\\.b TXT \"x.y\"\nempty TXT \"\"");

        var lines = zones.ExportZone("example.test")!.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("a\\.b.example.test. 5 IN TXT \"x.y\"", lines);
    }

    [TestMethod]
    public void ExportZone_EveryPrintableByteInALabelAndACharacterString_IsEscapedOnlyWhenSpecial()
    {
        const string Special = "\\\";()@$";
        for (var value = 0x21; value < 0x7F; value++)
        {
            var escaped = $"\\{value:D3}";
            var zones = new DnsZoneSet();
            zones.SeedZone("example.test", $"$TTL 5\n@ SOA a b 1 2 3 4 5\nl{escaped} TXT \"{escaped}\"");

            var line = zones.ExportZone("example.test")!.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];

            var character = (char)value;
            var inText = Special.Contains(character) ? $"\\{character}" : character.ToString();
            var inLabel = Special.Contains(character) || character == '.' ? $"\\{character}" : character.ToString();
            Assert.AreEqual($"l{inLabel}.example.test. 5 IN TXT \"{inText}\"", line);
        }
    }

    [TestMethod]
    public void ZoneFor_NameUnderASeededZone_IsThatZoneAndNameUnderNoneIsNull()
    {
        var zones = new DnsZoneSet();
        zones.SeedZone("example.test", Seeded);

        Assert.AreEqual("example.test.", zones.ZoneFor(DnsName.Parse("www.example.test"))?.Apex.ToString());
        Assert.IsNull(zones.ZoneFor(DnsName.Parse("www.other.test")));
    }
}
