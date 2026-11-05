using System.Net;
using System.Text;
using static Surl.Extended.Protocol.DnsQuery.DnsTestMessages;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A query, and a response carrying each record type ADR-0092 decision 2 serves, written and read
/// back field by field (RFC 1035 sections 3.3 and 4.1, RFC 3596, RFC 2782, RFC 8659, RFC 3597,
/// RFC 6891).
/// </summary>
[TestClass]
public sealed class DnsMessageWriterTests
{
    [TestMethod]
    public void WriteThenRead_QueryWithOpt_KeepsHeaderQuestionAndOpt()
    {
        var opt = new DnsOptRecord(1232, 0, 0, true, [new DnsOption(DnsOption.PaddingCode, new byte[4])]);

        var read = RoundTrip(Query("Example.COM", DnsRecordType.AAAA, opt.ToResourceRecord()));

        Assert.AreEqual(QueryHeader, read.Header);
        Assert.HasCount(1, read.Questions);
        Assert.AreEqual("Example.COM.", read.Questions[0].Name.ToString());
        Assert.AreEqual(DnsRecordType.AAAA, read.Questions[0].Type);
        Assert.AreEqual(DnsClasses.Internet, read.Questions[0].Class);
        var readOpt = DnsOptRecord.FromResourceRecord(read.Additionals[0]);
        Assert.AreEqual(1232, readOpt.PayloadSize);
        Assert.IsTrue(readOpt.IsDnssecOk);
        Assert.AreEqual(DnsOption.PaddingCode, readOpt.Options[0].Code);
        Assert.AreEqual(4, readOpt.Options[0].Data.Length);
    }

    [TestMethod]
    public void WriteThenRead_HeaderWithEveryFlagSet_KeepsEveryField()
    {
        var header = new DnsHeader(0xFFFF, true, (DnsOpcode)15, true, true, true, true, 7, (DnsResponseCode)15);
        var message = new DnsMessage(header, [], [], [], []);

        var read = RoundTrip(message);

        Assert.AreEqual(header, read.Header);
        CollectionAssert.AreEqual(
            Bytes(0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0),
            DnsMessageWriter.Write(message));
    }

    [TestMethod]
    public void WriteThenRead_AResponse_KeepsTheAddressInFourBytes()
    {
        var message = Response("a.test", DnsRecordType.A, Record("a.test", new DnsAddressData(IPAddress.Parse("192.0.2.1"))));

        var data = RoundTripAnswerData<DnsAddressData>(message);

        Assert.AreEqual(IPAddress.Parse("192.0.2.1"), data.Address);
        Assert.AreEqual(DnsRecordType.A, data.Type);
        var bytes = DnsMessageWriter.Write(message);
        CollectionAssert.AreEqual(Bytes(0, 4, 192, 0, 2, 1), bytes[^6..]);
    }

    [TestMethod]
    public void WriteThenRead_AaaaResponse_KeepsTheAddressInSixteenBytes()
    {
        var message = Response("a.test", DnsRecordType.AAAA, Record("a.test", new DnsAddressData(IPAddress.Parse("2001:db8::1"))));

        var data = RoundTripAnswerData<DnsAddressData>(message);

        Assert.AreEqual(IPAddress.Parse("2001:db8::1"), data.Address);
        Assert.AreEqual(DnsRecordType.AAAA, data.Type);
        Assert.AreEqual(16, DnsMessageWriter.Write(message)[^17]);
    }

    [TestMethod]
    [DataRow(DnsRecordType.NS)]
    [DataRow(DnsRecordType.CNAME)]
    [DataRow(DnsRecordType.PTR)]
    public void WriteThenRead_NameResponse_KeepsTheNameCompressed(DnsRecordType type)
    {
        var message = Response("a.test", type, Record("a.test", new DnsDomainNameData(type, DnsName.Parse("b.a.test"))));

        var data = RoundTripAnswerData<DnsDomainNameData>(message);

        Assert.AreEqual(type, data.Type);
        Assert.AreEqual("b.a.test.", data.Name.ToString());
        CollectionAssert.AreEqual(Bytes(0, 4, 1, (byte)'b', 0xC0, 12), DnsMessageWriter.Write(message)[^6..]);
    }

    [TestMethod]
    public void WriteThenRead_SoaResponse_KeepsEveryField()
    {
        var soa = new DnsStartOfAuthorityData(
            DnsName.Parse("ns.a.test"), DnsName.Parse("admin.a.test"), 2026110401, 7200, 900, 1209600, 300);

        var data = RoundTripAnswerData<DnsStartOfAuthorityData>(Response("a.test", DnsRecordType.SOA, Record("a.test", soa)));

        Assert.AreEqual("ns.a.test.", data.PrimaryNameServer.ToString());
        Assert.AreEqual("admin.a.test.", data.ResponsibleMailbox.ToString());
        Assert.AreEqual(2026110401u, data.Serial);
        Assert.AreEqual(7200u, data.Refresh);
        Assert.AreEqual(900u, data.Retry);
        Assert.AreEqual(1209600u, data.Expire);
        Assert.AreEqual(300u, data.Minimum);
        Assert.AreEqual(DnsRecordType.SOA, data.Type);
    }

