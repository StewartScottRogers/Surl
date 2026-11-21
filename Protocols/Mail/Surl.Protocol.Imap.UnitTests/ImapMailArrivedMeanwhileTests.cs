using Surl.MailStore;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 5: mail delivered to the selected mailbox while a session is open - by SMTP
/// delivery or another session's <c>APPEND</c> - is reported as an untagged <c>EXISTS</c> before the
/// tagged answer of the session's next updating command.
/// </summary>
[TestClass]
public sealed class ImapMailArrivedMeanwhileTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string Select = "b SELECT INBOX\r\n";
    private const string Selected = "b OK [READ-WRITE] SELECT completed\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_MailDeliveredMeanwhile_NoopReportsItAsExists()
    {
        var store = AnonymousStore(new ManualTimeProvider());
        Deliver(store, string.Empty, 1);

        var responses = await ServeDuringAsync(store, "c NOOP\r\n", () => DeliverBySmtpAsync(store));

        Assert.AreEqual("* 2 EXISTS\r\nc OK NOOP completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_MailDeliveredMeanwhileToAnEmptyInbox_NoopReportsItAsExists()
    {
        var store = AnonymousStore(new ManualTimeProvider());

        var responses = await ServeDuringAsync(store, "c NOOP\r\n", () => DeliverBySmtpAsync(store));

        Assert.AreEqual("* 1 EXISTS\r\nc OK NOOP completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_MailAppendedMeanwhileByAnotherSession_NoopReportsItAsExists()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);

        var responses = await ServeDuringAsync(store, "c NOOP\r\n", async () =>
        {
            var other = await ServeAsync(Server(store), Login + "x APPEND INBOX {5}\r\nhello\r\n", clock, TestContext.CancellationToken);
            StringAssert.Contains(AfterGreeting(other), "x OK [APPENDUID");
        });

        Assert.AreEqual("* 2 EXISTS\r\nc OK NOOP completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_MailAppendedMeanwhileToAnEmptyInbox_NoopReportsItAsExists()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        var responses = await ServeDuringAsync(store, "c NOOP\r\n", async () =>
            await ServeAsync(Server(store), Login + "x APPEND INBOX {5}\r\nhello\r\n", clock, TestContext.CancellationToken));

        Assert.AreEqual("* 1 EXISTS\r\nc OK NOOP completed\r\n", responses);
    }

    [TestMethod]
    [DataRow("c CHECK\r\n", "* 2 EXISTS\r\nc OK CHECK completed\r\n")]
    [DataRow("c STATUS INBOX (MESSAGES)\r\n", "* STATUS INBOX (MESSAGES 2)\r\n* 2 EXISTS\r\nc OK STATUS completed\r\n")]
    public async Task ServeAsync_MailDeliveredMeanwhile_CheckAndStatusReportItAsExists(string command, string expected)
    {
        var store = AnonymousStore(new ManualTimeProvider());
        Deliver(store, string.Empty, 1);

        var responses = await ServeDuringAsync(store, command, () => DeliverBySmtpAsync(store));

        Assert.AreEqual(expected, responses);
    }

    [TestMethod]
    public async Task ServeAsync_MailDeliveredMeanwhile_FetchWithholdsExistsAndTheNextNoopSendsIt()
    {
        var store = AnonymousStore(new ManualTimeProvider());
        Deliver(store, string.Empty, 1);

        var responses = await ServeDuringAsync(store, "c FETCH 1 UID\r\nd NOOP\r\n", () => DeliverBySmtpAsync(store));

        Assert.AreEqual("* 1 FETCH (UID 1)\r\nc OK FETCH completed\r\n* 2 EXISTS\r\nd OK NOOP completed\r\n", responses);
    }

    private static async Task DeliverBySmtpAsync(MailboxStore store)
    {
        Assert.AreEqual(MailRecipientLookup.Deliverable, store.LookUpRecipient("<u@example.test>", out var recipient));
        var pending = store.CreatePendingMessage();
        await pending.Body.WriteAsync("Subject: s\r\n\r\nhi\r\n"u8.ToArray());
        Assert.AreEqual(MailStoreOutcome.Succeeded, await store.DeliverAsync([recipient!], pending, CancellationToken.None));
    }

    // Logs in and selects INBOX, runs the action right after the SELECT's answer, then sends the
    // commands; returns what was written after the SELECT's tagged response.
    private async Task<string> ServeDuringAsync(MailboxStore store, string commands, Func<Task> meanwhile)
    {
        var clock = new ManualTimeProvider();
        var connection = new ActAfterWriteConnection(Bytes(Login + Select + commands), "SELECT completed", meanwhile);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        var written = AfterGreeting(connection.WrittenBytes);
        return written[(written.IndexOf(Selected, StringComparison.Ordinal) + Selected.Length)..];
    }
}
