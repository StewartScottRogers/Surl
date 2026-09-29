using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class FileSystemFailureTests
{
    private const string DateAndServer = "Date: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n";
    private const string NotFound = "HTTP/1.1 404 Not Found\r\n" + DateAndServer + "Content-Length: 0\r\n\r\n";
    private const string OkHead =
        "HTTP/1.1 200 OK\r\n" + DateAndServer
        + "Last-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\n"
        + "Content-Type: application/octet-stream\r\n"
        + "Content-Length: 17\r\n\r\n";

    private static readonly string FilePath = Path.Join(Root, "file.txt");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(nameof(InMemoryContentFileSystem.GetLastWriteTimeUtc), true, "GET")]
    [DataRow(nameof(InMemoryContentFileSystem.GetFileLength), true, "GET")]
    [DataRow(nameof(InMemoryContentFileSystem.GetFileLength), false, "HEAD")]
    public async Task ServeAsync_FileStatusThrows_Answers404AndKeepsTheConnection(string failingMember, bool deniesAccess, string method)
    {
        Exception failure = deniesAccess ? new UnauthorizedAccessException($"Access to the path '{FilePath}' is denied.") : new IOException("The disk failed.");
        var fileSystem = StandardFileSystem().FailOn(FilePath, failingMember, failure);

        var (connection, log) = await ServeAsync(
            [Ascii($"{method} /file.txt HTTP/1.1\r\nHost: h\r\n\r\n{method} /missing HTTP/1.1\r\nHost: h\r\n\r\n")],
            TestContext.CancellationToken,
            fileSystem: fileSystem);

        Assert.AreEqual(NotFound + NotFound, Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual($"{method} /file.txt: 404, {FilePath} could not be read ({failure.GetType().Name}: {failure.Message})", log.Notes[0]);
        Assert.StartsWith($"{method} /missing: 404, nothing exists at", log.Notes[1]);
    }

    [TestMethod]
    public async Task ServeAsync_FileStatusThrowsOnAConnectionThatCloses_Answers404WithConnectionClose()
    {
        var fileSystem = StandardFileSystem().FailOn(FilePath, nameof(InMemoryContentFileSystem.GetFileLength), new IOException("The disk failed."));

        var (connection, _) = await ServeAsync(
            [Ascii("GET /file.txt HTTP/1.1\r\nHost: h\r\nConnection: close\r\n\r\n")],
            TestContext.CancellationToken,
            fileSystem: fileSystem);

        Assert.AreEqual("HTTP/1.1 404 Not Found\r\n" + DateAndServer + "Content-Length: 0\r\nConnection: close\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ServeAsync_OpenThrowsAfterTheHead_AbortsTheConnectionWithANoteNamingTheFile(bool deniesAccess)
    {
        Exception failure = deniesAccess ? new UnauthorizedAccessException("Access is denied.") : new FileNotFoundException("The file was deleted.");
        var fileSystem = StandardFileSystem().FailOn(FilePath, nameof(InMemoryContentFileSystem.OpenFileForAsyncRead), failure);

        var (connection, log) = await ServeAsync(
            [Ascii("GET /file.txt HTTP/1.1\r\nHost: h\r\n\r\nGET /file.txt HTTP/1.1\r\nHost: h\r\n\r\n")],
            TestContext.CancellationToken,
            fileSystem: fileSystem);

        Assert.AreEqual(OkHead, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.Aborted);
        Assert.HasCount(2, log.Notes);
        Assert.AreEqual($"{FilePath} could not be read after the 200 head was sent ({failure.GetType().Name}: {failure.Message}); the connection was aborted.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionWriteThrowsWhileTheFileIsSent_EscapesAsTheConnectionsFailure()
    {
        var connection = new FailingWriteConnection(Ascii("GET /file.txt HTTP/1.1\r\nHost: h\r\n\r\n"));
        var log = new RecordingExchangeLog();

        var thrown = await Assert.ThrowsExactlyAsync<IOException>(
            () => Server().ServeAsync(connection, Context(log, new FixedTimeProvider(Now), TestContext.CancellationToken)));

        Assert.AreEqual("The peer reset the connection.", thrown.Message);
        Assert.IsFalse(connection.Aborted);
        Assert.HasCount(1, log.Notes);
    }

    [TestMethod]
    [DataRow(nameof(InMemoryContentFileSystem.GetFileLength))]
    [DataRow(nameof(InMemoryContentFileSystem.OpenFileForAsyncRead))]
    public async Task ServeAsync_OtherExceptionFromTheContentStore_Escapes(string failingMember)
    {
        var fileSystem = StandardFileSystem().FailOn(FilePath, failingMember, new InvalidOperationException("A defect."));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ServeAsync([Ascii("GET /file.txt HTTP/1.1\r\nHost: h\r\n\r\n")], TestContext.CancellationToken, fileSystem: fileSystem));
    }
}