    [TestMethod]
    public void WriteThenRead_MxResponse_KeepsPreferenceAndExchange()
    {
        var message = Response("a.test", DnsRecordType.MX, Record("a.test", new DnsMailExchangeData(10, DnsName.Parse("mail.a.test"))));

        var data = RoundTripAnswerData<DnsMailExchangeData>(message);

        Assert.AreEqual(10, data.Preference);
        Assert.AreEqual("mail.a.test.", data.Exchange.ToString());
        Assert.AreEqual(DnsRecordType.MX, data.Type);
        CollectionAssert.AreEqual(
            Bytes(0, 9, 0, 10, 4, (byte)'m', (byte)'a', (byte)'i', (byte)'l', 0xC0, 12),
            DnsMessageWriter.Write(message)[^11..]);
    }

    [TestMethod]
    public void WriteThenRead_TxtResponse_KeepsEveryStringInOrder()
    {
        var strings = new[] { Encoding.ASCII.GetBytes("v=spf1 -all"), [], new byte[255] };
        var message = Response("a.test", DnsRecordType.TXT, Record("a.test", new DnsTextData(strings)));

        var data = RoundTripAnswerData<DnsTextData>(message);

        Assert.HasCount(3, data.Strings);
        Assert.AreEqual("v=spf1 -all", Encoding.ASCII.GetString(data.Strings[0].Span));
        Assert.AreEqual(0, data.Strings[1].Length);
        Assert.AreEqual(255, data.Strings[2].Length);
        Assert.AreEqual(DnsRecordType.TXT, data.Type);
    }

    [TestMethod]
    public void WriteThenRead_SrvResponse_KeepsEveryFieldAndWritesTheTargetInFull()
    {
        var message = Response(
            "_sip._tcp.a.test",
            DnsRecordType.SRV,
            Record("_sip._tcp.a.test", new DnsServiceLocationData(10, 60, 5060, DnsName.Parse("a.test"))));

        var data = RoundTripAnswerData<DnsServiceLocationData>(message);

        Assert.AreEqual(10, data.Priority);
        Assert.AreEqual(60, data.Weight);
        Assert.AreEqual(5060, data.Port);
        Assert.AreEqual("a.test.", data.Target.ToString());
        Assert.AreEqual(DnsRecordType.SRV, data.Type);
        CollectionAssert.AreEqual(
            Bytes(0, 14, 0, 10, 0, 60, 0x13, 0xC4, 1, (byte)'a', 4, (byte)'t', (byte)'e', (byte)'s', (byte)'t', 0),
            DnsMessageWriter.Write(message)[^16..]);
    }

    [TestMethod]
    public void WriteThenRead_CaaResponse_KeepsFlagsTagAndValueInRfc8659Form()
    {
        var caa = new DnsCertificationAuthorityAuthorizationData(0, "issue", Encoding.ASCII.GetBytes("ca.example.net"));
        var message = Response("a.test", DnsRecordType.CAA, Record("a.test", caa));

        var data = RoundTripAnswerData<DnsCertificationAuthorityAuthorizationData>(message);

        Assert.AreEqual(0, data.Flags);
        Assert.AreEqual("issue", data.Tag);
        Assert.AreEqual("ca.example.net", Encoding.ASCII.GetString(data.Value.Span));
        Assert.AreEqual(DnsRecordType.CAA, data.Type);
        var expected = Bytes(0, 21, 0, 5).Concat(Encoding.ASCII.GetBytes("issueca.example.net")).ToArray();
        CollectionAssert.AreEqual(expected, DnsMessageWriter.Write(message)[^expected.Length..]);
    }

    [TestMethod]
    public void WriteThenRead_UnknownTypeResponse_KeepsTheRdataOpaque()
    {
        var type = (DnsRecordType)65280;
        var message = Response("a.test", type, Record("a.test", new DnsOpaqueData(type, Bytes(0xC0, 12, 1, 2))));

        var data = RoundTripAnswerData<DnsOpaqueData>(message);

        Assert.AreEqual(type, data.Type);
        CollectionAssert.AreEqual(Bytes(0xC0, 12, 1, 2), data.Bytes.ToArray());
    }

