using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// What <see cref="DnsQueryAnswerer"/> answers to each request of ADR-0092 decision 2's
/// "errors in the request" table (RFC 1035 section 4.1.1, RFC 6891 sections 6.1.1, 6.1.3 and 7,
/// RFC 9619), from query bytes written by hand.
/// </summary>
[TestClass]
public sealed class DnsQueryAnswererTests
{
    private const ushort TypeA = 1;

    private static readonly DnsQueryAnswerer Answerer = new(DnsTestZones.LoadExample());

    [TestMethod]
    public void Constructor_NullZones_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsQueryAnswerer(null!));

    [TestMethod]
    public void Answer_Query_EchoesTheIdOpcodeRdAndQuestionWithQrSetAndRaClear()
    {
        var response = Answerer.Answer(Query("Web.Example.Test", TypeA))!;

        var header = response.Message.Header;
        Assert.AreEqual((ushort)0xBEEF, header.Id);
        Assert.IsTrue(header.IsResponse);
        Assert.AreEqual(DnsOpcode.Query, header.Opcode);
        Assert.IsTrue(header.IsRecursionDesired);
        Assert.IsFalse(header.IsRecursionAvailable);
        Assert.IsTrue(header.IsAuthoritativeAnswer);
        Assert.AreEqual(DnsResponseCode.NoError, header.ResponseCode);
        Assert.AreEqual("Web.Example.Test.", response.Message.Questions.Single().Name.ToString());
        Assert.AreEqual(DnsRecordType.A, response.Message.Answers.Single().Type);
        Assert.IsNull(response.ResponseOpt);
        Assert.IsNull(response.QueryOpt);
    }

    [TestMethod]
    public void Answer_FewerThanTwelveBytes_IsDropped() =>
        Assert.IsNull(Answerer.Answer(new byte[11]));

    [TestMethod]
    public void Answer_ResponseMessage_IsDropped() =>
        Assert.IsNull(Answerer.Answer(Query("web.example.test", TypeA, flags: 0x8100)));

    [TestMethod]
    public void Answer_TwoQuestions_IsFormatErrorWithNoQuestion()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, questionCount: 2))!;

        AssertFault(response, DnsResponseCode.FormatError, expectQuestion: false);
    }

    [TestMethod]
    public void Answer_NoQuestion_IsFormatErrorWithNoQuestion()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, questionCount: 0, additionals: Opt(1232)))!;

        AssertFault(response, DnsResponseCode.FormatError, expectQuestion: false);
        Assert.IsNotNull(response.ResponseOpt);
    }

    [TestMethod]
    public void Answer_StatusOpcode_IsNotImplementedWithTheQuestionEchoed()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, flags: 0x1100))!;

        AssertFault(response, DnsResponseCode.NotImplemented, expectQuestion: true);
        Assert.AreEqual(DnsOpcode.Status, response.Message.Header.Opcode);
        Assert.IsNull(response.ResponseOpt);
    }

    [TestMethod]
    public void Answer_NotifyOpcodeWithOpt_IsNotImplementedWithAnOpt()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, flags: 0x2000, additionals: Opt(4096)))!;

        AssertFault(response, DnsResponseCode.NotImplemented, expectQuestion: true);
        Assert.AreEqual(DnsQueryAnswerer.ServerPayloadSize, response.ResponseOpt!.PayloadSize);
    }

    [TestMethod]
    public void Answer_HeaderReadableQuestionNot_IsFormatErrorWithNoQuestion()
    {
        var query = Query("web.example.test", TypeA)[..14];

        var response = Answerer.Answer(query)!;

        AssertFault(response, DnsResponseCode.FormatError, expectQuestion: false);
        Assert.AreEqual((ushort)0xBEEF, response.Message.Header.Id);
    }

    [TestMethod]
    public void Answer_QuestionReadableRestNot_IsFormatErrorWithTheQuestionEchoed()
    {
        byte[] query = [.. Query("web.example.test", TypeA), 0xFF];

        var response = Answerer.Answer(query)!;

        AssertFault(response, DnsResponseCode.FormatError, expectQuestion: true);
    }

    [TestMethod]
    public void Answer_TwoOptRecords_IsFormatErrorWithNoOpt()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, additionals: [Opt(1232), Opt(1232)]))!;

        AssertFault(response, DnsResponseCode.FormatError, expectQuestion: true);
        Assert.IsNull(response.ResponseOpt);
    }

    [TestMethod]
    public void Answer_OptNotOwnedByTheRoot_IsFormatError()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, additionals: Opt(1232, owner: "example.test")))!;

        AssertFault(response, DnsResponseCode.FormatError, expectQuestion: true);
    }

    [TestMethod]
    public void Answer_OptVersionOne_IsBadVersWithAnOptOfVersionZero()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, additionals: Opt(1232, version: 1)))!;

        AssertFault(response, DnsResponseCode.NoError, expectQuestion: true);
        Assert.AreEqual((byte)1, response.ResponseOpt!.ExtendedResponseCodeHigh);
        Assert.AreEqual((byte)0, response.ResponseOpt.Version);
    }

    [TestMethod]
    public void Answer_OptWithDnssecOk_AnswersWithTheServerPayloadSizeAndDoCopied()
    {
        var response = Answerer.Answer(Query("web.example.test", TypeA, additionals: Opt(4096, dnssecOk: true)))!;

        Assert.AreEqual(DnsResponseCode.NoError, response.Message.Header.ResponseCode);
        Assert.AreEqual(new DnsOptRecord(1232, 0, 0, true, []).ToResourceRecord().TimeToLive, response.ResponseOpt!.ToResourceRecord().TimeToLive);
        Assert.AreEqual(DnsQueryAnswerer.ServerPayloadSize, response.ResponseOpt.PayloadSize);
        Assert.AreEqual((ushort)4096, response.QueryOpt!.PayloadSize);
        Assert.AreEqual(DnsRecordType.OPT, response.ToMessage().Additionals[^1].Type);
    }

    [TestMethod]
    [DataRow((ushort)252)]
    [DataRow((ushort)251)]
    public void Answer_ZoneTransfer_IsRefusedWithTheQuestionEchoed(ushort type)
    {
        var response = Answerer.Answer(Query("example.test", type))!;

        AssertFault(response, DnsResponseCode.Refused, expectQuestion: true);
    }

    [TestMethod]
    public void Answer_AxfrInADatagramWithZoneTransferAllowed_IsNotImplemented()
    {
        var response = AllowingTransfer.Answer(Query("example.test", 252))!;

        AssertFault(response, DnsResponseCode.NotImplemented, expectQuestion: true);
    }

    [TestMethod]
    public void Answer_IxfrInADatagramWithZoneTransferAllowed_IsTheApexSoaAlone()
    {
        var message = AllowingTransfer.Answer(Query("example.test", 251))!.Message;

        Assert.AreEqual(DnsResponseCode.NoError, message.Header.ResponseCode);
        Assert.IsTrue(message.Header.IsAuthoritativeAnswer);
        Assert.AreEqual(DnsRecordType.SOA, message.Answers.Single().Type);
        Assert.IsEmpty(message.Authorities);
    }

    [TestMethod]
    public void Answer_IxfrInADatagramNotAtAServedApex_IsNotAuth()
    {
        var response = AllowingTransfer.Answer(Query("web.example.test", 251))!;

        AssertFault(response, DnsResponseCode.NotAuthoritative, expectQuestion: true);
    }

    [TestMethod]
    public void AnswerOverStream_DroppedQuery_IsNoResponse() =>
        Assert.IsEmpty(Answerer.AnswerOverStream([0xBE, 0xEF, 0x01]));

    [TestMethod]
    public void AnswerOverStream_Query_IsOneResponse() =>
        Assert.AreEqual(DnsResponseCode.NoError, Answerer.AnswerOverStream(Query("web.example.test", TypeA)).Single().Message.Header.ResponseCode);

    private static readonly DnsQueryAnswerer AllowingTransfer = new(DnsTestZones.LoadExample(), allowZoneTransfer: true);

    private static void AssertFault(DnsResponse response, DnsResponseCode responseCode, bool expectQuestion)
    {
        var header = response.Message.Header;
        Assert.AreEqual(responseCode, header.ResponseCode);
        Assert.IsTrue(header.IsResponse);
        Assert.IsFalse(header.IsAuthoritativeAnswer);
        Assert.IsFalse(header.IsRecursionAvailable);
        Assert.AreEqual(expectQuestion ? 1 : 0, response.Message.Questions.Count);
        Assert.IsEmpty(response.Message.Answers);
    }
}
