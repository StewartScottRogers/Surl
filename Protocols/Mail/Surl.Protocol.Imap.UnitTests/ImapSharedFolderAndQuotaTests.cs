using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0102 decisions 1.4 and 1.6 over IMAP: a shared folder listed, named in <c>NAMESPACE</c>,
/// selected and fetched by its members and hidden from everyone else, protected from
/// <c>DELETE</c>, <c>RENAME</c> and <c>CREATE</c> under it; and an <c>APPEND</c> or <c>COPY</c>
/// past a mailbox's quota answered <c>NO [OVERQUOTA]</c> with nothing stored. Rows R6 to R11 of
/// the ADR are what pinned upstream curl 8.21.0 sent and did with these answers.
/// </summary>
[TestClass]
public sealed class ImapSharedFolderAndQuotaTests
{
    private const string Ann = "ann@example.com";
    private const string Team = "Shared/team";
    private const string LoggedIn = "a OK LOGIN completed\r\n";
    private const string OverQuota = "NO [OVERQUOTA] Mailbox is over its quota";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_ListByAMember_ShowsSharedAsNoselectAndTheFolderAsCurlPrintsThem()
    {
        var responses = await ResponsesAsync("al", "b LIST \"\" *\r\n");

        Assert.AreEqual(
            LoggedIn
            + "* LIST (\\HasNoChildren) \"/\" INBOX\r\n"
            + "* LIST (\\Noselect \\HasChildren) \"/\" Shared\r\n"
            + "* LIST (\\HasNoChildren) \"/\" Shared/team\r\n"
            + "b OK LIST completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_ListByANonMember_ShowsNoSharedFolder()
    {
        var responses = await ResponsesAsync("cy", "b LIST \"\" *\r\n");

        Assert.AreEqual(LoggedIn + "* LIST (\\HasNoChildren) \"/\" INBOX\r\nb OK LIST completed\r\n", responses);
    }

    [TestMethod]
    [DataRow("al", "* NAMESPACE ((\"\" \"/\")) NIL ((\"Shared/\" \"/\"))\r\n")]
    [DataRow(Ann, "* NAMESPACE ((\"\" \"/\")) NIL ((\"Shared/\" \"/\"))\r\n")]
    [DataRow("cy", "* NAMESPACE ((\"\" \"/\")) NIL NIL\r\n")]
    public async Task ServeAsync_Namespace_NamesTheSharedNamespaceOnlyForAMember(string user, string expected)
    {
        var responses = await ResponsesAsync(user, "b NAMESPACE\r\n");

        Assert.AreEqual(LoggedIn + expected + "b OK NAMESPACE completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_SelectAndUidFetchByAMember_ReturnTheMessageAnotherMemberAppended()
    {
        var responses = await ResponsesAsync(Ann, "b SELECT Shared/team\r\nc UID FETCH 1 BODY[]\r\n", store =>
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor("al"), Team, "hello"u8, MailFlags.None, null, out _)));

        Assert.EndsWith("b OK [READ-WRITE] SELECT completed\r\n* 1 FETCH (UID 1 FLAGS (\\Seen) BODY[] {5}\r\nhello)\r\nc OK FETCH completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_StatusByAMember_NamesTheSharedFolderAsListed()
    {
        var responses = await ResponsesAsync("al", "b STATUS Shared/team (MESSAGES)\r\n");

        Assert.AreEqual(LoggedIn + "* STATUS Shared/team (MESSAGES 0)\r\nb OK STATUS completed\r\n", responses);
    }

    [TestMethod]
    [DataRow("b SELECT Shared/team\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b EXAMINE Shared/team\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b STATUS Shared/team (MESSAGES)\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b APPEND Shared/team {5}\r\n", "b NO [TRYCREATE] Mailbox does not exist\r\n")]
    public async Task ServeAsync_SharedFolderToANonMember_GetsTheMissingMailboxAnswers(string command, string expected)
    {
        var responses = await ResponsesAsync("cy", command);

        Assert.AreEqual(LoggedIn + expected, responses);
    }

    [TestMethod]
    [DataRow("b DELETE Shared/team\r\n", "b NO [NOPERM] A shared folder cannot be deleted\r\n")]
    [DataRow("b RENAME Shared/team Mine\r\n", "b NO [NOPERM] A shared folder cannot be renamed\r\n")]
    [DataRow("b CREATE Shared/more\r\n", "b NO [NOPERM] A shared folder cannot be created under\r\n")]
    public async Task ServeAsync_ChangeToASharedFolder_IsRefusedNoPerm(string command, string expected)
    {
        var (responses, store) = await ServeWithStoreAsync("al", command);

        Assert.AreEqual(LoggedIn + expected, responses);
        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor("al")).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AppendPastTheQuota_AnswersOverQuotaBeforeTheContinuationAndStoresNothing()
    {
        var log = new RecordingExchangeLog();

        var (responses, store) = await ServeWithStoreAsync(Ann, "b APPEND INBOX {11}\r\n", log: log);

        Assert.AreEqual(LoggedIn + $"b {OverQuota}\r\n", responses);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ReadMailbox(store.ViewFor(Ann), "INBOX", out var snapshot));
        Assert.IsEmpty(snapshot!.Messages);
        Assert.Contains("APPEND refused: 11 bytes would pass the mailbox's quota", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithinTheQuota_IsStored()
    {
        var (responses, _) = await ServeWithStoreAsync(Ann, "b APPEND INBOX {10}\r\n1234567890\r\n");

        Assert.AreEqual(LoggedIn + "+ Ready for literal data\r\n" + $"b OK [APPENDUID {UidValidity + 2} 1] APPEND completed\r\n", responses);
    }

    [TestMethod]
    [DataRow("c COPY 1 Archive\r\n")]
    [DataRow("c UID COPY 1 Archive\r\n")]
    public async Task ServeAsync_CopyPastTheQuota_AnswersOverQuotaAndStoresNothing(string command)
    {
        var (responses, store) = await ServeWithStoreAsync(Ann, "b SELECT INBOX\r\n" + command, AppendSixBytesToAnn);

        Assert.EndsWith($"c {OverQuota}\r\n", responses);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ReadMailbox(store.ViewFor(Ann), "Archive", out var snapshot));
        Assert.IsEmpty(snapshot!.Messages);
    }

    [TestMethod]
    public async Task ServeAsync_CopyIntoASharedFolder_CountsAgainstNoQuota()
    {
        var (responses, store) = await ServeWithStoreAsync(Ann, "b SELECT INBOX\r\nc COPY 1 Shared/team\r\n", AppendSixBytesToAnn);

        Assert.EndsWith($"c OK [COPYUID {UidValidity + 4} 1 1] COPY completed\r\n", responses);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ReadMailbox(store.ViewFor("al"), Team, out var snapshot));
        Assert.HasCount(1, snapshot!.Messages);
    }

    private static void AppendSixBytesToAnn(MailboxStore store) =>
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor(Ann), "INBOX", "123456"u8, MailFlags.None, null, out _));

    private async Task<string> ResponsesAsync(string user, string request, Action<MailboxStore>? fill = null) =>
        (await ServeWithStoreAsync(user, request, fill)).Responses;

    private async Task<(string Responses, MailboxStore Store)> ServeWithStoreAsync(
        string user,
        string request,
        Action<MailboxStore>? fill = null,
        RecordingExchangeLog? log = null)
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore(["al", "cy"], allowAnonymous: false, clock, mailDomains: Configuration());
        fill?.Invoke(store);
        var connection = await ServeAsync(Server(store), $"a LOGIN {user} p\r\n" + request, clock, TestContext.CancellationToken, log: log);
        return (AfterGreeting(connection), store);
    }

    // ann@example.com is a mailbox with a 10-byte quota and an Archive folder; al and ann are
    // members of the shared folder team, cy is not.
    private static MailDomainConfiguration Configuration() =>
        new(
            ["example.com"],
            [Ann],
            [],
            [],
            [new MailboxSettings(Ann, 10, ["Archive"])],
            [new MailSharedFolder("team", ["al", Ann])]);
}
