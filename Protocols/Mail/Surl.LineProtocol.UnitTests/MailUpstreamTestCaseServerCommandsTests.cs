namespace Surl.LineProtocol;

[TestClass]
public sealed class MailUpstreamTestCaseServerCommandsTests
{
    [TestMethod]
    public void Supported_IsTheKeptInstructions()
    {
        CollectionAssert.AreEquivalent(
            new[] { "Testnum", "REPLY", "REPLYLF", "COUNT", "CAPA", "AUTH", "POSTFETCH" },
            MailUpstreamTestCaseServerCommands.Supported.ToArray());
    }

    [TestMethod]
    public void Parse_NoLines_ScriptsNothing()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse([]);

        Assert.IsNull(commands.TakeReply("NOOP", "NOOP"));
        Assert.IsNull(commands.TakeWelcome());
        Assert.IsEmpty(commands.Capabilities);
        Assert.IsEmpty(commands.AuthMechanisms);
        Assert.AreEqual(string.Empty, commands.PostFetch);
    }

    [TestMethod]
    public void Parse_BlankCommentAndIndentedLines_SkipsBlanksAndCommentsAndTrimsTheRest()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["", "   ", "# REPLY NOOP ignored", "  Testnum 800", "  REPLY NOOP 200 fine\r\n"]);

        Assert.AreEqual("200 fine\r\n", commands.TakeReply("NOOP", "NOOP"));
    }

    [TestMethod]
    public void Parse_TestnumAlone_IsAccepted()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["Testnum"]);

        Assert.IsNull(commands.TakeReply("Testnum", "Testnum"));
    }

    [TestMethod]
    public void TakeReply_Reply_EndsWithCrLfAndKeepsAnswering()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY EHLO 500 no"]);

        Assert.AreEqual("500 no\r\n", commands.TakeReply("EHLO client", "EHLO"));
        Assert.AreEqual("500 no\r\n", commands.TakeReply("EHLO client", "EHLO"));
    }

    [TestMethod]
    public void TakeReply_ReplyLf_EndsWithLf()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLYLF USER +OK"]);

        Assert.AreEqual("+OK\n", commands.TakeReply("USER me", "USER"));
    }

    [TestMethod]
    public void TakeReply_CommandsMatchCaseForCase()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY EHLO 500 no"]);

        Assert.IsNull(commands.TakeReply("ehlo client", "ehlo"));
    }

    [TestMethod]
    public void TakeReply_EmptyCommand_AnswersAnEmptySaslResponse()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY  235 Authenticated"]);

        Assert.AreEqual("235 Authenticated\r\n", commands.TakeReply(string.Empty, string.Empty));
    }

    [TestMethod]
    public void TakeReply_ReplyWithNoResponse_AnswersALineEndingAlone()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY NOOP"]);

        Assert.AreEqual("\r\n", commands.TakeReply("NOOP", "NOOP"));
    }

    [TestMethod]
    [DataRow("REPLY")]
    [DataRow("REPLYLF")]
    public void TakeReply_QuotedFullText_MatchesOnlyThatExactTextAndEndsWithCrLf(string word)
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse([$"{word} \"AUTH PLAIN\" 334 PLAIN go"]);

        Assert.AreEqual("334 PLAIN go\r\n", commands.TakeReply("AUTH PLAIN", "AUTH"));
        Assert.IsNull(commands.TakeReply("AUTH LOGIN", "AUTH"));
    }

    [TestMethod]
    public void TakeReply_QuotedFullText_WinsOverTheCommandWordAndSpendsNoCount()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY AUTH 504 no", "COUNT AUTH 1", "REPLY \"AUTH PLAIN\" 334 go"]);

        Assert.AreEqual("334 go\r\n", commands.TakeReply("AUTH PLAIN", "AUTH"));
        Assert.AreEqual("334 go\r\n", commands.TakeReply("AUTH PLAIN", "AUTH"));
        Assert.AreEqual("504 no\r\n", commands.TakeReply("AUTH LOGIN", "AUTH"));
        Assert.IsNull(commands.TakeReply("AUTH LOGIN", "AUTH"));
    }

    [TestMethod]
    public void TakeReply_Count_LimitsTheReplyToThatManyAnswers()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY RCPT 550 no", "COUNT RCPT 2"]);

        Assert.AreEqual("550 no\r\n", commands.TakeReply("RCPT TO:<a>", "RCPT"));
        Assert.AreEqual("550 no\r\n", commands.TakeReply("RCPT TO:<b>", "RCPT"));
        Assert.IsNull(commands.TakeReply("RCPT TO:<c>", "RCPT"));
    }

    [TestMethod]
    public void TakeReply_CountForACommandWithNoReply_AnswersNothing()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["COUNT RCPT 2"]);

        Assert.IsNull(commands.TakeReply("RCPT TO:<a>", "RCPT"));
    }

    [TestMethod]
    [DataRow("COUNT RCPT", DisplayName = "No number")]
    [DataRow("COUNT RCPT -1", DisplayName = "A negative number")]
    [DataRow("COUNT RCPT two", DisplayName = "Not a number")]
    [DataRow("COUNT", DisplayName = "No command and no number")]
    public void Parse_MalformedCount_ThrowsFormatException(string line)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => MailUpstreamTestCaseServerCommands.Parse([line]));

        StringAssert.Contains(exception.Message, line);
    }

    [TestMethod]
    [DataRow("REPLY \"AUTH PLAIN 334 go")]
    [DataRow("REPLY \"AUTH PLAIN\"")]
    public void Parse_UnclosedQuotedReply_ThrowsFormatException(string line)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => MailUpstreamTestCaseServerCommands.Parse([line]));

        StringAssert.Contains(exception.Message, "does not close its quoted command");
    }

    [TestMethod]
    [DataRow("DELAY")]
    [DataRow("SLOWDOWN")]
    [DataRow("SLOWDOWNDATA")]
    [DataRow("NOSAVE")]
    [DataRow("RETRSIZE")]
    public void Parse_RefusedInstruction_ThrowsNotSupportedExceptionNamingIt(string word)
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => MailUpstreamTestCaseServerCommands.Parse([$"{word} 5"]));

        StringAssert.Contains(exception.Message, $"servercmd-unsupported:{word}");
    }

    [TestMethod]
    public void TakeWelcome_ReplyWelcome_GivesTheGreetingOnce()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY welcome 220 hello"]);

        Assert.AreEqual("220 hello\r\n", commands.TakeWelcome());
        Assert.IsNull(commands.TakeWelcome());
    }

    [TestMethod]
    public void TakeWelcome_ReplyLfWelcome_IsEndedWithCrLf()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLYLF welcome 220 hello"]);

        Assert.AreEqual("220 hello\n\r\n", commands.TakeWelcome());
    }

    [TestMethod]
    public void Capabilities_Capa_SplitsOnSpacesKeepingAQuotedCapabilitysSpaces()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["CAPA  IMAP4rev1  \"SASL-IR X\" IDLE "]);

        CollectionAssert.AreEqual(new[] { "IMAP4rev1", "SASL-IR X", "IDLE" }, commands.Capabilities.ToArray());
    }

    [TestMethod]
    public void AuthMechanisms_Auth_SplitsOnSpaces()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["AUTH  PLAIN LOGIN  XOAUTH2"]);

        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN", "XOAUTH2" }, commands.AuthMechanisms.ToArray());
    }

    [TestMethod]
    public void PostFetch_Postfetch_IsTheRestOfTheLine()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["POSTFETCH FLAGS (\\Seen)"]);

        Assert.AreEqual("FLAGS (\\Seen)", commands.PostFetch);
    }

    [TestMethod]
    public void TakeReply_ResponseEscapes_AreReadAsPerlDoubleQuoted()
    {
        var commands = MailUpstreamTestCaseServerCommands.Parse(["REPLY DATA 354 go\\r\\n250 ok"]);

        Assert.AreEqual("354 go\r\n250 ok\r\n", commands.TakeReply("DATA", "DATA"));
    }

    [TestMethod]
    [DataRow("a\\rb", "a\rb", DisplayName = "\\r is CR")]
    [DataRow("a\\nb", "a\nb", DisplayName = "\\n is LF")]
    [DataRow("a\\tb", "a\tb", DisplayName = "\\t is a tab")]
    [DataRow("\\x41\\x4a", "AJ", DisplayName = "\\xHH is that character")]
    [DataRow("\\x4a7", "J7", DisplayName = "\\x takes two digits at most")]
    [DataRow("\\x9z", "\tz", DisplayName = "\\x takes one digit when one follows")]
    [DataRow("\\xz", "\0z", DisplayName = "\\x with no digit is the character 0")]
    [DataRow("\\x", "\0", DisplayName = "\\x at the end is the character 0")]
    [DataRow("\\\"\\\\\\q", "\"\\q", DisplayName = "A backslash before another character stands for it")]
    [DataRow("end\\", "end\\", DisplayName = "A trailing backslash is kept")]
    [DataRow("plain", "plain", DisplayName = "No escape")]
    public void ReadPerlDoubleQuoted_ReadsEachEscape(string text, string expected)
    {
        Assert.AreEqual(expected, MailUpstreamTestCaseServerCommands.ReadPerlDoubleQuoted(text));
    }
}
