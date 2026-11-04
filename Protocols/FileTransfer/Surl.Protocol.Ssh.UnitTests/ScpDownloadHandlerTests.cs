using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SftpTestPackets;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// SCP's source mode, curl's download, at channel level (ADR-0054, decisions 1, 3 and 13): the
/// client's side is a byte script written by hand from the ADR's account of libssh2's
/// <c>scp_recv</c> - a <c>\0</c> to start and one after each control line, nothing after the data -
/// and surl's bytes are compared whole. BL-172 proves them against the pinned upstream curl build
/// (ADR-0003).
/// </summary>
[TestClass]
public sealed class ScpDownloadHandlerTests
{
    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Download_WithP_SendsTheTLineTheCLineTheBytesAndAZeroThenExitStatus0()
    {
        var run = await RunAsync(Store(new(), clock), Source("/a.txt", preservesTimes: true), [0], [0], [0]);

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("T1790512496 0 1790512496 0\nC0644 12 a.txt\nhello world\n\0", run.Text);
        CollectionAssert.AreEqual(new[] { "SCP download /a.txt: sent 12 bytes" }, run.Notes);
    }

    [TestMethod]
    public async Task Download_WithoutP_SendsNoTLineAndARelativePathIsReadFromTheRoot()
    {
        var run = await RunAsync(Store(new(), clock), Source("dir/b.txt"), [0, 0]);

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("C0644 4 b.txt\nbee\n\0", run.Text);
    }

    [TestMethod]
    public async Task Download_EmptyFile_SendsSizeZeroAndNoBytes()
    {
        var store = Store(new(), clock, fileSystem => WriteFile(fileSystem, string.Empty, "empty.txt"));

        var run = await RunAsync(store, Source("/empty.txt"), [0], [0]);

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("C0644 0 empty.txt\n\0", run.Text);
    }

    [TestMethod]
    public async Task Download_FileLargerThanOneChunk_SendsEveryByte()
    {
        var content = new string('x', ScpDownloadHandler.ChunkBytes + 1000);
        var store = Store(new(), clock, fileSystem => WriteFile(fileSystem, content, "big.txt"));

        var run = await RunAsync(store, Source("/big.txt"), [0], [0]);

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual($"C0644 {content.Length} big.txt\n{content}\0", run.Text);
    }

    [TestMethod]
    [DataRow("/missing.txt")]
    [DataRow("/.hidden.txt")]
    [DataRow("/.surl/lock")]
    [DataRow("/a.txt/")]
    [DataRow("/../../../missing.txt")]
    [DataRow("/a\\b.txt")]
    [DataRow("/a\0b")]
    public async Task Download_NothingServedThere_IsNoSuchFileOrDirectoryAndExitStatus1(string path)
    {
        var run = await RunAsync(Store(new(), clock), Source(path), [0]);

        var rendered = SshLogText.Render(Encoding.UTF8.GetBytes(path));
        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual($"\u0001scp: {rendered}: No such file or directory\n", run.Text);
        CollectionAssert.AreEqual(new[] { $"SCP download {rendered} refused: No such file or directory: answered as absent" }, run.Notes);
    }

    [TestMethod]
    [DataRow("/dir")]
    [DataRow("/")]
    public async Task Download_Directory_IsNotARegularFileAndExitStatus1(string path)
    {
        var run = await RunAsync(Store(new(), clock), Source(path), [0]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual($"\u0001scp: {path}: not a regular file\n", run.Text);
        CollectionAssert.AreEqual(new[] { $"SCP download {path} refused: not a regular file" }, run.Notes);
    }

    [TestMethod]
    public async Task Download_StoreFails_IsReadErrorWithTheMessageInTheNoteOnly()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestThrowingContentFileSystem(clock, new IOException(@"disk C:\secret\a.txt failed")), new());

        var run = await RunAsync(store, Source("/a.txt"), [0], [0]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("C0644 12 a.txt\n\u0001scp: /a.txt: read error\n", run.Text);
        Assert.DoesNotContain("secret", run.Text);
        CollectionAssert.Contains(run.Notes, @"SCP download /a.txt refused: read error: disk C:\secret\a.txt failed");
    }

    [TestMethod]
    public async Task Download_FileUnreadable_IsReadErrorBeforeAnyLine()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestThrowingContentFileSystem(clock, new UnauthorizedAccessException("denied")), new());

        var run = await RunAsync(store, Source("/a.txt"), [0]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\u0001scp: /a.txt: read error\n", run.Text);
    }

    [TestMethod]
    public async Task Download_FileShrinks_EndsAfterTheBytesThereAreWithoutTheZeroAndExitStatus1()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestShrinkingContentFileSystem(clock), new());

        var run = await RunAsync(store, Source("/a.txt"), [0], [0]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("C0644 12 a.txt\n", run.Text);
        CollectionAssert.AreEqual(new[] { "SCP download /a.txt: sent 0 of 12 bytes, the file shrank" }, run.Notes);
    }

    [TestMethod]
    public async Task Download_NoStartAcknowledgement_EndsWithExitStatus1AndSendsNothing()
    {
        var run = await RunAsync(Store(new(), clock), Source("/a.txt"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual(string.Empty, run.Text);
        CollectionAssert.AreEqual(new[] { "SCP download /a.txt ended: the client answered the start with EOF" }, run.Notes);
    }

    [TestMethod]
    public async Task Download_TLineRefused_EndsWithExitStatus1BeforeTheCLine()
    {
        var run = await RunAsync(Store(new(), clock), Source("/a.txt", preservesTimes: true), [0], [1]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("T1790512496 0 1790512496 0\n", run.Text);
        CollectionAssert.AreEqual(new[] { "SCP download /a.txt ended: the client answered the T line with byte 0x01" }, run.Notes);
    }

    [TestMethod]
    public async Task Download_CLineRefused_EndsWithExitStatus1BeforeTheBytes()
    {
        var run = await RunAsync(Store(new(), clock), Source("/a.txt"), [0], [2]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("C0644 12 a.txt\n", run.Text);
        CollectionAssert.AreEqual(new[] { "SCP download /a.txt ended: the client answered the C line with byte 0x02" }, run.Notes);
    }

    [TestMethod]
    public void ContentChannelHandlers_ServeScpDownloadsAndUploads()
    {
        var handlers = new SshContentChannelHandlers(Store(new(), clock));
        var context = Context(clock, TestContext.CancellationToken);

        Assert.IsInstanceOfType<ScpDownloadHandler>(handlers.ForScp(Source("/a.txt"), context));
        Assert.IsInstanceOfType<ScpUploadHandler>(handlers.ForScp(new SshScpCommand(false, false, false, "/a.txt"), context));
    }

    private static SshScpCommand Source(string path, bool preservesTimes = false) => new(true, preservesTimes, false, path);

    private async Task<ScpRun> RunAsync(ContentStore store, SshScpCommand command, params byte[][] inbound)
    {
        var log = new RecordingExchangeLog();
        var channel = new SftpTestChannel(inbound);
        var handler = new ScpDownloadHandler(store, command, Context(clock, TestContext.CancellationToken, null, log));

        var exit = await handler.RunAsync(channel, TestContext.CancellationToken);

        return new ScpRun(exit, Encoding.UTF8.GetString([.. channel.Written]), [.. log.Notes]);
    }

    private sealed record ScpRun(uint Exit, string Text, string[] Notes);
}