    [TestMethod]
    public void WriteThenRead_ResponseWithEverySection_KeepsEachSectionApart()
    {
        var soa = new DnsStartOfAuthorityData(DnsName.Parse("ns.a.test"), DnsName.Parse("admin.a.test"), 1, 2, 3, 4, 5);
        var message = new DnsMessage(
            ResponseHeader,
            [new DnsQuestion(DnsName.Parse("a.test"), DnsRecordType.ANY, DnsClasses.Any)],
            [Record("a.test", new DnsAddressData(IPAddress.Loopback))],
            [Record("a.test", soa)],
            [new DnsOptRecord(1232, 1, 0, false, []).ToResourceRecord()]);

        var read = RoundTrip(message);

        Assert.AreEqual(DnsClasses.Any, read.Questions[0].Class);
        Assert.AreEqual(DnsRecordType.A, read.Answers[0].Type);
        Assert.AreEqual(DnsRecordType.SOA, read.Authorities[0].Type);
        Assert.AreEqual(DnsRecordType.OPT, read.Additionals[0].Type);
        Assert.AreEqual(3600u, read.Answers[0].TimeToLive);
    }

    /// <summary>
    /// RFC 1035 section 4.1.4's example: <c>F.ISI.ARPA</c> in full, <c>FOO.F.ISI.ARPA</c> as the
    /// label FOO and a pointer to F.ISI.ARPA, and <c>ARPA</c> as a pointer alone to the ARPA label
    /// inside F.ISI.ARPA. The RFC places the names at offsets 20, 40 and 64 for illustration; no
    /// well-formed message has gaps of 8 and 18 bytes between three names, so here F.ISI.ARPA is
    /// the question at offset 12 and the pointers name 12 and 18.
    /// </summary>
    [TestMethod]
    public void Write_Rfc1035CompressionExample_WritesTheRfcsPointers()
    {
        var address = new DnsAddressData(IPAddress.Parse("10.0.0.1"));
        var message = Response("F.ISI.ARPA", DnsRecordType.A, Record("FOO.F.ISI.ARPA", address, 0), Record("ARPA", address, 0));
        var expected = HeaderBytes(1, answers: 2)
            .Concat(Bytes(1, 'F', 3, 'I', 'S', 'I', 4, 'A', 'R', 'P', 'A', 0, 0, 1, 0, 1))
            .Concat(Bytes(3, 'F', 'O', 'O', 0xC0, 12, 0, 1, 0, 1, 0, 0, 0, 0, 0, 4, 10, 0, 0, 1))
            .Concat(Bytes(0xC0, 18, 0, 1, 0, 1, 0, 0, 0, 0, 0, 4, 10, 0, 0, 1))
            .ToArray();
        expected[2] = 0x85;

        var bytes = DnsMessageWriter.Write(message);

        CollectionAssert.AreEqual(expected, bytes);
    }

    [TestMethod]
    public void Write_SuffixFirstWrittenPastOffset16383_IsNotPointedTo()
    {
        var message = Response(
            ".",
            DnsRecordType.A,
            Record(".", new DnsOpaqueData((DnsRecordType)65280, new byte[20000])),
            Record("late.test", new DnsAddressData(IPAddress.Loopback)),
            Record("late.test", new DnsAddressData(IPAddress.Loopback)));

        var bytes = DnsMessageWriter.Write(message);

        Assert.AreEqual(12 + 5 + 11 + 20000 + (2 * (11 + 10 + 4)), bytes.Length);
        Assert.AreEqual(DnsReadFault.None, DnsMessageReader.Read(bytes).Fault);
    }

    [TestMethod]
    public void Write_SuffixWrittenUncompressedInSrv_IsPointedToByALaterName()
    {
        var message = Response(
            ".",
            DnsRecordType.SRV,
            Record(".", new DnsServiceLocationData(0, 0, 53, DnsName.Parse("ns.test"))),
            Record("ns.test", new DnsAddressData(IPAddress.Loopback)));

        var bytes = DnsMessageWriter.Write(message);

        CollectionAssert.AreEqual(Bytes(0xC0, 12 + 5 + 11 + 6), bytes[(12 + 5 + 11 + 6 + 9)..][..2]);
    }

    [TestMethod]
    [DataRow(16, 0, 0)]
    [DataRow(0, 8, 0)]
    [DataRow(0, 0, 16)]
    public void Write_HeaderFieldPastItsBits_Throws(int opcode, int z, int responseCode)
    {
        var header = new DnsHeader(1, false, (DnsOpcode)opcode, false, false, false, false, (byte)z, (DnsResponseCode)responseCode);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DnsMessageWriter.Write(new DnsMessage(header, [], [], [], [])));
    }

    [TestMethod]
    public void Write_SectionOf65536Entries_Throws()
    {
        var question = new DnsQuestion(DnsName.Root, DnsRecordType.A, DnsClasses.Internet);
        var message = new DnsMessage(QueryHeader, Enumerable.Repeat(question, 65536).ToArray(), [], [], []);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DnsMessageWriter.Write(message));
    }

    [TestMethod]
    public void Write_RdataOf65536Bytes_Throws()
    {
        var message = Response(".", DnsRecordType.A, Record(".", new DnsOpaqueData((DnsRecordType)65280, new byte[65536])));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DnsMessageWriter.Write(message));
    }
}
