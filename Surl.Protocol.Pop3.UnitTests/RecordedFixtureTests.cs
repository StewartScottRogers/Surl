using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// Replays each upstream curl recording in <c>Fixtures/</c> (see its README): the bytes curl sent
/// against the replies ADR-0056 decides, which curl accepted with the exit code recorded. Surl,
/// serving a maildrop of two copies of the recorder's message, must write exactly the replies the
/// recorder sent.
/// </summary>
[TestClass]
public sealed class RecordedFixtureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("list", "0", 2)]
    [DataRow("retr", "0", 2)]
    [DataRow("list-l", "0", 2)]
    [DataRow("list-one-l", "0", 2)]
    [DataRow("head-list", "0", 2)]
    [DataRow("head-retr", "0", 2)]
    [DataRow("x-uidl", "0", 2)]
    [DataRow("x-uidl-one", "0", 2)]
    [DataRow("x-capa", "0", 2)]
    [DataRow("x-list-one", "0", 2)]
    [DataRow("x-dele", "0", 1)]
    [DataRow("x-dele-url", "0", 1)]
    [DataRow("x-top", "0", 2)]
    [DataRow("x-top-url", "0", 2)]
    [DataRow("x-retr", "0", 2)]
    [DataRow("x-stat", "0", 2)]
    [DataRow("x-noop", "0", 2)]
    [DataRow("x-rset", "0", 2)]
    [DataRow("x-xyzzy", "8", 2)]
    [DataRow("retr-missing", "8", 2)]
    public async Task ServeAsync_RecordedRequest_WritesTheRecordedReplies(string caseName, string exitCode, int messagesLeft)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message, Message);
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies(caseName), Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.HasCount(messagesLeft, Inbox(store, "u"));
    }

    // curl without -u sends RETR straight after CAPA; --allow-anonymous lets it read the
    // anonymous owner's maildrop (ADR-0056, decision 7).
    [TestMethod]
    public async Task ServeAsync_RecordedRequestWithoutLogin_ReadsTheAnonymousMaildrop()
    {
        Assert.AreEqual("0", Read("anonymous-retr", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock, Message, Message);
        var connection = new InMemoryConnection([ReadBytes("anonymous-retr", "request.bin")]);
        var policy = new Pop3TestPolicy { AnonymousVerdict = PasswordLoginVerdict.AcceptedUnchecked };

        await Server(store, policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("anonymous-retr"), Utf8(connection.WrittenBytes));
        Assert.AreEqual(Message, Utf8(ReadBytes("anonymous-retr", "stdout.bin")));
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRequestOneBytePerRead_WritesTheRecordedReplies()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message, Message);
        var request = ReadBytes("retr", "request.bin");
        var connection = new InMemoryConnection(Enumerable.Range(0, request.Length).Select(index => new ReadOnlyMemory<byte>(request, index, 1)));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("retr"), Utf8(connection.WrittenBytes));
    }

    // Every line the recorder sent, in order, each ending CRLF.
    private static string RecordedReplies(string caseName) =>
        string.Concat(Read(caseName, "transcript.txt")
            .Split("\r\n")
            .Where(line => line.StartsWith("< ", StringComparison.Ordinal))
            .Select(line => line[2..] + "\r\n"));

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
