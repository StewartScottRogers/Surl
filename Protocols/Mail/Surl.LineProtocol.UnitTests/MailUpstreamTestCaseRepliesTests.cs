using static Surl.LineProtocol.Wire;

namespace Surl.LineProtocol;

[TestClass]
public sealed class MailUpstreamTestCaseRepliesTests
{
    private static readonly Dictionary<string, byte[]> NoParts = [];

    [TestMethod]
    public void SupportedServerCommands_IsExactlyTheKeptInstructions()
    {
        CollectionAssert.AreEquivalent(
            new[] { "Testnum", "REPLY", "REPLYLF", "COUNT", "CAPA", "AUTH", "POSTFETCH" },
            MailUpstreamTestCaseReplies.SupportedServerCommands.ToArray());
        Assert.HasCount(7, MailUpstreamTestCaseReplies.SupportedServerCommands);
    }

    [TestMethod]
    public void Constructor_NullParts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailUpstreamTestCaseReplies(null!, []));
    }

    [TestMethod]
    public void Constructor_NullServerCommands_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailUpstreamTestCaseReplies(NoParts, null!));
    }

    [TestMethod]
    public void Constructor_RefusedInstruction_ThrowsNotSupportedException()
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => new MailUpstreamTestCaseReplies(NoParts, ["DELAY LIST 2"]));

        StringAssert.Contains(exception.Message, "DELAY");
    }

    [TestMethod]
    public void Constructor_MalformedCount_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => new MailUpstreamTestCaseReplies(NoParts, ["COUNT RCPT x"]));
    }

    [TestMethod]
    public void TakeScriptedReply_NullCommandText_Throws()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);

        Assert.ThrowsExactly<ArgumentNullException>(() => replies.TakeScriptedReply(null!));
    }

    [TestMethod]
    public void TakeScriptedReply_CommandWithArguments_AnswersByItsFirstWord()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["REPLY MAIL 501 bad\\x21"]);

        Assert.AreEqual("501 bad!\r\n", Text(replies.TakeScriptedReply("MAIL FROM:<a@b>")));
    }

    [TestMethod]
    public void TakeScriptedReply_CommandAlone_AnswersByIt()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["REPLYLF QUIT +OK bye"]);

        Assert.AreEqual("+OK bye\n", Text(replies.TakeScriptedReply("QUIT")));
    }

    [TestMethod]
    public void TakeScriptedReply_EmptySaslResponse_AnswersTheEmptyCommand()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["REPLY  235 ok"]);

        Assert.AreEqual("235 ok\r\n", Text(replies.TakeScriptedReply(string.Empty)));
    }

    [TestMethod]
    public void TakeScriptedReply_QuotedFullText_AnswersOnlyThatText()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["REPLY \"AUTH PLAIN\" 334 PLAIN"]);

        Assert.AreEqual("334 PLAIN\r\n", Text(replies.TakeScriptedReply("AUTH PLAIN")));
        Assert.IsNull(replies.TakeScriptedReply("AUTH LOGIN"));
    }

    [TestMethod]
    public void TakeScriptedReply_Count_SpendsOneUsePerAnswer()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["REPLY RCPT 550 no", "COUNT RCPT 1"]);

        Assert.AreEqual("550 no\r\n", Text(replies.TakeScriptedReply("RCPT TO:<a>")));
        Assert.IsNull(replies.TakeScriptedReply("RCPT TO:<b>"));
    }

    [TestMethod]
    public void TakeScriptedReply_NotScripted_IsNull()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);

        Assert.IsNull(replies.TakeScriptedReply("NOOP"));
    }

    [TestMethod]
    public void TakeScriptedReply_LatinOneEscape_IsOneByte()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["REPLY NOOP \\xe9"]);

        CollectionAssert.AreEqual(new byte[] { 0xE9, 0x0D, 0x0A }, replies.TakeScriptedReply("NOOP"));
    }

    [TestMethod]
    public void TakeWelcome_ReplyWelcome_IsTheGreetingOnce()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["REPLY welcome * OK hi"]);

        Assert.AreEqual("* OK hi\r\n", Text(replies.TakeWelcome()));
        Assert.IsNull(replies.TakeWelcome());
    }

    [TestMethod]
    public void TakeWelcome_NoReplyWelcome_IsNull()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);

        Assert.IsNull(replies.TakeWelcome());
    }

    [TestMethod]
    public void CapabilitiesAuthMechanismsAndPostFetch_AreTheCasesInstructions()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, ["Testnum 800", "CAPA IDLE \"X Y\"", "AUTH PLAIN LOGIN", "POSTFETCH FLAGS (\\Seen)"]);

        CollectionAssert.AreEqual(new[] { "IDLE", "X Y" }, replies.Capabilities.ToArray());
        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN" }, replies.AuthMechanisms.ToArray());
        Assert.AreEqual("FLAGS (\\Seen)", replies.PostFetch);
    }

    [TestMethod]
    [DataRow(new string[0], false, DisplayName = "Neither")]
    [DataRow(new[] { "CAPA IDLE" }, true, DisplayName = "Capabilities only")]
    [DataRow(new[] { "AUTH PLAIN" }, true, DisplayName = "Mechanisms only")]
    [DataRow(new[] { "CAPA IDLE", "AUTH PLAIN" }, true, DisplayName = "Both")]
    public void OffersCapabilities_IsWhetherAnyCapabilityOrMechanismIsOffered(string[] lines, bool expected)
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, lines);

        Assert.AreEqual(expected, replies.OffersCapabilities);
    }

    [TestMethod]
    public void FindData_NullName_Throws()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);

        Assert.ThrowsExactly<ArgumentNullException>(() => replies.FindData(null!));
    }

    [TestMethod]
    [DataRow("INBOX", "plain", DisplayName = "No digits picks data")]
    [DataRow("1", "plain", DisplayName = "A small number picks data")]
    [DataRow("10000", "plain", DisplayName = "10000 is not over 10000")]
    [DataRow("800/;MAILINDEX=10002", "plain", DisplayName = "Only the first run of digits counts")]
    [DataRow("INBOX/;MAILINDEX=10002", "second", DisplayName = "Over 10000 picks dataNUM")]
    [DataRow("10003", "third", DisplayName = "A message number over 10000 picks dataNUM")]
    [DataRow("10007", "plain", DisplayName = "A missing dataNUM falls back to data")]
    [DataRow("99999999999999999999999", "plain", DisplayName = "Digits too long for a number pick data")]
    public void FindData_PicksThePartByTheNamesNumber(string name, string expected)
    {
        var replies = new MailUpstreamTestCaseReplies(
            new Dictionary<string, byte[]> { ["data"] = Bytes("plain"), ["data2"] = Bytes("second"), ["data3"] = Bytes("third") },
            []);

        Assert.AreEqual(expected, Text(replies.FindData(name)));
    }

    [TestMethod]
    public void FindData_NoDataPart_IsEmpty()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);

        Assert.IsEmpty(replies.FindData("10002"));
    }

    [TestMethod]
    public void ReceivedBytes_IsEveryCommandLineWithCrLfInOrder()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);
        Assert.IsEmpty(replies.ReceivedBytes);

        replies.RecordCommandLine(Bytes("EHLO client"));
        replies.RecordCommandLine([]);
        replies.RecordCommandLine(Bytes("QUIT"));

        Assert.AreEqual("EHLO client\r\n\r\nQUIT\r\n", Text(replies.ReceivedBytes));
        Assert.IsEmpty(replies.UploadedBytes);
    }

    [TestMethod]
    public void UploadedBytes_IsEveryUploadAsSentInOrder()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);

        replies.RecordUpload(Bytes("literal one"));
        replies.RecordDotStuffedUpload(Bytes(".dot\r\nline"));

        Assert.AreEqual("literal one..dot\r\nline\r\n.\r\n", Text(replies.UploadedBytes));
        Assert.IsEmpty(replies.ReceivedBytes);
    }

    [TestMethod]
    public void ReceivedBytes_IsACopy()
    {
        var replies = new MailUpstreamTestCaseReplies(NoParts, []);
        replies.RecordCommandLine(Bytes("A"));

        replies.ReceivedBytes[0] = (byte)'B';

        Assert.AreEqual("A\r\n", Text(replies.ReceivedBytes));
    }

    [TestMethod]
    public void Parts_AreCopiedAtConstruction()
    {
        var parts = new Dictionary<string, byte[]> { ["data"] = Bytes("kept") };
        var replies = new MailUpstreamTestCaseReplies(parts, []);

        parts["data"] = Bytes("changed");

        Assert.AreEqual("kept", Text(replies.FindData("1")));
    }
}
