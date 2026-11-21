using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 Amendment 1, decisions 3 to 5: <c>SPECIAL-USE</c> offered only while the store holds a
/// use, the use after the children attribute of <c>LIST</c> and <c>LSUB</c>, and <c>CREATE</c>'s
/// <c>(USE (...))</c>, with the four BL-1485 measurements of pinned upstream curl replayed.
/// </summary>
[TestClass]
public sealed class ImapSpecialUseTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string LoggedIn = "a OK LOGIN completed\r\n";
    private const string UseRefused = "NO [USEATTR] The use cannot be given to this mailbox";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_NoUseHeld_CapabilitiesAreUnchangedAndCreateWithAUseIsRefused()
    {
        var responses = await ResponsesAsync(Login + "b CAPABILITY\r\nc CREATE x (USE (\\Sent))\r\n", TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + $"* CAPABILITY {Capabilities.Replace("SASL-IR ", string.Empty, StringComparison.Ordinal)}\r\nb OK CAPABILITY completed\r\nc {UseRefused}\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_AUseHeld_GreetingAndCapabilityOfferSpecialUseAfterMove()
    {
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);

        var connection = await ServeAsync(Server(store), "b CAPABILITY\r\n", clock, TestContext.CancellationToken);

        var offered = Capabilities.Replace("MOVE ", "MOVE SPECIAL-USE CREATE-SPECIAL-USE ", StringComparison.Ordinal);
        Assert.StartsWith($"* OK [CAPABILITY {offered}] surl ready\r\n* CAPABILITY {offered}\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ListAndLsub_MarkOnlyMailboxesThatHoldAUse()
    {
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);
        Create(store, string.Empty, "Plain/Child");
        var connection = await ServeAsync(Server(store), Login + "b LIST \"\" *\r\nc LSUB \"\" Sent\r\n", clock, TestContext.CancellationToken);

        var written = Utf8(connection.WrittenBytes);
        Assert.Contains("* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIST (\\HasNoChildren \\Drafts) \"/\" Drafts\r\n", written);
        Assert.Contains("* LIST (\\Noselect \\HasChildren) \"/\" Plain\r\n", written);
        Assert.Contains("* LIST (\\HasNoChildren \\Sent) \"/\" Sent\r\n", written);
        Assert.Contains("* LSUB (\\HasNoChildren \\Sent) \"/\" Sent\r\nc OK LSUB completed\r\n", written);
    }

    [TestMethod]
    [DataRow("special-use-list")]
    [DataRow("special-use-append-sent")]
    public async Task ServeAsync_RecordedSpecialUseRequest_WritesTheRecordedResponses(string caseName)
    {
        Assert.AreEqual("0", Encoding.UTF8.GetString(RecordedFixtureTests.ReadBytes(caseName, "exitcode.txt")).Trim());
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);
        var connection = new InMemoryConnection([RecordedFixtureTests.ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        // Three differences from the recording, none in what curl reads: the recorder served SPECIAL-USE
        // alone (Surl also offers CREATE-SPECIAL-USE), listed Sent, Drafts, Trash where Surl lists
        // ordinally (ADR-0055 decision 6), and issued UIDVALIDITYs in its own order.
        var expected = RecordedFixtureTests.RecordedResponses(caseName)
            .Replace("SPECIAL-USE", "SPECIAL-USE CREATE-SPECIAL-USE", StringComparison.Ordinal)
            .Replace("APPENDUID 1790668800", "APPENDUID 1790668801", StringComparison.Ordinal);
        Assert.AreEqual(OrderListLines(expected), OrderListLines(Utf8(connection.WrittenBytes)));
        Assert.IsTrue(connection.WritesCompleted);
    }

    // LIST-EXTENDED stays unadvertised, so both forms are answered as any unimplemented LIST form.
    [TestMethod]
    [DataRow("special-use-list-return")]
    [DataRow("special-use-list-select")]
    public async Task ServeAsync_RecordedExtendedListRequest_AnswersTheFormAsInvalidAndCarriesOn(string caseName)
    {
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);
        var connection = new InMemoryConnection([RecordedFixtureTests.ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        var written = Utf8(connection.WrittenBytes);
        Assert.Contains("A003 BAD Invalid arguments\r\n", written);
        Assert.EndsWith("A004 OK LOGOUT completed\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_CreateWithAUse_SucceedsAndTheMailboxListsWithIt()
    {
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);
        var connection = await ServeAsync(Server(store), Login + "b CREATE Mine (USE (\\Archive))\r\nc CREATE Empty (USE ())\r\nd LIST \"\" Mine\r\n", clock, TestContext.CancellationToken);

        Assert.Contains("b OK CREATE completed\r\nc OK CREATE completed\r\n* LIST (\\HasNoChildren \\Archive) \"/\" Mine\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("CREATE Again (USE (\\Sent))")]
    [DataRow("CREATE Odd (USE (\\Important))")]
    [DataRow("CREATE Odd (USE (\\Archive \\Flagged))")]
    [DataRow("CREATE Odd (USE (\\sent))")]
    public async Task ServeAsync_CreateWithAUseTakenUnknownOrTooMany_AnswersUseAttribute(string command)
    {
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);
        var connection = await ServeAsync(Server(store), Login + $"b {command}\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith($"b {UseRefused}\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("CREATE Odd (USE (\\Archive)", "BAD Invalid arguments")]
    [DataRow("CREATE Odd (USE (\\Archive))x", "BAD Invalid arguments")]
    [DataRow("CREATE Odd (NOUSE (\\Archive))", "BAD Invalid arguments")]
    [DataRow("CREATE Odd (USE \\Archive)", "BAD Invalid arguments")]
    [DataRow("CREATE Odd (USE (Archive))", "BAD Invalid arguments")]
    [DataRow("CREATE Odd (USE (\\Archive  \\Sent))", "BAD Invalid arguments")]
    [DataRow("CREATE Odd (USE (\\))", "BAD Invalid arguments")]
    [DataRow("CREATE Odd junk", "BAD Invalid arguments")]
    [DataRow("CREATE", "BAD Invalid arguments")]
    [DataRow("CREATE \"a&\" (USE (\\Archive))", "NO [CANNOT] Invalid mailbox name")]
    public async Task ServeAsync_CreateWithMalformedParameters_IsRefused(string command, string answer)
    {
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);
        var connection = await ServeAsync(Server(store), Login + $"b {command}\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith($"b {answer}\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_RenameAndDelete_CarryAndEndTheUse()
    {
        var clock = new ManualTimeProvider();
        var store = StoreWithUses(clock);
        var connection = await ServeAsync(
            Server(store),
            Login + "b RENAME Sent Outbox\r\nc LIST \"\" Outbox\r\nd DELETE Outbox\r\ne CREATE Again (USE (\\Sent))\r\n",
            clock,
            TestContext.CancellationToken);

        var written = Utf8(connection.WrittenBytes);
        Assert.Contains("* LIST (\\HasNoChildren \\Sent) \"/\" Outbox\r\n", written);
        Assert.Contains("d OK DELETE completed\r\ne OK CREATE completed\r\n", written);
    }

    private static string OrderListLines(string written) =>
        string.Concat(written.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).Select(line => line + "\r\n"));

    private static MailboxStore StoreWithUses(TimeProvider clock)
    {
        var store = AnonymousStore(clock);
        var view = store.ViewFor(string.Empty);
        foreach (var (name, use) in new[] { ("Sent", @"\Sent"), ("Drafts", @"\Drafts"), ("Trash", @"\Trash") })
        {
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, name, use: use));
        }

        for (var number = 0; number < 2; number++)
        {
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, "Sent", Encoding.ASCII.GetBytes("Subject: s\r\n\r\nhello\r\n"), MailFlags.None, null, out _));
        }

        return store;
    }
}
