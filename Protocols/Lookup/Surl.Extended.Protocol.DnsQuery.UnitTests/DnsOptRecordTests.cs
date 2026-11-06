using System.Net;
using static Surl.Extended.Protocol.DnsQuery.DnsTestMessages;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>The OPT pseudo-record's fields (RFC 6891 section 6.1, RFC 3225 section 3).</summary>
[TestClass]
public sealed class DnsOptRecordTests
{
    /// <summary>
    /// A query's additional section holding RFC 6891 section 6.1.2's OPT written by hand: owner the
    /// root, TYPE 41, CLASS the payload size 4096, TTL extended RCODE 0, version 0, DO set, and one
    /// option, code 10 (a client cookie) with 8 bytes.
    /// </summary>
    [TestMethod]
    public void FromResourceRecord_OptReadFromTheWire_HasItsPayloadSize()
    {
        var message = HeaderBytes(1, additionals: 1)
            .Concat(Bytes(1, 'a', 0, 0, 1, 0, 1))
            .Concat(Bytes(0, 0, 41, 0x10, 0x00, 0, 0, 0x80, 0x00, 0, 12, 0, 10, 0, 8, 1, 2, 3, 4, 5, 6, 7, 8))
            .ToArray();

        var outcome = DnsMessageReader.Read(message);
        var opt = DnsOptRecord.FromResourceRecord(outcome.Message!.Additionals[0]);

        Assert.AreEqual(4096, opt.PayloadSize);
        Assert.AreEqual(0, opt.ExtendedResponseCodeHigh);
        Assert.AreEqual(0, opt.Version);
        Assert.IsTrue(opt.IsDnssecOk);
        Assert.HasCount(1, opt.Options);
        Assert.AreEqual(10, opt.Options[0].Code);
        Assert.AreEqual(8, opt.Options[0].Data.Length);
        Assert.AreEqual(".", outcome.Message.Additionals[0].Name.ToString());
    }

    [TestMethod]
    public void ToResourceRecord_BadVersionAnswer_PutsTheExtendedRcodeAndVersionInTheTtl()
    {
        // BADVERS is extended RCODE 16: 1 in the OPT's upper eight bits, 0 in the header.
        var record = new DnsOptRecord(1232, 1, 0, false, []).ToResourceRecord();

        Assert.AreEqual(DnsRecordType.OPT, record.Type);
        Assert.AreEqual(1232, record.Class);
        Assert.AreEqual(0x0100_0000u, record.TimeToLive);
        Assert.AreSame(DnsName.Root, record.Name);
    }

    [TestMethod]
    public void FromResourceRecord_VersionAndOtherFlagBits_ReadsTheVersionAndIgnoresTheFlags()
    {
        var record = new DnsResourceRecord(DnsName.Root, 512, 0x0201_7FFF, new DnsOptData([]));

        var opt = DnsOptRecord.FromResourceRecord(record);

        Assert.AreEqual(2, opt.ExtendedResponseCodeHigh);
        Assert.AreEqual(1, opt.Version);
        Assert.IsFalse(opt.IsDnssecOk);
    }

    [TestMethod]
    public void FromResourceRecord_RecordThatIsNotOpt_Throws()
    {
        var record = Record("a.test", new DnsAddressData(IPAddress.Loopback));

        Assert.ThrowsExactly<ArgumentException>(() => DnsOptRecord.FromResourceRecord(record));
    }
}
