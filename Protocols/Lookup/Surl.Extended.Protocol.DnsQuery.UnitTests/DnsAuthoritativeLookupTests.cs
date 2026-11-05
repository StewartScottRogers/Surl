using System.Net;
using System.Text;
using static Surl.Extended.Protocol.DnsQuery.DnsTestZones;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The authoritative answer to each outcome of ADR-0092 decision 2's table, from
/// <see cref="DnsTestZones.Example"/> (RFC 1034 section 4.3.2, RFC 2308 sections 3 and 5,
/// RFC 4343, RFC 4592, RFC 6604 section 2).
/// </summary>
[TestClass]
public sealed class DnsAuthoritativeLookupTests
{
    [TestMethod]
    public void Answer_EachServedTypeInTheZoneFile_AnswersItsRecord()
    {
        var zones = LoadExample();

        Assert.AreEqual(IPAddress.Parse("192.0.2.80"), Single<DnsAddressData>(zones, "web.example.test", DnsRecordType.A).Address);
        Assert.AreEqual(IPAddress.Parse("2001:db8::53"), Single<DnsAddressData>(zones, "ns1.example.test", DnsRecordType.AAAA).Address);
        Assert.AreEqual("ns1.example.test.", Single<DnsDomainNameData>(zones, "example.test", DnsRecordType.NS).Name.ToString());
        Assert.AreEqual("www.example.test.", Single<DnsDomainNameData>(zones, "ptr.example.test", DnsRecordType.PTR).Name.ToString());
        var startOfAuthority = Single<DnsStartOfAuthorityData>(zones, "example.test", DnsRecordType.SOA);
        Assert.AreEqual(
            "ns1.example.test. hostmaster.example.test. 2026110401 7200 900 1209600 300",
            $"{startOfAuthority.PrimaryNameServer} {startOfAuthority.ResponsibleMailbox} {startOfAuthority.Serial} {startOfAuthority.Refresh} {startOfAuthority.Retry} {startOfAuthority.Expire} {startOfAuthority.Minimum}");
        var mailExchange = Single<DnsMailExchangeData>(zones, "example.test", DnsRecordType.MX);
        Assert.AreEqual(10, mailExchange.Preference);
        Assert.AreEqual("mail.example.test.", mailExchange.Exchange.ToString());
        var text = Single<DnsTextData>(zones, "text.example.test", DnsRecordType.TXT);
        CollectionAssert.AreEqual(
            new[] { "hello world", "say \"hi\"", "plain", "A." },
            text.Strings.Select(value => Encoding.UTF8.GetString(value.Span)).ToArray());
        var service = Single<DnsServiceLocationData>(zones, "_sip._tcp.example.test", DnsRecordType.SRV);
        Assert.AreEqual((ushort)5060, service.Port);
        Assert.AreEqual("sip.example.test.", service.Target.ToString());
        var authorization = Single<DnsCertificationAuthorityAuthorizationData>(zones, "example.test", DnsRecordType.CAA);
        Assert.AreEqual("issue", authorization.Tag);
        Assert.AreEqual("ca.example", Encoding.ASCII.GetString(authorization.Value.Span));
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD, 0xEF }, Single<DnsOpaqueData>(zones, "opaque.example.test", (DnsRecordType)65280).Bytes.ToArray());
        Assert.AreEqual(IPAddress.Parse("192.0.2.7"), Single<DnsAddressData>(zones, "known.example.test", DnsRecordType.A).Address);
    }

    [TestMethod]
    public void Answer_NameAndTypeExist_AnswersTheSetWithTheApexNsAndAddressesOfTargets()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("example.test", DnsRecordType.MX));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsTrue(answer.IsAuthoritative);
        Assert.HasCount(1, answer.Answers);
        Assert.AreEqual(DnsRecordType.NS, answer.Authorities.Single().Type);
        CollectionAssert.AreEqual(
            new[] { "mail.example.test. A", "ns1.example.test. A", "ns1.example.test. AAAA" },
            answer.Additionals.Select(record => $"{record.Name} {record.Type}").ToArray());
        Assert.AreEqual(600u, answer.Additionals[0].TimeToLive);
    }

    [TestMethod]
    public void Answer_ApexNs_LeavesTheAuthorityEmptyAndAnAnsweredAddressOutOfTheAdditional()
    {
        var zones = LoadExample();

        var nameServers = zones.Answer(Question("example.test", DnsRecordType.NS));
        var address = zones.Answer(Question("ns1.example.test", DnsRecordType.A));

        Assert.IsEmpty(nameServers.Authorities);
        Assert.HasCount(2, nameServers.Additionals);
        Assert.AreEqual(DnsRecordType.AAAA, address.Additionals.Single().Type);
    }

    [TestMethod]
    public void Answer_ServiceLocation_AddsTheTargetsAddress()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("_sip._tcp.example.test", DnsRecordType.SRV));

        Assert.AreEqual("sip.example.test.", answer.Additionals[0].Name.ToString());
    }

    [TestMethod]
    public void Answer_Any_AnswersEverySetAtTheName()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("example.test", DnsRecordType.ANY));

        CollectionAssert.AreEqual(
            new[] { DnsRecordType.SOA, DnsRecordType.NS, DnsRecordType.MX, DnsRecordType.CAA },
            answer.Answers.Select(record => record.Type).ToArray());
    }

    [TestMethod]
    public void Answer_NameWithoutTheType_AnswersNoDataWithTheSoaAtItsNegativeTtl()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("web.example.test", DnsRecordType.AAAA));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsTrue(answer.IsAuthoritative);
        Assert.IsEmpty(answer.Answers);
        var startOfAuthority = answer.Authorities.Single();
        Assert.AreEqual(DnsRecordType.SOA, startOfAuthority.Type);
        Assert.AreEqual(300u, startOfAuthority.TimeToLive);
        Assert.IsEmpty(answer.Additionals);
    }

    [TestMethod]
    public void Answer_EmptyNonTerminal_AnswersNoData()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("b.c.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.AreEqual(DnsRecordType.SOA, answer.Authorities.Single().Type);
    }

    [TestMethod]
    public void Answer_MissingName_AnswersNxDomainWithTheSoa()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("missing.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NonExistentDomain, answer.ResponseCode);
        Assert.IsTrue(answer.IsAuthoritative);
        Assert.IsEmpty(answer.Answers);
        Assert.AreEqual(300u, answer.Authorities.Single().TimeToLive);
    }

    [TestMethod]
    public void Answer_InZoneCanonicalName_FollowsItToTheARecord()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("www.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsTrue(answer.IsAuthoritative);
        CollectionAssert.AreEqual(
            new[] { "www.example.test. CNAME 300", "web.example.test. A 3600" },
            answer.Answers.Select(record => $"{record.Name} {record.Type} {record.TimeToLive}").ToArray());
        Assert.AreEqual("web.example.test.", ((DnsDomainNameData)answer.Answers[0].Data).Name.ToString());
    }

    [TestMethod]
    public void Answer_CanonicalNameAskedFor_AnswersTheCanonicalNameAlone()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("www.example.test", DnsRecordType.CNAME));

        Assert.AreEqual(DnsRecordType.CNAME, answer.Answers.Single().Type);
    }

    [TestMethod]
    public void Answer_ChainToAMissingName_AnswersTheChainWithNxDomain()
    {
        var zones = Load(("example.test.zone", Minimal("www CNAME gone")));

        var answer = zones.Answer(Question("www.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NonExistentDomain, answer.ResponseCode);
        Assert.AreEqual(DnsRecordType.CNAME, answer.Answers.Single().Type);
        Assert.AreEqual(DnsRecordType.SOA, answer.Authorities.Single().Type);
    }

    [TestMethod]
    public void Answer_ChainLeavingEveryZone_AnswersTheCanonicalNamesWithNoError()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("away.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsTrue(answer.IsAuthoritative);
        Assert.AreEqual(DnsRecordType.CNAME, answer.Answers.Single().Type);
        Assert.IsEmpty(answer.Authorities);
    }

    [TestMethod]
    public void Answer_ChainThatLoops_AnswersServerFailure()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("loop1.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.ServerFailure, answer.ResponseCode);
        Assert.HasCount(2, answer.Answers);
        Assert.IsEmpty(answer.Authorities);
    }

    [TestMethod]
    public void Answer_ChainPastSixteen_AnswersServerFailureWithSixteen()
    {
        var lines = Enumerable.Range(0, 20).Select(index => $"c{index} CNAME c{index + 1}").ToArray();
        var zones = Load(("example.test.zone", Minimal(lines)));

        var answer = zones.Answer(Question("c0.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.ServerFailure, answer.ResponseCode);
        Assert.HasCount(DnsAuthoritativeLookup.MaxCanonicalNames, answer.Answers);
    }

    [TestMethod]
    public void Answer_ChainIntoAnotherZone_AnswersFromThatZone()
    {
        var zones = Load(
            ("example.test.zone", Minimal("www CNAME www.other.test.")),
            ("other.test.zone", Minimal("www A 192.0.2.1")));

        var answer = zones.Answer(Question("www.example.test", DnsRecordType.A));

        CollectionAssert.AreEqual(
            new[] { DnsRecordType.CNAME, DnsRecordType.A },
            answer.Answers.Select(record => record.Type).ToArray());
    }

    [TestMethod]
    public void Answer_NameOutsideEveryZone_AnswersRefusedWithRecursionUnavailable()
    {
        var zones = LoadExample();
        var question = Question("www.elsewhere.invalid", DnsRecordType.A);
        var query = new DnsHeader(0xBEEF, false, DnsOpcode.Query, false, false, true, false, 0, DnsResponseCode.NoError);

        var response = zones.Answer(question).ToResponse(query, question);

        Assert.AreEqual(DnsResponseCode.Refused, response.Header.ResponseCode);
        Assert.IsFalse(response.Header.IsRecursionAvailable);
        Assert.IsFalse(response.Header.IsAuthoritativeAnswer);
        Assert.IsTrue(response.Header.IsRecursionDesired);
        Assert.AreEqual((ushort)0xBEEF, response.Header.Id);
        Assert.IsEmpty(response.Answers);
        Assert.IsEmpty(response.Authorities);
    }

    [TestMethod]
    public void Answer_NoZoneAtAll_AnswersRefused()
    {
        var zones = new DnsZoneSet();

        var answer = zones.Answer(Question("example.test", DnsRecordType.A));

        Assert.AreSame(DnsAuthoritativeAnswer.Refused, answer);
    }

    [TestMethod]
    public void Answer_ClassOtherThanInternet_AnswersRefused()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("web.example.test", DnsRecordType.A, recordClass: 3));

        Assert.AreEqual(DnsResponseCode.Refused, answer.ResponseCode);
    }

    [TestMethod]
    public void Answer_ClassAny_AnswersAsInternet()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("web.example.test", DnsRecordType.A, DnsClasses.Any));

        Assert.HasCount(1, answer.Answers);
    }

    [TestMethod]
    public void Answer_MixedCaseQuestion_AnswersWithTheQuestionsOwnCase()
    {
        var zones = LoadExample();
        var question = Question("WwW.ExAmPlE.TeSt", DnsRecordType.A);
        var query = new DnsHeader(7, false, DnsOpcode.Query, false, false, false, false, 0, DnsResponseCode.NoError);

        var response = zones.Answer(question).ToResponse(query, question);

        Assert.AreEqual("WwW.ExAmPlE.TeSt.", response.Questions.Single().Name.ToString());
        Assert.AreEqual("WwW.ExAmPlE.TeSt.", response.Answers[0].Name.ToString());
        Assert.AreEqual("web.example.test.", response.Answers[1].Name.ToString());
        Assert.IsTrue(response.Header.IsAuthoritativeAnswer);
        Assert.IsTrue(response.Header.IsResponse);
    }

    [TestMethod]
    public void Answer_NameBelowAWildcardsParent_AnswersTheWildcardAsTheName()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("Any.Thing.wild.example.test", DnsRecordType.TXT));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.AreEqual("Any.Thing.wild.example.test.", answer.Answers.Single().Name.ToString());
    }

    [TestMethod]
    public void Answer_WildcardsParentItself_AnswersNoDataNotTheWildcard()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("wild.example.test", DnsRecordType.TXT));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsEmpty(answer.Answers);
    }

    [TestMethod]
    public void Answer_WildcardWithoutTheType_AnswersNoData()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("x.wild.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsEmpty(answer.Answers);
        Assert.AreEqual(DnsRecordType.SOA, answer.Authorities.Single().Type);
    }

    [TestMethod]
    public void Answer_NameBelowADelegation_AnswersAReferralWithGlue()
    {
        var zones = LoadExample();

        var answer = zones.Answer(Question("host.sub.example.test", DnsRecordType.A));

        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.IsFalse(answer.IsAuthoritative);
        Assert.IsEmpty(answer.Answers);
        Assert.AreEqual("sub.example.test. NS", answer.Authorities.Select(record => $"{record.Name} {record.Type}").Single());
        Assert.AreEqual("ns.sub.example.test. A", answer.Additionals.Select(record => $"{record.Name} {record.Type}").Single());
    }

    [TestMethod]
    public void Answer_ChainIntoADelegation_AnswersTheChainAuthoritativelyWithTheReferral()
    {
        var zones = Load(("example.test.zone", Minimal("www CNAME host.sub", "sub NS ns.elsewhere.invalid.")));

        var answer = zones.Answer(Question("www.example.test", DnsRecordType.A));

        Assert.IsTrue(answer.IsAuthoritative);
        Assert.AreEqual(DnsRecordType.CNAME, answer.Answers.Single().Type);
        Assert.AreEqual(DnsRecordType.NS, answer.Authorities.Single().Type);
        Assert.IsEmpty(answer.Additionals);
    }

    [TestMethod]
    public void Answer_OverlappingZones_AnswersFromTheLongestApex()
    {
        var zones = Load(
            ("example.test.zone", Minimal("www.sub A 192.0.2.1")),
            ("sub.example.test.zone", Minimal("www A 192.0.2.2")));

        var answer = zones.Answer(Question("www.sub.example.test", DnsRecordType.A));

        Assert.AreEqual(IPAddress.Parse("192.0.2.2"), ((DnsAddressData)answer.Answers.Single().Data).Address);
    }

    [TestMethod]
    public void Answer_ZoneWithoutApexNs_AddsNoAuthority()
    {
        var zones = Load(("example.test.zone", Minimal("www A 192.0.2.1")));

        var answer = zones.Answer(Question("www.example.test", DnsRecordType.A));

        Assert.IsEmpty(answer.Authorities);
    }

    [TestMethod]
    public void Answer_NullQuestion_Throws()
    {
        var zones = new DnsZoneSet();

        Assert.ThrowsExactly<ArgumentNullException>(() => zones.Answer(null!));
    }

    private static T Single<T>(DnsZoneSet zones, string name, DnsRecordType type)
        where T : DnsRecordData
    {
        var answer = zones.Answer(Question(name, type));
        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.AreEqual(type, answer.Answers.Single().Type);
        return (T)answer.Answers[0].Data;
    }
}
