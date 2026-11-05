namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>The response header an authoritative answer is sent with (RFC 1035 section 4.1.1; ADR-0092 decision 2).</summary>
[TestClass]
public sealed class DnsAuthoritativeAnswerTests
{
    [TestMethod]
    public void ToResponse_QueryWithFlagsSet_CopiesIdOpcodeAndRdAndClearsTheRest()
    {
        var query = new DnsHeader(0x0102, false, DnsOpcode.Query, true, true, false, true, 7, DnsResponseCode.NoError);
        var question = new DnsQuestion(DnsName.Parse("a.test"), DnsRecordType.A, DnsClasses.Internet);
        var answer = new DnsAuthoritativeAnswer(DnsResponseCode.NonExistentDomain, true, [], [], []);

        var response = answer.ToResponse(query, question);

        Assert.AreEqual(
            new DnsHeader(0x0102, true, DnsOpcode.Query, true, false, false, false, 0, DnsResponseCode.NonExistentDomain),
            response.Header);
        Assert.AreSame(question, response.Questions.Single());
    }

    [TestMethod]
    public void ToResponse_NullArgument_Throws()
    {
        var header = new DnsHeader(1, false, DnsOpcode.Query, false, false, false, false, 0, DnsResponseCode.NoError);
        var question = new DnsQuestion(DnsName.Root, DnsRecordType.A, DnsClasses.Internet);

        Assert.ThrowsExactly<ArgumentNullException>(() => DnsAuthoritativeAnswer.Refused.ToResponse(null!, question));
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsAuthoritativeAnswer.Refused.ToResponse(header, null!));
    }
}
