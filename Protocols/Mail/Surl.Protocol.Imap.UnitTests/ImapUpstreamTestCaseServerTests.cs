using System.Text;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// The IMAP server half of an upstream test case (ADR-0080 decision 2): Surl's own server with
/// the case's greeting, <c>REPLY</c>'d answers, capabilities, data and <c>POSTFETCH</c> text, and a
/// record of every command line and <c>APPEND</c> message - driven over an in-memory connection.
/// </summary>
[TestClass]
public sealed class ImapUpstreamTestCaseServerTests
{
    private const string PlainGreeting = "* OK surl ready\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullReplies_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapUpstreamTestCaseServer(null!, new AnonymousAuthenticationPolicy()));
    }

    [TestMethod]
    public void Constructor_NullAuthenticationPolicy_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapUpstreamTestCaseServer(Replies([]), null!));
    }

    [TestMethod]
    public void Schemes_IsImap()
    {
        CollectionAssert.AreEqual(new[] { "imap" }, new ImapUpstreamTestCaseServer(Replies([]), new AnonymousAuthenticationPolicy()).Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NoReplyWelcome_GreetsWithNoCapabilities()
    {
        var written = await ServeCaseAsync(Replies([]), "a LOGOUT\r\n");

        Assert.AreEqual(PlainGreeting + "* BYE surl logging out\r\na OK LOGOUT completed\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyWelcome_ReplacesTheGreeting()
    {
        var written = await ServeCaseAsync(Replies(["REPLY welcome * OK Welcome to the case"]), "a LOGOUT\r\n");

        Assert.StartsWith("* OK Welcome to the case\r\n* BYE", written);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyedCommand_IsAnsweredWithTheCasesTextAndNotRun()
    {
        var replies = Replies(["REPLY SELECT A002 NO no such mailbox"]);

        var written = await ServeCaseAsync(replies, "A002 SELECT INBOX\r\nA003 LOGOUT\r\n");

        Assert.AreEqual(PlainGreeting + "A002 NO no such mailbox\r\n* BYE surl logging out\r\nA003 OK LOGOUT completed\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_CountLimitedReply_IsAnsweredByTheCaseOnlyThatManyTimes()
    {
        var replies = Replies(["REPLY NOOP A NO busy", "COUNT NOOP 1"]);

        var written = await ServeCaseAsync(replies, "a NOOP\r\nb NOOP\r\n");

        Assert.AreEqual(PlainGreeting + "A NO busy\r\nb OK NOOP completed\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_TagAlone_IsMatchedOnTheWholeLineAndAnsweredByTheServerWhenNotReplyed()
    {
        var replies = Replies([]);

        var written = await ServeCaseAsync(replies, "a\r\n");

        StringAssert.StartsWith(written, PlainGreeting + "a BAD");
        Assert.AreEqual("a\r\n", Text(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_Capability_ListsTheCasesCapaAndAuthMechanisms()
    {
        var written = await ServeCaseAsync(Replies(["CAPA IDLE ID", "AUTH PLAIN LOGIN"]), "a CAPABILITY\r\n");

        Assert.AreEqual(PlainGreeting + "* CAPABILITY IMAP4rev1 IDLE ID AUTH=PLAIN AUTH=LOGIN\r\na OK CAPABILITY completed\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_CapabilityWithArguments_IsInvalid()
    {
        var written = await ServeCaseAsync(Replies(["CAPA IDLE"]), "a CAPABILITY x\r\n");

        Assert.AreEqual(PlainGreeting + "a BAD Invalid arguments\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_CapabilityWithNoCapaOrAuth_IsNotACommand()
    {
        var written = await ServeCaseAsync(Replies([]), "a CAPABILITY\r\n");

        Assert.AreEqual(PlainGreeting + "a BAD Command not recognized\r\n", written);
    }

    [TestMethod]
    [DataRow("a LOGIN user secret\r\n", "a OK LOGIN completed\r\n", DisplayName = "Anyone logs in")]
    [DataRow("a LOGIN user\r\n", "a BAD Invalid arguments\r\n", DisplayName = "A login with no password is invalid")]
    [DataRow("a LOGIN user secret x\r\n", "a BAD Invalid arguments\r\n", DisplayName = "A login with a third argument is invalid")]
    public async Task ServeAsync_Login_AcceptsAnyoneWithAUserAndPassword(string request, string expected)
    {
        var written = await ServeCaseAsync(Replies([]), request);

        Assert.AreEqual(PlainGreeting + expected, written);
    }

    [TestMethod]
    [DataRow("a SELECT\r\n", DisplayName = "No mailbox")]
    [DataRow("a SELECT INBOX x\r\n", DisplayName = "A second argument")]
    public async Task ServeAsync_SelectWithoutOneMailbox_IsInvalid(string request)
    {
        var written = await ServeCaseAsync(Replies([]), request);

        Assert.AreEqual(PlainGreeting + "a BAD Invalid arguments\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_SelectThenFetch_SendsTheCasesDataWithPostFetchBeforeTheClosingParenthesis()
    {
        var replies = Replies(["POSTFETCH FLAGS (\\Seen)"], ("data", "hello\r\n"));

        var written = await ServeCaseAsync(replies, "a SELECT INBOX\r\nb FETCH 1 BODY[TEXT]\r\n");

        Assert.AreEqual(
            PlainGreeting
            + "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n* 1 EXISTS\r\n* 0 RECENT\r\n* OK [UIDVALIDITY 1] UIDs valid\r\n* OK [UIDNEXT 2] Predicted next UID\r\n"
            + "a OK [READ-WRITE] SELECT completed\r\n"
            + "* 1 FETCH (BODY[TEXT] {7}\r\nhello\r\nFLAGS (\\Seen))\r\n"
            + "b OK FETCH completed\r\n",
            written);
    }

    [TestMethod]
    public async Task ServeAsync_FetchFromAMailboxNumberedOver10000_SendsTheDataNumPart()
    {
        var replies = Replies([], ("data", "plain"), ("data2", "second"));

        var written = await ServeCaseAsync(replies, "a SELECT 10002\r\nb FETCH 1 BODY[]\r\n");

        StringAssert.Contains(written, "* 1 FETCH (BODY[] {6}\r\nsecond)\r\nb OK FETCH completed\r\n");
    }

    // Case 841: the number the client asked for is echoed.
    [TestMethod]
    public async Task ServeAsync_CustomFetchOfMessage123_EchoesTheNumberAndItem()
    {
        var replies = Replies([], ("data", "hi"));

        var written = await ServeCaseAsync(replies, "a SELECT INBOX\r\nb FETCH 123 BODY[1]\r\n");

        StringAssert.Contains(written, "* 123 FETCH (BODY[1] {2}\r\nhi)\r\nb OK FETCH completed\r\n");
    }

    // Case 3206: the item list is echoed as sent, its parentheses and quoted string included.
    [TestMethod]
    public async Task ServeAsync_CustomFetchWithAnItemList_EchoesTheListAsSent()
    {
        var replies = Replies([], ("data", "hi"));

        var written = await ServeCaseAsync(replies, "a SELECT INBOX\r\nb FETCH 456 (\"fake {50}\" BODY[TEXT])\r\n");

        StringAssert.Contains(written, "* 456 FETCH ((\"fake {50}\" BODY[TEXT]) {2}\r\nhi)\r\nb OK FETCH completed\r\n");
    }

    // Case 1847: upstream's server takes UID as the command, so FETCH lands in the number's place.
    [TestMethod]
    public async Task ServeAsync_UidFetch_PutsFetchWhereTheNumberGoes()
    {
        var replies = Replies([], ("data", "hi"));

        var written = await ServeCaseAsync(replies, "a SELECT INBOX\r\nb UID FETCH 1 BODY[]\r\n");

        StringAssert.Contains(written, "* FETCH FETCH (1 BODY[] {2}\r\nhi)\r\nb OK UID FETCH completed\r\n");
    }

    [TestMethod]
    [DataRow("a FETCH\r\n", DisplayName = "No arguments")]
    [DataRow("a FETCH 1\r\n", DisplayName = "No item")]
    [DataRow("a UID\r\n", DisplayName = "UID alone")]
    [DataRow("a UID NOOP\r\n", DisplayName = "UID of a command it has no form of")]
    [DataRow("a UID \r\n", DisplayName = "UID and a space")]
    [DataRow("a STORE\r\n", DisplayName = "STORE alone")]
    [DataRow("a STORE 1 FLAGS (\\Seen)\r\n", DisplayName = "STORE that does not add")]
    [DataRow("a STORE 1\r\n", DisplayName = "STORE with no flags")]
    [DataRow("a LIST\r\n", DisplayName = "LIST alone")]
    [DataRow("a SEARCH\r\n", DisplayName = "SEARCH alone")]
    public async Task ServeAsync_CaseCommandWithoutItsArguments_IsInvalid(string request)
    {
        var written = await ServeCaseAsync(Replies([]), request);

        Assert.AreEqual(PlainGreeting + "a BAD Invalid arguments\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_List_SendsTheCasesListing()
    {
        var replies = Replies([], ("data", "* LIST () \"/\" /blurdybloop\r\n* LIST (\\Noselect) \"/\" /foo\r\n"));

        var written = await ServeCaseAsync(replies, "a LIST \"\" *\r\n");

        Assert.AreEqual(PlainGreeting + "* LIST () \"/\" /blurdybloop\r\n* LIST (\\Noselect) \"/\" /foo\r\na OK LIST completed\r\n", written);
    }

    [TestMethod]
    [DataRow("a LSUB \"\" *\r\n", "a OK LSUB completed\r\n", DisplayName = "LSUB")]
    [DataRow("a EXAMINE 10002\r\n", "a OK [READ-ONLY] EXAMINE completed\r\n", DisplayName = "EXAMINE")]
    [DataRow("a STATUS 10002 (MESSAGES)\r\n", "a OK STATUS completed\r\n", DisplayName = "STATUS")]
    public async Task ServeAsync_MailboxCommand_SendsTheDataItsFirstArgumentPicks(string request, string completion)
    {
        var replies = Replies([], ("data", "* plain\r\n"), ("data2", "* second\r\n"));

        var written = await ServeCaseAsync(replies, request);

        Assert.AreEqual(PlainGreeting + (request.Contains("10002", StringComparison.Ordinal) ? "* second\r\n" : "* plain\r\n") + completion, written);
    }

    [TestMethod]
    [DataRow("b SEARCH ALL\r\n", "b OK SEARCH completed\r\n", DisplayName = "SEARCH")]
    [DataRow("b UID SEARCH ALL\r\n", "b OK UID SEARCH completed\r\n", DisplayName = "UID SEARCH")]
    [DataRow("b UID STORE 1 +FLAGS (\\Deleted)\r\n", "b OK UID STORE completed\r\n", DisplayName = "UID STORE")]
    [DataRow("b UID COPY 1 Trash\r\n", "b OK UID COPY completed\r\n", DisplayName = "UID COPY")]
    public async Task ServeAsync_SelectedMailboxCommand_SendsTheSelectedMailboxesData(string request, string completion)
    {
        var replies = Replies([], ("data", "* plain\r\n"), ("data2", "* SEARCH 1 2\r\n"));

        var written = await ServeCaseAsync(replies, "a SELECT 10002\r\n" + request);

        StringAssert.EndsWith(written, "a OK [READ-WRITE] SELECT completed\r\n* SEARCH 1 2\r\n" + completion);
    }

    [TestMethod]
    public async Task ServeAsync_StoreAddingFlags_EchoesTheFlagsAdded()
    {
        var written = await ServeCaseAsync(Replies([]), "a STORE 1 +FLAGS (\\Deleted)\r\n");

        Assert.AreEqual(PlainGreeting + "* 1 FETCH (FLAGS (\\Deleted))\r\na OK STORE completed\r\n", written);
    }

    [TestMethod]
    [DataRow("CHECK")]
    [DataRow("CLOSE")]
    [DataRow("EXPUNGE")]
    [DataRow("UNSELECT")]
    [DataRow("CREATE")]
    [DataRow("DELETE")]
    [DataRow("RENAME")]
    [DataRow("SUBSCRIBE")]
    [DataRow("UNSUBSCRIBE")]
    [DataRow("COPY")]
    [DataRow("MOVE")]
    public async Task ServeAsync_MailboxChangingCommand_CompletesAndChangesNothing(string command)
    {
        var written = await ServeCaseAsync(Replies([]), $"a {command} x\r\n");

        Assert.AreEqual(PlainGreeting + $"a OK {command} completed\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_Append_RecordsTheMessageInUploadedBytesAndTheCommandLineInReceivedBytes()
    {
        var replies = Replies([]);

        var written = await ServeCaseAsync(replies, "a APPEND INBOX {7}\r\nhello\r\n\r\nb LOGOUT\r\n");

        Assert.AreEqual(PlainGreeting + "+ Ready for literal data\r\na OK APPEND completed\r\n* BYE surl logging out\r\nb OK LOGOUT completed\r\n", written);
        Assert.AreEqual("hello\r\n", Text(replies.UploadedBytes));
        Assert.AreEqual("a APPEND INBOX {7}\r\nb LOGOUT\r\n", Text(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_AppendClosedInItsMessage_EndsTheSessionAndUploadsNothing()
    {
        var replies = Replies([]);

        var written = await ServeCaseAsync(replies, "a APPEND INBOX {7}\r\nhel");

        Assert.AreEqual(PlainGreeting + "+ Ready for literal data\r\n", written);
        Assert.IsEmpty(replies.UploadedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_CommandWithALiteral_RecordsItsMarkerAndBytesInReceivedBytes()
    {
        var replies = Replies([]);

        await ServeCaseAsync(replies, "a LOGIN {4}\r\nuser secret\r\nb LOGOUT\r\n");

        Assert.AreEqual("a LOGIN {4}\r\nuser\r\n secret\r\nb LOGOUT\r\n", Text(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ReplyedSaslResponse_IsAnsweredWithTheCasesText()
    {
        var replies = Replies(["AUTH PLAIN", "REPLY AUTHENTICATE + ", "REPLY dXNlcgB1c2VyAHNlY3JldA== A001 OK AUTHENTICATE completed"]);

        var written = await ServeCaseAsync(replies, "A001 AUTHENTICATE PLAIN\r\ndXNlcgB1c2VyAHNlY3JldA==\r\n");

        Assert.AreEqual(PlainGreeting + "+ \r\nA001 OK AUTHENTICATE completed\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_NoCase_GreetsWithItsOwnCapabilityAndFetchesFromTheMailStore()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);

        var connection = await ServeAsync(Server(store), "a SELECT INBOX\r\nb FETCH 1 BODY[TEXT]\r\n", clock, TestContext.CancellationToken);

        StringAssert.Contains(AfterGreeting(connection), "* 1 FETCH (FLAGS (\\Seen) BODY[TEXT] {7}\r\nhello\r\n)\r\nb OK FETCH completed\r\n");
    }

    private static MailUpstreamTestCaseReplies Replies(string[] serverCommands, params (string Name, string Text)[] parts) =>
        new(parts.ToDictionary(part => part.Name, part => Encoding.Latin1.GetBytes(part.Text)), serverCommands);

    private static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private async Task<string> ServeCaseAsync(MailUpstreamTestCaseReplies replies, string request)
    {
        var server = new ImapUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy());
        var connection = new InMemoryConnection(Bytes(request));
        await server.ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));
        return Text(connection.WrittenBytes);
    }
}
