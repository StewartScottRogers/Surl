using System.Text;
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
