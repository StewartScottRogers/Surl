using System.Net;
using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A case's <c>&lt;dns&gt;</c> section as zones and a query log (ADR-0123 decision 1), judged by
/// RFC 1035, RFC 3596 and RFC 4592's wildcard rules.
/// </summary>
[TestClass]
public sealed class DnsQueryUpstreamTestCaseRepliesTests
{
    [TestMethod]
    public void Constructor_NullLines_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsQueryUpstreamTestCaseReplies(null!));

    [TestMethod]
    public void SupportedLineNames_AreAAndAaaa() =>
        CollectionAssert.AreEquivalent(new[] { "A", "AAAA" }, DnsQueryUpstreamTestCaseReplies.SupportedLineNames.ToArray());

    [TestMethod]
    public void Zones_AAndAaaaLines_AnswerEveryNameWithBoth()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies(["A: 127.0.0.1", "", "AAAA: [::1]"]);

        var a = replies.Zones.Answer(Question("examplehost.example", DnsRecordType.A));
        var aaaa = replies.Zones.Answer(Question("examplehost.invalid", DnsRecordType.AAAA));

        Assert.AreEqual(IPAddress.Loopback, ((DnsAddressData)a.Answers.Single().Data).Address);
        Assert.AreEqual(2580u, a.Answers.Single().TimeToLive);
        Assert.AreEqual(IPAddress.IPv6Loopback, ((DnsAddressData)aaaa.Answers.Single().Data).Address);
    }

    [TestMethod]
    public void Zones_LaterLineOfANameReplacesTheEarlier()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies(["A: 192.0.2.1", "A: 192.0.2.2"]);

        var answer = replies.Zones.Answer(Question("a.test", DnsRecordType.A));

        Assert.AreEqual(IPAddress.Parse("192.0.2.2"), ((DnsAddressData)answer.Answers.Single().Data).Address);
    }

    [TestMethod]
    public void Zones_NoneAddressOrAMissingLine_AnswerNoErrorWithNoRecords()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies(["A: none", "AAAA"]);

        var a = replies.Zones.Answer(Question("examplehost.example", DnsRecordType.A));
        var aaaa = replies.Zones.Answer(Question("examplehost.example", DnsRecordType.AAAA));

        Assert.AreEqual(DnsResponseCode.NoError, a.ResponseCode);
        Assert.IsEmpty(a.Answers);
        Assert.AreEqual(DnsResponseCode.NoError, aaaa.ResponseCode);
        Assert.IsEmpty(aaaa.Answers);
    }

    [TestMethod]
    public void Zones_EmptySection_AnswersNoErrorForAnyName()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies([]);

        var answer = replies.Zones.Answer(Question("examplehost.invalid", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsEmpty(answer.Answers);
    }

    [TestMethod]
    public void Zones_AddressOfTheOtherFamily_GivesNoRecord()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies(["A: ::1", "AAAA: 127.0.0.1"]);

        Assert.IsEmpty(replies.Zones.Answer(Question("a.test", DnsRecordType.A)).Answers);
        Assert.IsEmpty(replies.Zones.Answer(Question("a.test", DnsRecordType.AAAA)).Answers);
    }

    [TestMethod]
    [DataRow("HTTPS: 1 .", "HTTPS")]
    [DataRow("Delay-A: 100", "Delay-A")]
    [DataRow("Other", "Other")]
    public void Constructor_UnsupportedLine_ThrowsNotSupportedNamingIt(string line, string name)
    {
        var fault = Assert.ThrowsExactly<NotSupportedException>(() => new DnsQueryUpstreamTestCaseReplies([line]));

        Assert.AreEqual(name, fault.Message);
    }

    [TestMethod]
    public void QueryLog_EachQuery_IsOneLineWithoutTheTrailingDotInArrivalOrder()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies([]);

        replies.RecordQuery(Question("examplehost.invalid", DnsRecordType.A));
        replies.RecordQuery(Question("examplehost.invalid", DnsRecordType.AAAA));
        replies.RecordQuery(Question("examplehost.invalid", (DnsRecordType)65));
        replies.RecordQuery(Question("examplehost.invalid", DnsRecordType.TXT));

        Assert.AreEqual(
            "QNAME examplehost.invalid QTYPE A\nQNAME examplehost.invalid QTYPE AAAA\nQNAME examplehost.invalid QTYPE HTTPS\nQNAME examplehost.invalid QTYPE <unknown>\n",
            Encoding.Latin1.GetString(replies.QueryLog));
    }

    private static DnsQuestion Question(string name, DnsRecordType type) => new(DnsName.Parse(name), type, DnsClasses.Internet);
}
