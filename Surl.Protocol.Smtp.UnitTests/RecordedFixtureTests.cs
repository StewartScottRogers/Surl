using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// Replays each upstream curl recording in <c>Fixtures/</c> (see its README): the bytes curl sent
/// against the replies ADR-0053 decides, which curl accepted with the exit code recorded. Surl
/// must write exactly the replies the recorder sent, and store exactly the message curl sent,
/// after decision 6's trace fields.
/// </summary>
[TestClass]
public sealed class RecordedFixtureTests
{
    private const string Mail = "From: a@x\r\nTo: b@y\r\nSubject: hi\r\n\r\nhello\r\n.dot line\r\n";
    private const string Dots = "Subject: dots\r\n\r\n.\r\n..\r\n.x\r\nend\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("one-recipient", "0", 1, Mail)]
    [DataRow("two-recipients", "0", 2, Mail)]
    [DataRow("refused-recipient", "55", 0, "")]
    [DataRow("refused-recipient-allowfails", "0", 1, Mail)]
    [DataRow("dot-stuffed-body", "0", 1, Dots)]
    [DataRow("vrfy", "0", 0, "")]
    [DataRow("expn", "0", 0, "")]
    [DataRow("help", "0", 0, "")]
    [DataRow("noop", "0", 0, "")]
    public async Task ServeAsync_RecordedRequest_WritesTheRecordedRepliesAndStoresTheMessage(string caseName, string exitCode, int storedCopies, string body)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies(caseName), Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        var expected = Enumerable.Repeat(TraceFields("a@x") + body, storedCopies).ToList();
        CollectionAssert.AreEqual(expected, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRequestOneBytePerRead_WritesTheRecordedReplies()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var request = ReadBytes("dot-stuffed-body", "request.bin");
        var connection = new InMemoryConnection(Enumerable.Range(0, request.Length).Select(index => new ReadOnlyMemory<byte>(request, index, 1)));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("dot-stuffed-body"), Utf8(connection.WrittenBytes));
        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + Dots }, Inbox(store, string.Empty).ToList());
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
