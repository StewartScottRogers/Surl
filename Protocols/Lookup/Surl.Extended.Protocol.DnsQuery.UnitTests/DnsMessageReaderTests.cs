using static Surl.Extended.Protocol.DnsQuery.DnsTestMessages;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The reader's refusals (RFC 1035 sections 2.3.4 and 4.1.4, ADR-0092 decision 2's "cannot be
/// read"), each from bytes written by hand from the RFC text.
/// </summary>
[TestClass]
public sealed class DnsMessageReaderTests
{
    private static readonly byte[] QuestionTypeAndClass = Bytes(0, 1, 0, 1);

    /// <summary>
    /// RFC 1035 section 4.1.4's three names, read: F.ISI.ARPA in full at 12, FOO and a pointer to
    /// 12, and a pointer to the ARPA label at 18.
    /// </summary>
    [TestMethod]
    public void Read_Rfc1035CompressionExample_ReadsTheThreeNames()
    {
        var message = HeaderBytes(3)
            .Concat(Bytes(1, 'F', 3, 'I', 'S', 'I', 4, 'A', 'R', 'P', 'A', 0)).Concat(QuestionTypeAndClass)
            .Concat(Bytes(3, 'F', 'O', 'O', 0xC0, 12)).Concat(QuestionTypeAndClass)
            .Concat(Bytes(0xC0, 18)).Concat(QuestionTypeAndClass)
            .ToArray();

        var outcome = DnsMessageReader.Read(message);

        Assert.AreEqual(DnsReadFault.None, outcome.Fault);
        CollectionAssert.AreEqual(
            new[] { "F.ISI.ARPA.", "FOO.F.ISI.ARPA.", "ARPA." },
            outcome.Questions.Select(question => question.Name.ToString()).ToArray());
    }

    [TestMethod]
    public void Read_ElevenBytes_IsNoHeader()
    {
        var outcome = DnsMessageReader.Read(new byte[11]);

        Assert.AreEqual(DnsReadFault.NoHeader, outcome.Fault);
        Assert.IsNull(outcome.Header);
        Assert.IsEmpty(outcome.Questions);
        Assert.IsNull(outcome.Message);
    }

