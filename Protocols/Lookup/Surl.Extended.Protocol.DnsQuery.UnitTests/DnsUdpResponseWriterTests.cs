using System.Net;
using static Surl.Extended.Protocol.DnsQuery.DnsTestMessages;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// How <see cref="DnsUdpResponseWriter"/> bounds and cuts a UDP answer (ADR-0092 decision 3;
/// RFC 1035 section 4.2.1, RFC 6891 section 6.2.5, RFC 2181 section 9).
/// </summary>
[TestClass]
public sealed class DnsUdpResponseWriterTests
{
    [TestMethod]
    [DataRow(null, 512)]
    [DataRow(100, 512)]
    [DataRow(800, 800)]
    [DataRow(4096, 1232)]
    public void LimitFor_PayloadSize_IsClampedFrom512To1232(int? payloadSize, int expected)
    {
        var opt = payloadSize is null ? null : new DnsOptRecord((ushort)payloadSize, 0, 0, false, []);

        Assert.AreEqual(expected, DnsUdpResponseWriter.LimitFor(opt));
    }

    [TestMethod]
    public void Write_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsUdpResponseWriter.Write(null!));

    [TestMethod]
    public void Write_AdditionalsPastTheLimit_DropsThemWithoutTc()
    {
        var response = Respond([Address("a", 1)], authorities: [Address("ns", 2)], additionals: Addresses("x", 40));

        var message = DnsTestQueries.ReadWhole(DnsUdpResponseWriter.Write(response));

        Assert.IsFalse(message.Header.IsTruncated);
        Assert.HasCount(1, message.Answers);
        Assert.HasCount(1, message.Authorities);
        Assert.IsEmpty(message.Additionals);
    }

    [TestMethod]
    public void Write_ReferralAuthorityPastTheLimit_DropsItWithoutTc()
    {
        var response = Respond([Address("a", 1)], authorities: Addresses("ns", 40), additionals: []);

        var message = DnsTestQueries.ReadWhole(DnsUdpResponseWriter.Write(response));

        Assert.IsFalse(message.Header.IsTruncated);
        Assert.HasCount(1, message.Answers);
        Assert.IsEmpty(message.Authorities);
    }

    [TestMethod]
    public void Write_NegativeAnswerAuthorityPastTheLimit_DropsItWithTc()
    {
        var startOfAuthority = Record(
            "example.test",
            new DnsStartOfAuthorityData(DnsName.Parse("ns.example.test"), DnsName.Parse("host.example.test"), 1, 2, 3, 4, 5));
        var response = Respond([], authorities: [startOfAuthority, .. Addresses("ns", 40)], additionals: []);

        var message = DnsTestQueries.ReadWhole(DnsUdpResponseWriter.Write(response));

        Assert.IsTrue(message.Header.IsTruncated);
        Assert.IsEmpty(message.Authorities);
    }

    [TestMethod]
    public void Write_AnswersPastTheLimit_KeepsWholeRecordSetsAndTheOpt()
    {
        var opt = new DnsOptRecord(DnsQueryAnswerer.ServerPayloadSize, 0, 0, false, []);
        var answers = Addresses("first", 3).Concat(Addresses("second", 60)).ToArray();
        var response = Respond(answers, [], []) with { ResponseOpt = opt, QueryOpt = opt with { PayloadSize = 600 } };

        var datagram = DnsUdpResponseWriter.Write(response);
        var message = DnsTestQueries.ReadWhole(datagram);

        Assert.IsLessThanOrEqualTo(600, datagram.Length);
        Assert.IsTrue(message.Header.IsTruncated);
        CollectionAssert.AreEqual(
            new[] { "first.example.test.", "first.example.test.", "first.example.test." },
            message.Answers.Select(record => record.Name.ToString()).ToArray());
        Assert.AreEqual(DnsRecordType.OPT, message.Additionals.Single().Type);
    }

    private static DnsResponse Respond(DnsResourceRecord[] answers, DnsResourceRecord[] authorities, DnsResourceRecord[] additionals) =>
        new(new DnsMessage(ResponseHeader, [new DnsQuestion(DnsName.Parse("q.example.test"), DnsRecordType.A, 1)], answers, authorities, additionals), null, null);

    // count A records, each owned by its own name unless the owner is "first" or "second", whose
    // records make one RRset each.
    private static DnsResourceRecord[] Addresses(string owner, int count) => Enumerable.Range(0, count)
        .Select(index => owner is "first" or "second"
            ? Address(owner, index)
            : Address($"{owner}{index}", index))
        .ToArray();

    private static DnsResourceRecord Address(string owner, int index) =>
        Record($"{owner}.example.test", new DnsAddressData(new IPAddress([192, 0, 2, (byte)index])));
}
