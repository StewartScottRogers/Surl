using System.Text;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// BL-877 and ADR-0080 decision 2: <see cref="Pop3UpstreamTestCaseServer"/>, Surl's own POP3
/// server playing one upstream test case - the case's greeting, its <c>REPLY</c>'d lines, its
/// <c>CAPA</c> and <c>AUTH</c>, and its data as the maildrop - over an in-memory connection.
/// </summary>
[TestClass]
public sealed class Pop3UpstreamTestCaseServerTests
{
    private const string CaseMessage = "From: me\r\n\r\nbody\r\n";

    private const string SecondMessage = "From: you\r\n\r\nsecond\r\n";

    private const string Listing = "1 100\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullReplies_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3UpstreamTestCaseServer(null!, new AnonymousAuthenticationPolicy()));
    }

    [TestMethod]
    public void Constructor_NullAuthenticationPolicy_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3UpstreamTestCaseServer(Case([]), null!));
    }

    [TestMethod]
    public void Schemes_IsPop3()
    {
        CollectionAssert.AreEqual(new[] { "pop3" }, new Pop3UpstreamTestCaseServer(Case([]), new AnonymousAuthenticationPolicy()).Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ReplyWelcome_ReplacesTheGreetingOnTheFirstConnectionOnly()
    {
        var replies = Case(["REPLY welcome +OK case greeting <1972.987654321@curl>"]);

        var first = await ServeAsync(replies, "QUIT\r\n");
        var second = await ServeAsync(replies, "QUIT\r\n");

        Assert.AreEqual("+OK case greeting <1972.987654321@curl>\r\n+OK surl signing off\r\n", Utf8(first.WrittenBytes));
        Assert.AreEqual(Greeting + "+OK surl signing off\r\n", Utf8(second.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ReplyDCommand_IsAnsweredWithTheCasesTextAndNotRun()
    {
        var connection = await ServeAsync(Case(["REPLY USER -ERR case refuses"]), "USER u\r\nPASS p\r\n");

        Assert.AreEqual("-ERR case refuses\r\n+OK Logged in\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_CountLimitedReply_IsSpentThenTheServerAnswers()
    {
        var connection = await ServeAsync(Case(["REPLY NOOP -ERR busy", "COUNT NOOP 1"]), "NOOP\r\nNOOP\r\n");

        Assert.AreEqual("-ERR busy\r\n+OK\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_CapaWithCapabilitiesAndAuth_ListsThemButApopThenTheSaslMechanisms()
    {
        var connection = await ServeAsync(Case(["CAPA TOP APOP UIDL", "AUTH PLAIN LOGIN"]), "CAPA\r\n");

        Assert.AreEqual("+OK Capability list follows\r\nTOP\r\nUIDL\r\nSASL PLAIN LOGIN\r\n.\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_CapaWithCapabilitiesOnly_HasNoSaslLine()
    {
        var connection = await ServeAsync(Case(["CAPA TOP"]), "CAPA\r\n");

        Assert.AreEqual("+OK Capability list follows\r\nTOP\r\n.\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_CapaWithoutCapabilitiesOrAuth_IsNotACommand()
    {
        var connection = await ServeAsync(Case([]), "CAPA\r\n");

        Assert.AreEqual("-ERR Command not recognized\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_CapaWithAnArgument_IsInvalidArguments()
    {
        var connection = await ServeAsync(Case(["CAPA TOP"]), "CAPA x\r\n");

        Assert.AreEqual("-ERR Invalid arguments\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_UserAndPass_AcceptAnyLogin()
    {
        var connection = await ServeAsync(Case([]), "USER anyone\r\nPASS anything\r\n");

        Assert.AreEqual("+OK User accepted\r\n+OK Logged in\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_UserAndPassWithoutAnArgument_AreInvalidArguments()
    {
        var connection = await ServeAsync(Case([]), "USER\r\nPASS\r\n");

        Assert.AreEqual("-ERR Invalid arguments\r\n-ERR Invalid arguments\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_ApopOfferedByTheCase_AcceptsAnyLogin()
    {
        var connection = await ServeAsync(Case(["CAPA APOP"]), "APOP anyone 7501e9fa2c4c0d1e3a5b7c9d0e1f2a3b\r\nAPOP anyone\r\n");

        Assert.AreEqual("+OK Logged in\r\n-ERR Invalid arguments\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_ApopNotOfferedByTheCase_IsNotACommand()
    {
        var connection = await ServeAsync(Case([]), "APOP anyone 7501e9fa2c4c0d1e3a5b7c9d0e1f2a3b\r\n");

        Assert.AreEqual("-ERR Command not recognized\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_AuthNotReplyD_GoesToTheAuthenticationPolicyPassedIn()
    {
        var connection = await ServeAsync(Case(["AUTH PLAIN"]), "AUTH PLAIN AHUAcA==\r\n");

        Assert.AreEqual("+OK Authentication successful\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_Retr_SendsTheCasesDataWithoutLoginThenTheEnd()
    {
        var connection = await ServeAsync(Case([], (CaseMessage, null)), "RETR 1\r\n");

        Assert.AreEqual($"+OK {CaseMessage.Length} octets\r\n{CaseMessage}.\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_RetrOverTenThousand_SendsTheDataNumPart()
    {
        var connection = await ServeAsync(Case([], (CaseMessage, SecondMessage)), "RETR 10002\r\n");

        Assert.AreEqual($"+OK {SecondMessage.Length} octets\r\n{SecondMessage}.\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_RetrWithNoData_IsNoSuchMessage()
    {
        var connection = await ServeAsync(Case([]), "RETR 1\r\n");

        Assert.AreEqual("-ERR No such message\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_Top_SendsTheDataTheMessageNumberPicks()
    {
        var connection = await ServeAsync(Case([], (CaseMessage, SecondMessage)), "TOP 1 0\r\nTOP 10002 0\r\nTOP 1\r\n");

        Assert.AreEqual(
            $"+OK Top of message follows\r\n{CaseMessage}.\r\n+OK Top of message follows\r\n{SecondMessage}.\r\n-ERR Invalid arguments\r\n",
            RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_List_SendsTheCasesDataAsTheListing()
    {
        var connection = await ServeAsync(Case([], (Listing, null)), "LIST\r\n");

        Assert.AreEqual($"+OK 1 messages ({Listing.Length} octets)\r\n{Listing}.\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_ListOneMessage_AnswersOneLineWithItsSize()
    {
        var connection = await ServeAsync(Case([], (CaseMessage, null)), "LIST 1\r\nLIST 1 2\r\n");

        Assert.AreEqual($"+OK 1 {CaseMessage.Length}\r\n-ERR Invalid arguments\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_Uidl_SendsTheCasesDataAsTheListing()
    {
        var connection = await ServeAsync(Case([], (Listing, null)), "UIDL\r\n");

        Assert.AreEqual($"+OK Unique-ID listing follows\r\n{Listing}.\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_StatAndRset_CountTheCasesOneMessage()
    {
        var connection = await ServeAsync(Case([], (CaseMessage, null)), "STAT\r\nRSET\r\n");

        Assert.AreEqual(
            $"+OK 1 {CaseMessage.Length}\r\n+OK Maildrop has 1 messages ({CaseMessage.Length} octets)\r\n",
            RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_DeleAndNoop_AnswerForTheCasesOneMessage()
    {
        var connection = await ServeAsync(Case([], (CaseMessage, null)), "DELE 1\r\nNOOP\r\nSTAT\r\n");

        Assert.AreEqual($"+OK Message deleted\r\n+OK\r\n+OK 1 {CaseMessage.Length}\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    [DataRow("STAT 1", DisplayName = "Too many words")]
    [DataRow("NOOP  ", DisplayName = "An empty word")]
    [DataRow("RETR é", DisplayName = "A byte outside printable ASCII")]
    public async Task ServeAsync_MaildropCommandWithWrongArguments_IsInvalidArguments(string line)
    {
        var request = new InMemoryConnection([new ReadOnlyMemory<byte>([.. Encoding.Latin1.GetBytes(line), .. "\r\n"u8])]);

        await Server(Case([], (CaseMessage, null))).ServeAsync(request, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual("-ERR Invalid arguments\r\n", RepliesAfterGreeting(request));
    }

    [TestMethod]
    public async Task ReceivedBytes_IsEveryCommandLineReadWithCrLf()
    {
        var replies = Case(["REPLY NOOP +OK case"]);

        await ServeAsync(replies, "CAPA\r\nNOOP\r\nRETR 1\r\nQUIT\r\n");

        Assert.AreEqual("CAPA\r\nNOOP\r\nRETR 1\r\nQUIT\r\n", Encoding.Latin1.GetString(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task Pop3ProtocolServer_WithoutACase_GreetsAndAnswersCapaWithItsOwnText()
    {
        var clock = new ManualTimeProvider();

        var connection = await Pop3TestExchange.ServeAsync(AccountStore(clock), "CAPA\r\nRETR 1\r\n", clock, TestContext.CancellationToken);

        StringAssert.StartsWith(RepliesAfterGreeting(connection), "+OK Capability list follows\r\nTOP\r\nUIDL\r\n");
        StringAssert.EndsWith(RepliesAfterGreeting(connection), ".\r\n-ERR [AUTH] Authentication required\r\n");
    }

    private static MailUpstreamTestCaseReplies Case(string[] serverCommands, (string Data, string? Data2)? parts = null)
    {
        var dictionary = new Dictionary<string, byte[]>();
        if (parts is { } given)
        {
            dictionary["data"] = Encoding.Latin1.GetBytes(given.Data);
            if (given.Data2 is not null)
            {
                dictionary["data2"] = Encoding.Latin1.GetBytes(given.Data2);
            }
        }

        return new MailUpstreamTestCaseReplies(dictionary, serverCommands);
    }

    private static Pop3UpstreamTestCaseServer Server(MailUpstreamTestCaseReplies replies) =>
        new(replies, new AnonymousAuthenticationPolicy());

    private async Task<InMemoryConnection> ServeAsync(MailUpstreamTestCaseReplies replies, string request)
    {
        var connection = new InMemoryConnection(Ascii(request));
        await Server(replies).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));
        return connection;
    }
}
