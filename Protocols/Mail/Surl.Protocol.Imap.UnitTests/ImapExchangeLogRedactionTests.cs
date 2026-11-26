using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

[TestClass]
public sealed class ImapExchangeLogRedactionTests
{
    private static IReceivedBytesRedactor NewRedactor() =>
        ImapTestExchange.Server(ImapTestExchange.AnonymousStore(new ManualTimeProvider()))
            .CreateRedactor(new ExchangeRedactionContext(1, "imap"));

    /// <summary>
    /// Plays an exchange: a step starting with <c>&lt;</c> is what the server sent (without the
    /// <c>&lt;</c>), any other step is what the client sent. Returns what the client's bytes log as.
    /// </summary>
    private static string Logged(params string[] steps)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var step in steps)
        {
            if (step.StartsWith('<'))
            {
                redactor.ObserveSent(Encoding.ASCII.GetBytes(step[1..]));
            }
            else
            {
                redactor.Redact(Encoding.ASCII.GetBytes(step), output);
            }
        }

        redactor.Flush(output);
        return Encoding.ASCII.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void Login_WithAnAtomPassword_IsMasked() =>
        Assert.AreEqual("A1 LOGIN alice ****\r\n", Logged("A1 LOGIN alice s3cret\r\n"));

    [TestMethod]
    public void Login_WithAQuotedPassword_IsMaskedWhole() =>
        Assert.AreEqual("A1 LOGIN alice ****\r\n", Logged("A1 LOGIN alice \"s3 cret\"\r\n"));

    [TestMethod]
    public void Login_WithAQuotedPasswordHoldingAnEscapedQuote_IsMaskedWhole() =>
        Assert.AreEqual("A1 LOGIN \"al ice\" ****\r\n", Logged("A1 LOGIN \"al ice\" \"s3\\\" cret\"\r\n"));

    [TestMethod]
    public void Login_WithAnUnterminatedQuotedPassword_IsMasked() =>
        Assert.AreEqual("A1 LOGIN alice ****\r\n", Logged("A1 LOGIN alice \"s3 cret\r\n"));

    [TestMethod]
    public void Login_InLowerCase_IsMaskedKeepingTheClientsCase() =>
        Assert.AreEqual("a1 login alice ****\r\n", Logged("a1 login alice s3cret\r\n"));

    [TestMethod]
    public void Login_WithAPasswordLiteralOnTheNextLine_IsMasked() =>
        Assert.AreEqual(
            "A1 LOGIN alice {6}\r\n****\r\n",
            Logged("A1 LOGIN alice {6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithANonSynchronizingPasswordLiteral_IsMasked() =>
        Assert.AreEqual("A1 LOGIN alice {6+}\r\n****\r\n", Logged("A1 LOGIN alice {6+}\r\ns3cret\r\n"));

    [TestMethod]
    public void Login_WithAPasswordLiteralHoldingALineBreak_IsMaskedOnEveryLine() =>
        Assert.AreEqual("A1 LOGIN alice {7+}\r\n****\r\n****\r\n", Logged("A1 LOGIN alice {7+}\r\ns3\r\nret\r\n"));

    [TestMethod]
    public void Login_WithAUserLiteralAndAnAtomPassword_MasksThePassword() =>
        Assert.AreEqual(
            "A1 LOGIN {5}\r\nalice ****\r\n",
            Logged("A1 LOGIN {5}\r\n", "<+ Ready for literal data\r\n", "alice s3cret\r\n"));

    [TestMethod]
    public void Login_WithAUserLiteralAndAPasswordLiteral_MasksThePassword() =>
        Assert.AreEqual(
            "A1 LOGIN {5}\r\nalice {6}\r\n****\r\n",
            Logged("A1 LOGIN {5}\r\n", "<+ Ready for literal data\r\n", "alice {6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithAnEmptyUserLiteralThenAPasswordLiteral_MasksThePassword() =>
        Assert.AreEqual("A1 LOGIN {0+}\r\n {6+}\r\n****\r\n", Logged("A1 LOGIN {0+}\r\n", " {6+}\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_ThenAnotherCommand_LogsTheOtherCommandUnchanged() =>
        Assert.AreEqual("A1 LOGIN alice ****\r\nA2 SELECT INBOX\r\n", Logged("A1 LOGIN alice s3cret\r\n", "A2 SELECT INBOX\r\n"));

    [TestMethod]
    public void Login_WithNoArguments_IsLoggedAsSent() =>
        Assert.AreEqual("A1 LOGIN\r\nA2 LOGIN \r\n", Logged("A1 LOGIN\r\nA2 LOGIN \r\n"));

    [TestMethod]
    public void Login_WithOnlyAUser_IsLoggedAsSent() =>
        Assert.AreEqual("A1 LOGIN alice\r\n", Logged("A1 LOGIN alice\r\n"));

    [TestMethod]
    public void Login_SplitAcrossReads_IsMasked() =>
        Assert.AreEqual("A1 LOGIN alice ****\r\n", Logged("A1 LO", "GIN alice s3c", "ret\r\n"));

    [TestMethod]
    public void Login_RefusedLiteral_LetsTheNextLineBeReadAsACommand() =>
        Assert.AreEqual(
            "A1 LOGIN alice {6}\r\nA2 LOGIN bob ****\r\n",
            Logged("A1 LOGIN alice {6}\r\n", "<A1 BAD Literal too long\r\n", "A2 LOGIN bob s3cret\r\n"));

    [TestMethod]
    public void Login_WithTwoSpacesAfterTheTagAndAnAtomPassword_IsMasked() =>
        Assert.AreEqual("A1  LOGIN alice ****\r\n", Logged("A1  LOGIN alice s3cret\r\n"));

    [TestMethod]
    public void Login_WithTwoSpacesAfterTheTagAndAQuotedPassword_IsMasked() =>
        Assert.AreEqual("A1  LOGIN alice ****\r\n", Logged("A1  LOGIN alice \"s3 cret\"\r\n"));

    [TestMethod]
    public void Login_WithTwoSpacesAfterTheTagAndAPasswordLiteral_IsMasked() =>
        Assert.AreEqual(
            "A1  LOGIN alice {6}\r\n****\r\n",
            Logged("A1  LOGIN alice {6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void TagFollowedOnlyBySpaces_IsLoggedAsSent() =>
        Assert.AreEqual("A1  \r\n", Logged("A1  \r\n"));

    [TestMethod]
    public void Login_WithASpaceBeforeTheTagAndAnAtomPassword_IsMasked() =>
        Assert.AreEqual(" A1 LOGIN alice ****\r\n", Logged(" A1 LOGIN alice s3cret\r\n"));

    [TestMethod]
    public void Login_WithSeveralSpacesBeforeTheTagAndAQuotedPassword_IsMasked() =>
        Assert.AreEqual("   A1  LOGIN alice ****\r\n", Logged("   A1  LOGIN alice \"s3 cret\"\r\n"));

    [TestMethod]
    public void Login_WithASpaceBeforeTheTagAndAPasswordLiteral_IsMasked() =>
        Assert.AreEqual(
            " A1 LOGIN alice {6}\r\n****\r\n",
            Logged(" A1 LOGIN alice {6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithASpaceInPlaceOfTheTag_IsMasked() =>
        Assert.AreEqual(" LOGIN login ****\r\n", Logged(" LOGIN login s3cret\r\n"));

    [TestMethod]
    public void LineOfOnlySpaces_IsLoggedAsSent() =>
        Assert.AreEqual("   \r\n", Logged("   \r\n"));

    [TestMethod]
    public void Login_WithNoTagAndNoLeadingSpace_IsMasked() =>
        Assert.AreEqual("LOGIN alice ****\r\n", Logged("LOGIN alice s3cret\r\n"));

    [TestMethod]
    public void Login_WithNoTagAndAPasswordLiteral_IsMasked() =>
        Assert.AreEqual(
            "LOGIN alice {6}\r\n****\r\n",
            Logged("LOGIN alice {6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithTabsBetweenWords_IsMasked() =>
        Assert.AreEqual("A1\tLOGIN\talice\t****\r\n", Logged("A1\tLOGIN\talice\ts3cret\r\n"));

    [TestMethod]
    public void Login_WithNoTagAndTabsBetweenWords_IsMasked() =>
        Assert.AreEqual("\tLOGIN\talice\t****\r\n", Logged("\tLOGIN\talice\ts3cret\r\n"));

    [TestMethod]
    public void Login_WithTabsAndAPasswordLiteral_IsMasked() =>
        Assert.AreEqual(
            "A1\tLOGIN\talice\t{6}\r\n****\r\n",
            Logged("A1\tLOGIN\talice\t{6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithATabInsideThePassword_MasksAllOfIt() =>
        Assert.AreEqual("A1 LOGIN alice ****\r\n", Logged("A1 LOGIN alice s3\tcret\r\n"));

    [TestMethod]
    public void Login_WithATabInsideTheUser_WithholdsEverythingAfterTheMask() =>
        Assert.AreEqual("A1 LOGIN al\t****\r\n", Logged("A1 LOGIN al\tice s3cret\r\n"));

    [TestMethod]
    public void Login_WithATabInsideTheUserAndAPasswordLiteral_MasksTheLiteral() =>
        Assert.AreEqual("A1 LOGIN al\t****\r\n****\r\n", Logged("A1 LOGIN al\tice {6+}\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithATagOfLogin_MasksBothArguments() =>
        Assert.AreEqual("LOGIN LOGIN **** ****\r\n", Logged("LOGIN LOGIN alice s3cret\r\n"));

    [TestMethod]
    public void Login_WithATagOfLoginAndAUserLiteralAndAPasswordLiteral_MasksBothLiterals() =>
        Assert.AreEqual(
            "LOGIN LOGIN {5}\r\n**** {6}\r\n****\r\n",
            Logged("LOGIN LOGIN {5}\r\n", "<+ Ready for literal data\r\n", "alice {6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithNoTagAndAUserNamedLogin_MasksThePassword() =>
        Assert.AreEqual("LOGIN login ****\r\n", Logged("LOGIN login s3cret\r\n"));

    [TestMethod]
    public void Login_WithNoTagAUserNamedLoginAndAPasswordLiteral_MasksThePassword() =>
        Assert.AreEqual(
            "LOGIN login {6}\r\n****\r\n",
            Logged("LOGIN login {6}\r\n", "<+ Ready for literal data\r\n", "s3cret\r\n"));

    [TestMethod]
    public void Login_WithOnlyAUserThenAnotherCommand_LogsTheOtherCommandUnchanged() =>
        Assert.AreEqual("A1 LOGIN alice\r\nA2 NOOP\r\n", Logged("A1 LOGIN alice\r\n", "A2 NOOP\r\n"));

    [TestMethod]
    public void Authenticate_WithAnInitialResponse_IsMasked() =>
        Assert.AreEqual("A2 AUTHENTICATE PLAIN ****\r\n", Logged("A2 AUTHENTICATE PLAIN AGFsaWNlAHMzY3JldA==\r\n"));

    [TestMethod]
    public void Authenticate_WithContinuations_MasksEveryAnswer() =>
        Assert.AreEqual(
            "A2 AUTHENTICATE LOGIN\r\n****\r\n****\r\n",
            Logged("A2 AUTHENTICATE LOGIN\r\n", "<+ VXNlcm5hbWU6\r\n", "YWxpY2U=\r\n", "<+ UGFzc3dvcmQ6\r\n", "czNjcmV0\r\n"));

    [TestMethod]
    public void Authenticate_CancelledWithAStar_IsLoggedAsSent() =>
        Assert.AreEqual("A2 AUTHENTICATE LOGIN\r\n*\r\n", Logged("A2 AUTHENTICATE LOGIN\r\n", "<+ VXNlcm5hbWU6\r\n", "*\r\n"));

    [TestMethod]
    public void AppendLiteral_HoldingLoginText_IsLoggedUnchanged() =>
        Assert.AreEqual(
            "A3 APPEND INBOX {25}\r\nSubject: x\r\n\r\nLOGIN a b\r\n\r\nA4 NOOP\r\n",
            Logged(
                "A3 APPEND INBOX {25}\r\n",
                "<+ Ready for literal data\r\n",
                "Subject: x\r\n\r\nLOGIN a b\r\n",
                "\r\n",
                "A4 NOOP\r\n"));

    [TestMethod]
    public void AppendLiteral_HoldingAnAuthenticateLineAfterAContinuation_IsLoggedUnchanged() =>
        Assert.AreEqual(
            "A3 APPEND INBOX {26}\r\nA9 AUTHENTICATE PLAIN eA==\r\n",
            Logged("A3 APPEND INBOX {26}\r\n", "<+ Ready for literal data\r\n", "A9 AUTHENTICATE PLAIN eA==\r\n"));

    [TestMethod]
    public void OtherCommands_AreLoggedUnchanged() =>
        Assert.AreEqual("A1 SELECT INBOX\r\nNOOP\r\n", Logged("A1 SELECT INBOX\r\n", "NOOP\r\n"));

    [TestMethod]
    public void ObserveSent_WithNoPendingLiteralOrContinuation_ChangesNothing() =>
        Assert.AreEqual("A1 LOGIN alice ****\r\n", Logged("<* OK ready\r\n", "A1 LOGIN alice s3cret\r\n"));

    [TestMethod]
    public void Redact_WithNoOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapLineRedactionRule().Redact("A1 NOOP"u8, null!));
}
