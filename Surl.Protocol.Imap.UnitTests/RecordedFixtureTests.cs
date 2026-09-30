using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// Replays each upstream curl recording in <c>Fixtures/</c> (see its README): the bytes curl sent
/// against the responses ADR-0055 decides, which curl accepted with exit 0. Surl must write
/// exactly the responses the recorder sent, against a store whose <c>INBOX</c> holds two unseen
/// messages beside a mailbox <c>Sent</c>.
/// </summary>
[TestClass]
public sealed class RecordedFixtureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("list-root")]
    [DataRow("list-inbox")]
    [DataRow("lsub")]
    [DataRow("select")]
    [DataRow("examine")]
    [DataRow("status")]
    [DataRow("fetch-uid")]
    [DataRow("fetch-mailindex")]
    [DataRow("fetch-section-text")]
    [DataRow("fetch-header-fields")]
    [DataRow("fetch-section-1")]
    [DataRow("fetch-partial")]
    [DataRow("fetch-text-partial")]
    [DataRow("search-subject")]
    [DataRow("fetch-all")]
    [DataRow("uid-fetch-bodystructure")]
    [DataRow("uid-search")]
    public async Task ServeAsync_RecordedRequest_WritesTheRecordedResponses(string caseName)
    {
        Assert.AreEqual("0", Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses(caseName), Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("append", "INBOX[1 None, 2 None, 3 Seen] Sent[]")]
    [DataRow("create", "Archive[] INBOX[1 None, 2 None] Sent[]")]
    [DataRow("delete", "INBOX[1 None, 2 None]")]
    [DataRow("rename", "Archive[] INBOX[1 None, 2 None]")]
    [DataRow("subscribe", "INBOX[1 None, 2 None] Sent[]")]
    [DataRow("unsubscribe", "INBOX[1 None, 2 None] Sent[]")]
    [DataRow("store", "INBOX[1 Deleted, 2 None] Sent[]")]
    [DataRow("uid-store-silent", "INBOX[1 Seen, 2 Seen] Sent[]")]
    [DataRow("copy", "INBOX[1 None, 2 None] Sent[1 None, 2 None]")]
    [DataRow("uid-move", "INBOX[2 None] Sent[1 None]")]
    [DataRow("expunge", "INBOX[2 None] Sent[]")]
    [DataRow("uid-expunge", "INBOX[2 None] Sent[]")]
    public async Task ServeAsync_RecordedChange_WritesTheRecordedResponsesAndChangesTheStore(string caseName, string expectedStore)
    {
        Assert.AreEqual("0", Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        if (caseName.EndsWith("expunge", StringComparison.Ordinal))
        {
            store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
        }

        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses(caseName), Utf8(connection.WrittenBytes));
        Assert.AreEqual(expectedStore, Describe(store));
    }

    [TestMethod]
    public async Task ServeAsync_RecordedAppend_StoresTheUploadedBytesExactly()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        var connection = new InMemoryConnection([ReadBytes("append", "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(store.ViewFor(null), "INBOX", 3, out var message));
        Assert.AreEqual("From: a@x\r\nSubject: hi\r\n\r\nhello\r\n", Encoding.UTF8.GetString(message.Span));
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRequestOneBytePerRead_WritesTheRecordedResponses()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        var request = ReadBytes("status", "request.bin");
        var connection = new InMemoryConnection(Enumerable.Range(0, request.Length).Select(index => new ReadOnlyMemory<byte>(request, index, 1)));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses("status"), Utf8(connection.WrittenBytes));
    }

    // Every line the recorder sent, in order, each ending CRLF.
    private static string RecordedResponses(string caseName) =>
        string.Concat(Read(caseName, "transcript.txt")
            .Split("\r\n")
            .Where(line => line.StartsWith("< ", StringComparison.Ordinal))
            .Select(line => line[2..] + "\r\n"));

    // Every mailbox in the store's order, each with its messages' UIDs and flags.
    private static string Describe(MailboxStore store)
    {
        var view = store.ViewFor(null);
        return string.Join(' ', store.ListMailboxes(view).Select(name =>
        {
            store.ReadMailbox(view, name, out var snapshot);
            return $"{name}[{string.Join(", ", snapshot!.Messages.Select(message => $"{message.Uid} {message.Flags}"))}]";
        }));
    }

    private static string Read(string caseName, string fileName) => Encoding.UTF8.GetString(ReadBytes(caseName, fileName));

    private static byte[] ReadBytes(string caseName, string fileName)
    {
        using var stream = typeof(RecordedFixtureTests).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/{fileName}")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/{fileName}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