    [TestMethod]
    public void Read_PointerToItself_IsRefusedAsNotBackwards()
    {
        var message = HeaderBytes(1).Concat(Bytes(0xC0, 12)).Concat(QuestionTypeAndClass).ToArray();

        Assert.AreEqual(DnsReadFault.CompressionPointerNotBackwards, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_PointerLoopThroughALabel_IsRefusedAsNotBackwards()
    {
        var message = HeaderBytes(1).Concat(Bytes(1, 'a', 0xC0, 12)).Concat(QuestionTypeAndClass).ToArray();

        Assert.AreEqual(DnsReadFault.CompressionPointerNotBackwards, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_PointerLoopBetweenTwoNames_IsRefusedAsNotBackwards()
    {
        // The first name points forward to the second, which points back to the first: the
        // forward pointer is refused before the loop can be walked.
        var message = HeaderBytes(2)
            .Concat(Bytes(1, 'a', 0xC0, 20)).Concat(QuestionTypeAndClass)
            .Concat(Bytes(0xC0, 12)).Concat(QuestionTypeAndClass)
            .ToArray();

        Assert.AreEqual(DnsReadFault.CompressionPointerNotBackwards, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_PointerPastTheMessageEnd_IsRefused()
    {
        var message = HeaderBytes(1).Concat(Bytes(0xC0, 0xFF)).Concat(QuestionTypeAndClass).ToArray();

        Assert.AreEqual(DnsReadFault.CompressionPointerPastEndOfMessage, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_PointerCutOffAfterItsFirstByte_IsPastEndOfMessage()
    {
        var message = HeaderBytes(1).Concat(Bytes(0xC0)).ToArray();

        Assert.AreEqual(DnsReadFault.PastEndOfMessage, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_PointerToALabelRunningPastTheEnd_IsPastEndOfMessage()
    {
        // The pointer names offset 0, the ID's first byte 0x3F: a 63-byte label the message cannot hold.
        var message = HeaderBytes(1).Concat(Bytes(0xC0, 0)).Concat(QuestionTypeAndClass).ToArray();
        message[0] = 0x3F;

        Assert.AreEqual(DnsReadFault.PastEndOfMessage, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    [DataRow(0x40)]
    [DataRow(0x80)]
    public void Read_LabelLengthOver63_IsAReservedLabelType(int lengthByte)
    {
        var message = HeaderBytes(1).Concat(Bytes(lengthByte)).Concat(new byte[lengthByte]).Concat(QuestionTypeAndClass).ToArray();

        Assert.AreEqual(DnsReadFault.ReservedLabelType, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_NameOf257Bytes_IsNameTooLong()
    {
        var message = HeaderBytes(1).Concat(Labels(4, 63)).Concat(Bytes(0)).Concat(QuestionTypeAndClass).ToArray();

        Assert.AreEqual(DnsReadFault.NameTooLong, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_NameOf255Bytes_IsRead()
    {
        var message = HeaderBytes(1).Concat(Labels(3, 63)).Concat(Labels(1, 61)).Concat(Bytes(0)).Concat(QuestionTypeAndClass).ToArray();

        var outcome = DnsMessageReader.Read(message);

        Assert.AreEqual(DnsReadFault.None, outcome.Fault);
        Assert.AreEqual(255, outcome.Questions[0].Name.WireLength);
    }

    [TestMethod]
    public void Read_NamePast255BytesOnlyOnceAPointerIsFollowed_IsNameTooLong()
    {
        // The first name is 193 bytes; the second is a 63-byte label and a pointer to it, 257 bytes.
        var message = HeaderBytes(2)
            .Concat(Labels(3, 63)).Concat(Bytes(0)).Concat(QuestionTypeAndClass)
            .Concat(Labels(1, 63)).Concat(Bytes(0xC0, 12)).Concat(QuestionTypeAndClass)
            .ToArray();

        var outcome = DnsMessageReader.Read(message);

        Assert.AreEqual(DnsReadFault.NameTooLong, outcome.Fault);
        Assert.HasCount(1, outcome.Questions);
    }

    [TestMethod]
    public void Read_QuestionCountPastTheQuestionsPresent_IsPastEndOfMessage()
    {
        var outcome = DnsMessageReader.Read(HeaderBytes(1));

        Assert.AreEqual(DnsReadFault.PastEndOfMessage, outcome.Fault);
        Assert.IsNotNull(outcome.Header);
        Assert.IsEmpty(outcome.Questions);
    }

    [TestMethod]
    public void Read_FaultInTheAnswerSection_KeepsTheHeaderAndQuestion()
    {
        var message = HeaderBytes(1, answers: 1).Concat(Bytes(0)).Concat(QuestionTypeAndClass).ToArray();

        var outcome = DnsMessageReader.Read(message);

        Assert.AreEqual(DnsReadFault.PastEndOfMessage, outcome.Fault);
        Assert.AreEqual(0x1234, outcome.Header!.Id);
        Assert.IsTrue(outcome.Header.IsRecursionDesired);
        Assert.HasCount(1, outcome.Questions);
        Assert.IsNull(outcome.Message);
    }

    [TestMethod]
    public void Read_ByteAfterTheLastRecord_IsRefused()
    {
        var message = HeaderBytes(1).Concat(Bytes(0)).Concat(QuestionTypeAndClass).Concat(Bytes(0)).ToArray();

        Assert.AreEqual(DnsReadFault.BytesAfterLastRecord, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_RdlengthPastTheMessageEnd_IsPastEndOfMessage()
    {
        var message = Answer(DnsRecordType.A, Bytes(0, 5, 1, 2, 3, 4));

        Assert.AreEqual(DnsReadFault.PastEndOfMessage, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    [DataRow(DnsRecordType.A, new byte[] { 0, 3, 1, 2, 3 })]
    [DataRow(DnsRecordType.A, new byte[] { 0, 5, 1, 2, 3, 4, 5 })]
    [DataRow(DnsRecordType.AAAA, new byte[] { 0, 4, 1, 2, 3, 4 })]
    [DataRow(DnsRecordType.TXT, new byte[] { 0, 0 })]
    [DataRow(DnsRecordType.TXT, new byte[] { 0, 2, 5, 1 })]
    [DataRow(DnsRecordType.NS, new byte[] { 0, 2, 1, 97, 0 })]
    [DataRow(DnsRecordType.MX, new byte[] { 0, 2, 0, 10, 0 })]
    [DataRow(DnsRecordType.SOA, new byte[] { 0, 2, 0, 0 })]
    [DataRow(DnsRecordType.SRV, new byte[] { 0, 6, 0, 0, 0, 0, 0, 53, 0 })]
    [DataRow(DnsRecordType.CAA, new byte[] { 0, 2, 0, 0 })]
    [DataRow(DnsRecordType.CAA, new byte[] { 0, 4, 0, 2, 33, 33 })]
    [DataRow(DnsRecordType.OPT, new byte[] { 0, 3, 0, 12, 0 })]
    [DataRow(DnsRecordType.OPT, new byte[] { 0, 4, 0, 12, 0, 1 })]
    public void Read_RdataNotHoldingItsTypesFields_IsRecordDataMalformed(DnsRecordType type, byte[] lengthAndRdata)
    {
        var message = Answer(type, lengthAndRdata);

        Assert.AreEqual(DnsReadFault.RecordDataMalformed, DnsMessageReader.Read(message).Fault);
    }

    [TestMethod]
    public void Read_NameInRdataPointingToTheQuestion_ReadsPastTheRdataEnd()
    {
        var message = Answer(DnsRecordType.CNAME, Bytes(0, 2, 0xC0, 12));

        var outcome = DnsMessageReader.Read(message);

        Assert.AreEqual(DnsReadFault.None, outcome.Fault);
        Assert.AreEqual("a.", ((DnsDomainNameData)outcome.Message!.Answers[0].Data).Name.ToString());
    }

    /// <summary>A message whose one question is <c>a.</c> and whose one answer, owned by the root, has <paramref name="lengthAndRdata"/>.</summary>
    private static byte[] Answer(DnsRecordType type, byte[] lengthAndRdata) =>
        HeaderBytes(1, answers: 1)
            .Concat(Bytes(1, 'a', 0)).Concat(QuestionTypeAndClass)
            .Concat(Bytes(0, (int)type >> 8, (int)type & 0xFF, 0, 1, 0, 0, 0, 0))
            .Concat(lengthAndRdata)
            .ToArray();

    private static byte[] Labels(int count, int length) =>
        Enumerable.Range(0, count).SelectMany(_ => Bytes(length).Concat(Enumerable.Repeat((byte)'x', length))).ToArray();
}
