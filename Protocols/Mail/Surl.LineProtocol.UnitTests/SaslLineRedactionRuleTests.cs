using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

[TestClass]
public sealed class SaslLineRedactionRuleTests
{
    private static SaslLineRedactionRule SmtpRule() => new(["AUTH"], "334 ");

    private static string Redact(SaslLineRedactionRule rule, string line)
    {
        var output = new ArrayBufferWriter<byte>();
        rule.Redact(Wire.Bytes(line), output);
        return Wire.Text(output.WrittenSpan);
    }

    private static string Chunked(SaslLineRedactionRule rule, string received)
    {
        var output = new ArrayBufferWriter<byte>();
        new LineBytesRedactor(rule).Redact(Wire.Bytes(received), output);
        return Wire.Text(output.WrittenSpan);
    }

    [TestMethod]
    public void Constructor_RejectsNullCommandWordsAndEmptyPrefix()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SaslLineRedactionRule(null!, "+ "));
        Assert.ThrowsExactly<ArgumentException>(() => new SaslLineRedactionRule(["AUTH"], ""));
    }

    [TestMethod]
    public void Redact_RejectsNullOutput() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => SmtpRule().Redact("AUTH"u8, null!));

    [TestMethod]
    public void Redact_AuthPlainInitialResponse_IsMasked() =>
        Assert.AreEqual("AUTH PLAIN ****", Redact(SmtpRule(), "AUTH PLAIN AGFsaWNlAHMzY3JldA=="));

    [TestMethod]
    public void Redact_CommandWordIsMatchedWithoutRegardToCase() =>
        Assert.AreEqual("auth plain ****", Redact(SmtpRule(), "auth plain AGFsaWNlAHMzY3JldA=="));

    [TestMethod]
    public void Redact_ImapTaggedAuthenticateInitialResponse_IsMasked() =>
        Assert.AreEqual(
            "A1 AUTHENTICATE PLAIN ****",
            Redact(new SaslLineRedactionRule(["AUTHENTICATE"], "+ "), "A1 AUTHENTICATE PLAIN AGFsaWNlAHMzY3JldA=="));

    [TestMethod]
    [DataRow("AUTH  PLAIN AGFsaWNlAHMzY3JldA==", "AUTH  PLAIN ****")]
    [DataRow("AUTH PLAIN  AGFsaWNlAHMzY3JldA==", "AUTH PLAIN  ****")]
    [DataRow("AUTH   PLAIN   AGFsaWNlAHMzY3JldA==  extra", "AUTH   PLAIN   ****  extra")]
    public void Redact_InitialResponseAfterARunOfSpaces_IsMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(SmtpRule(), line));

    [TestMethod]
    [DataRow("A1  AUTHENTICATE PLAIN AGFsaWNlAHMzY3JldA==", "A1  AUTHENTICATE PLAIN ****")]
    [DataRow("A1 AUTHENTICATE  PLAIN AGFsaWNlAHMzY3JldA==", "A1 AUTHENTICATE  PLAIN ****")]
    [DataRow("A1 AUTHENTICATE PLAIN  AGFsaWNlAHMzY3JldA==", "A1 AUTHENTICATE PLAIN  ****")]
    [DataRow("AUTHENTICATE  PLAIN AGFsaWNlAHMzY3JldA==", "AUTHENTICATE  PLAIN ****")]
    [DataRow("AUTHENTICATE PLAIN  AGFsaWNlAHMzY3JldA==", "AUTHENTICATE PLAIN  ****")]
    public void Redact_ImapInitialResponseAfterARunOfSpaces_IsMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(new SaslLineRedactionRule(["AUTHENTICATE"], "+ "), line));

    [TestMethod]
    [DataRow("AUTH\tPLAIN\tAGFsaWNlAHMzY3JldA==", "AUTH\tPLAIN\t****")]
    [DataRow("AUTH PLAIN\tAGFsaWNlAHMzY3JldA==", "AUTH PLAIN\t****")]
    [DataRow("AUTH \t PLAIN \t AGFsaWNlAHMzY3JldA==", "AUTH \t PLAIN \t ****")]
    [DataRow("\tAUTH PLAIN AGFsaWNlAHMzY3JldA==", "\tAUTH PLAIN ****")]
    public void Redact_AuthInitialResponseOnALineSeparatedByTabs_IsMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(SmtpRule(), line));

    [TestMethod]
    [DataRow("A1\tAUTHENTICATE\tPLAIN\tAGFsaWNlAHMzY3JldA==", "A1\tAUTHENTICATE\tPLAIN\t****")]
    [DataRow("A1 AUTHENTICATE\tPLAIN AGFsaWNlAHMzY3JldA==", "A1 AUTHENTICATE\tPLAIN ****")]
    [DataRow("AUTHENTICATE\tPLAIN\tAGFsaWNlAHMzY3JldA==", "AUTHENTICATE\tPLAIN\t****")]
    public void Redact_TaggedAuthenticateInitialResponseOnALineSeparatedByTabs_IsMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(new SaslLineRedactionRule(["AUTHENTICATE"], "+ "), line));

    [TestMethod]
    [DataRow("AUTH PLAIN AGFsaWNl\tAHMzY3JldA==", "AUTH PLAIN ****")]
    [DataRow("AUTH\tPLAIN abc extra", "AUTH\tPLAIN ****")]
    [DataRow("AUTH PL\tAIN AGFsaWNlAHMzY3JldA==", "AUTH PL\t****")]
    public void Redact_LineWithATab_WritesNothingAfterTheMask(string line, string logged) =>
        Assert.AreEqual(logged, Redact(SmtpRule(), line));

    [TestMethod]
    public void Redact_TabSeparatedAuthWithoutInitialResponse_IsUnchanged() =>
        Assert.AreEqual("AUTH\tPLAIN\t", Redact(SmtpRule(), "AUTH\tPLAIN\t"));

    [TestMethod]
    public void Redact_SpacesBeforeTheCommandWord_StillMasksTheInitialResponse() =>
        Assert.AreEqual("  AUTH PLAIN ****", Redact(SmtpRule(), "  AUTH PLAIN AGFsaWNlAHMzY3JldA=="));

    [TestMethod]
    public void Redact_TextAfterTheInitialResponse_IsKept() =>
        Assert.AreEqual("AUTH PLAIN **** extra", Redact(SmtpRule(), "AUTH PLAIN abc extra"));

    [TestMethod]
    public void Redact_MoreTokensThanTheRuleReads_MasksOnlyTheInitialResponse() =>
        Assert.AreEqual("AUTH PLAIN **** a b c", Redact(SmtpRule(), "AUTH PLAIN abc a b c"));

    [TestMethod]
    public void Redact_AuthWithoutInitialResponse_IsUnchanged()
    {
        Assert.AreEqual("AUTH LOGIN", Redact(SmtpRule(), "AUTH LOGIN"));
        Assert.AreEqual("AUTH", Redact(SmtpRule(), "AUTH"));
        Assert.AreEqual("AUTH PLAIN ", Redact(SmtpRule(), "AUTH PLAIN "));
        Assert.AreEqual("AUTH  PLAIN  ", Redact(SmtpRule(), "AUTH  PLAIN  "));
    }

    [TestMethod]
    public void Redact_OtherCommands_AreUnchanged()
    {
        Assert.AreEqual("MAIL FROM:<a@b> AUTH=<>", Redact(SmtpRule(), "MAIL FROM:<a@b> AUTH=<>"));
        Assert.AreEqual("A1 LOGIN alice pw", Redact(SmtpRule(), "A1 LOGIN alice pw"));
        Assert.AreEqual(string.Empty, Redact(SmtpRule(), string.Empty));
    }

    [TestMethod]
    public void Redact_AuthLoginExchange_MasksBothContinuationAnswers()
    {
        var rule = SmtpRule();
        Assert.AreEqual("AUTH LOGIN", Redact(rule, "AUTH LOGIN"));
        rule.ObserveSent("334 VXNlcm5hbWU6\r\n"u8);
        Assert.AreEqual("****", Redact(rule, "YWxpY2U="));
        rule.ObserveSent("334 UGFzc3dvcmQ6\r\n"u8);
        Assert.AreEqual("****", Redact(rule, "czNjcmV0"));
        rule.ObserveSent("235 2.7.0 Authentication successful\r\n"u8);
        Assert.AreEqual("MAIL FROM:<a@b>", Redact(rule, "MAIL FROM:<a@b>"));
    }

    [TestMethod]
    public void Redact_CancelLine_IsUnchangedAndEndsThePendingContinuation()
    {
        var rule = SmtpRule();
        rule.ObserveSent("334 \r\n"u8);
        Assert.AreEqual("*", Redact(rule, "*"));
        Assert.AreEqual("NOOP", Redact(rule, "NOOP"));
    }

    [TestMethod]
    public void Redact_EmptyAnswerToAContinuation_IsUnchanged()
    {
        var rule = SmtpRule();
        rule.ObserveSent("334 \r\n"u8);
        Assert.AreEqual(string.Empty, Redact(rule, string.Empty));
    }

    [TestMethod]
    public void Redact_ClientLineWithNoContinuationPending_IsLoggedUnchanged()
    {
        var rule = SmtpRule();
        rule.ObserveSent("220 ready\r\n"u8);
        Assert.AreEqual("EHLO client", Redact(rule, "EHLO client"));
        Assert.AreEqual("YWxpY2U=", Redact(rule, "YWxpY2U="));
    }

    [TestMethod]
    public void Redact_SentLineThatIsNotAContinuation_ClearsThePendingMark()
    {
        var rule = SmtpRule();
        rule.ObserveSent("334 x\r\n535 denied\r\n"u8);
        Assert.AreEqual("YWxpY2U=", Redact(rule, "YWxpY2U="));
    }

    [TestMethod]
    public void ObserveSent_ContinuationWithoutLineEnd_StillMarks()
    {
        var rule = new SaslLineRedactionRule(["AUTHENTICATE"], "+ ");
        rule.ObserveSent("+ "u8);
        Assert.AreEqual("****", Redact(rule, "c2VjcmV0"));
    }

    [TestMethod]
    public void ObserveSent_BlankLinesAndImapPlusOk_DoNotMark()
    {
        var rule = new SaslLineRedactionRule(["AUTH"], "+ ");
        rule.ObserveSent("\r\n+OK done\r\n"u8);
        Assert.AreEqual("c2VjcmV0", Redact(rule, "c2VjcmV0"));
    }

    [TestMethod]
    public void LineBytesRedactor_WithTheRule_RedactsAnAuthLoginExchangeInAnyChunking()
    {
        var rule = SmtpRule();
        var redactor = new LineBytesRedactor(rule);
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact("AUTH LO"u8, output);
        redactor.Redact("GIN\r\n"u8, output);
        redactor.ObserveSent("334 VXNlcm5hbWU6\r\n"u8);
        redactor.Redact("YWxpY2U=\r\n"u8, output);
        Assert.AreEqual("AUTH LOGIN\r\n****\r\n", Wire.Text(output.WrittenSpan));
        Assert.AreEqual("AUTH PLAIN ****\r\n", Chunked(SmtpRule(), "AUTH PLAIN AGFsaWNl\r\n"));
    }
}
