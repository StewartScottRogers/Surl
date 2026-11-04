using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SftpTestPackets;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// SCP's sink mode, curl's upload, at channel level (ADR-0054, decisions 1, 4, 12 and 13): the
/// client's side is a byte script written by hand from the ADR's account of libssh2's
/// <c>scp_send</c> - a <c>C</c> line, the file's bytes, then <c>EOF</c> - and surl's bytes are
/// compared whole. BL-172 proves them against the pinned upstream curl build (ADR-0003).
/// </summary>
[TestClass]
public sealed class ScpUploadHandlerTests
{
    private static readonly ContentExposureOptions Uploads = new() { AllowUploads = true };

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Upload_Allowed_AcknowledgesTheStartTheCLineAndTheEndAndCommitsTheFile()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("/new.txt"), Ascii("C0644 5 ignored.txt\n"), Ascii("hello"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("\0\0\0", run.Text);
        Assert.AreEqual("hello", ReadFile(fileSystem, "new.txt"));
        CollectionAssert.AreEqual(new[] { "SCP upload /new.txt: received 5 bytes, mode 0644 not kept" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_ReplacesAnExistingFileAndTakesTheControlLineAndDataInOneChunk()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("a.txt"), Ascii("C0600 3 a.txt\nnew"), [0]);

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("\0\0\0", run.Text);
        Assert.AreEqual("new", ReadFile(fileSystem, "a.txt"));
    }

    [TestMethod]
    public async Task Upload_EmptyFile_IsCommittedEmpty()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("/empty.txt"), Ascii("C0644 0 empty.txt\n"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual(string.Empty, ReadFile(fileSystem, "empty.txt"));
    }

    [TestMethod]
    public async Task Upload_LargerThanOneChunk_WritesEveryByte()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);
        var content = new string('y', ScpUploadHandler.ChunkBytes * 2 + 7);

        var run = await RunAsync(store, Sink("/big.txt"), Ascii($"C0644 {content.Length} big.txt\n"), Ascii(content));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual(content, ReadFile(fileSystem, "big.txt"));
    }

    [TestMethod]
    [DataRow("/dir", "/dir/c.txt")]
    [DataRow("/dir/", "/dir/c.txt")]
    public async Task Upload_PathNamesADirectory_WritesTheFileUnderItsName(string path, string target)
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink(path, targetIsDirectory: true), Ascii("C0644 3 c.txt\n"), Ascii("sea"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("sea", ReadFile(fileSystem, "dir", "c.txt"));
        CollectionAssert.AreEqual(new[] { $"SCP upload {target}: received 3 bytes, mode 0644 not kept" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_WithoutAllowUploads_IsPermissionDeniedBeforeAnyDataAndLeavesNothing()
    {
        var (store, fileSystem) = StoreWithFileSystem(new());
        var before = RootNames(fileSystem);

        var run = await RunAsync(store, Sink("/new.txt"), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\u0001scp: /new.txt: Permission denied\n", run.Text);
        CollectionAssert.AreEquivalent(before, RootNames(fileSystem));
        CollectionAssert.AreEqual(new[] { "SCP upload /new.txt refused: /new.txt: Permission denied: uploads are off (--allow-uploads)" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_PastMaxFilesize_IsFileTooLargeBeforeAnyDataAndLeavesNothingBehind()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads with { MaxUploadBytes = 4 });
        var before = RootNames(fileSystem);

        var run = await RunAsync(store, Sink("/new.txt"), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\u0001scp: /new.txt: File too large\n", run.Text);
        CollectionAssert.AreEquivalent(before, RootNames(fileSystem));
        CollectionAssert.AreEqual(new[] { "SCP upload /new.txt refused: /new.txt: File too large: past --max-filesize" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_AtMaxFilesize_IsAccepted()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads with { MaxUploadBytes = 5 });

        var run = await RunAsync(store, Sink("/new.txt"), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("hello", ReadFile(fileSystem, "new.txt"));
    }

    [TestMethod]
    [DataRow("/.hidden.txt", "Permission denied")]
    [DataRow("/.surl/lock", "Permission denied")]
    [DataRow("/new.txt/", "Permission denied")]
    [DataRow("/missing/new.txt", "No such file or directory")]
    [DataRow("/a.txt/new.txt", "No such file or directory")]
    public async Task Upload_TargetTheStoreRefuses_IsAnsweredAsDecision4SaysAndLeavesNothing(string path, string reason)
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);
        var before = RootNames(fileSystem);

        var run = await RunAsync(store, Sink(path), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual($"\0\u0001scp: {path}: {reason}\n", run.Text);
        CollectionAssert.AreEquivalent(before, RootNames(fileSystem));
        Assert.AreEqual("lock\n", ReadFile(fileSystem, ".surl", "lock"));
    }

    [TestMethod]
    [DataRow("/..\\..\\escape.txt")]
    [DataRow("/a\0b")]
    public async Task Upload_PathTheStoreCannotMap_IsNoSuchFileOrDirectory(string path)
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);
        var before = RootNames(fileSystem);

        var run = await RunAsync(store, Sink(path), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        var rendered = SshLogText.Render(Encoding.UTF8.GetBytes(path));
        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual($"\0\u0001scp: {rendered}: No such file or directory\n", run.Text);
        CollectionAssert.AreEquivalent(before, RootNames(fileSystem));
        CollectionAssert.AreEqual(new[] { $"SCP upload {rendered} refused: {rendered}: No such file or directory: answered as absent" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_DotDotAboveTheRoot_StopsAtTheRootAndWritesInsideIt()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("/../../../inside.txt"), Ascii("C0644 2 x\n"), Ascii("in"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("in", ReadFile(fileSystem, "inside.txt"));
    }

    [TestMethod]
    public async Task Upload_NameIsADirectoryUnderTheTargetDirectory_IsIsADirectory()
    {
        var run = await RunAsync(Store(Uploads, clock), Sink("/"), Ascii("C0644 5 dir\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\u0001scp: /dir: Is a directory\n", run.Text);
    }

    [TestMethod]
    [DataRow("/a.txt")]
    [DataRow("/missing")]
    public async Task Upload_WithDAndAPathThatIsNotADirectory_IsNotADirectory(string path)
    {
        var run = await RunAsync(Store(Uploads, clock), Sink(path, targetIsDirectory: true), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual($"\0\u0001scp: {path}: Not a directory\n", run.Text);
        CollectionAssert.AreEqual(new[] { $"SCP upload {path} refused: {path}: Not a directory" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_TLineWithP_IsAcknowledgedAndSetsTheModificationTime()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("/new.txt", preservesTimes: true), Ascii("T1790512496 0 1790512400 0\n"), Ascii("C0644 2 new.txt\n"), Ascii("hi"), [0]);

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("\0\0\0\0", run.Text);
        Assert.AreEqual(WrittenAt, fileSystem.GetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "new.txt")));
        CollectionAssert.AreEqual(new[] { "SCP upload /new.txt: received 2 bytes, mode 0644 not kept, modification time set" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_TLineWithoutP_IsAcknowledgedButTheTimeIsNotKept()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("/new.txt"), Ascii("T1790512496 0 1790512496 0\n"), Ascii("C0644 2 new.txt\n"), Ascii("hi"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("\0\0\0\0", run.Text);
        Assert.AreEqual(clock.GetUtcNow(), fileSystem.GetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "new.txt")));
    }

    [TestMethod]
    public async Task Upload_PWithoutATLine_KeepsTheTimeOfWriting()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("/new.txt", preservesTimes: true), Ascii("C0644 2 new.txt\n"), Ascii("hi"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual(clock.GetUtcNow(), fileSystem.GetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "new.txt")));
        CollectionAssert.AreEqual(new[] { "SCP upload /new.txt: received 2 bytes, mode 0644 not kept" }, run.Notes);
    }

    [TestMethod]
    [DataRow("D0755 0 sub\n", "received directory without -r")]
    [DataRow("E\n", "received directory without -r")]
    [DataRow("C0648 5 new.txt\n", "bad mode")]
    [DataRow("C0644 five new.txt\n", "bad size")]
    [DataRow("C0644 5 ..\n", "unexpected filename")]
    [DataRow("hello\n", "unexpected line")]
    [DataRow("T1 0\n", "unexpected line")]
    public async Task Upload_MalformedControlLine_IsAProtocolErrorBeforeAnyData(string line, string error)
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);
        var before = RootNames(fileSystem);

        var run = await RunAsync(store, Sink("/new.txt"), Ascii(line), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual($"\0\u0001scp: protocol error: {error}\n", run.Text);
        CollectionAssert.AreEquivalent(before, RootNames(fileSystem));
        CollectionAssert.AreEqual(new[] { $"SCP upload /new.txt refused: protocol error: {error}" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_ControlLinePastMaxLine_IsLineTooLong()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 15 };

        var run = await RunAsync(Store(Uploads, clock), Sink("/new.txt"), limits, Ascii("C0644 5 new.txt\n"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\u0001scp: protocol error: line too long\n", run.Text);
    }

    [TestMethod]
    public async Task Upload_ControlLineOfExactlyMaxLine_IsRead()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };

        var run = await RunAsync(Store(Uploads, clock), Sink("/new.txt"), limits, Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(0u, run.Exit);
    }

    [TestMethod]
    public async Task Upload_MaxLineAndMaxFilesizeOfZero_AreNoLimit()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads with { MaxUploadBytes = 0 });
        var limits = ExchangeLimits.Default with { MaxLineBytes = 0 };

        var run = await RunAsync(store, Sink("/new.txt"), limits, Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("hello", ReadFile(fileSystem, "new.txt"));
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { (byte)'C', (byte)'0' })]
    public async Task Upload_ClientEndsBeforeACLine_EndsWithExitStatus1(byte[] inbound)
    {
        var run = await RunAsync(Store(Uploads, clock), Sink("/new.txt"), inbound);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0", run.Text);
        CollectionAssert.AreEqual(new[] { "SCP upload /new.txt ended: the client sent no file" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_DataEndsEarly_IsDiscardedAndTheTargetUntouched()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);
        var before = RootNames(fileSystem);

        var run = await RunAsync(store, Sink("/a.txt"), Ascii("C0644 5 a.txt\n"), Ascii("hel"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\0", run.Text);
        Assert.AreEqual("hello world\n", ReadFile(fileSystem, "a.txt"));
        CollectionAssert.AreEquivalent(before, RootNames(fileSystem));
        CollectionAssert.AreEqual(new[] { "SCP upload /a.txt abandoned after 3 of 5 bytes" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_ByteOtherThanZeroAfterTheData_KeepsTheFileAndEndsWithExitStatus1()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);

        var run = await RunAsync(store, Sink("/new.txt"), Ascii("C0644 2 new.txt\n"), Ascii("hi"), [1]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\0", run.Text);
        Assert.AreEqual("hi", ReadFile(fileSystem, "new.txt"));
        CollectionAssert.Contains(run.Notes, "SCP upload /new.txt ended: the client sent byte 0x01 after the data");
    }

    [TestMethod]
    [DataRow("IOException")]
    [DataRow("UnauthorizedAccessException")]
    public async Task Upload_StoreFailsToWrite_IsWriteErrorWithTheMessageInTheNoteOnly(string kind)
    {
        Exception failure = kind == "IOException" ? new IOException(@"disk C:\secret full") : new UnauthorizedAccessException(@"C:\secret denied");
        var fileSystem = new UnitTestWriteFailingContentFileSystem(new InMemoryContentFileSystem(clock), failure);
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, Uploads);

        var run = await RunAsync(store, Sink("/new.txt"), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\u0001scp: /new.txt: write error\n", run.Text);
        Assert.DoesNotContain("secret", run.Text);
        CollectionAssert.AreEqual(new[] { $"SCP upload /new.txt refused: /new.txt: write error: {failure.Message}" }, run.Notes);
    }

    [TestMethod]
    public async Task Upload_TargetBecomesADirectoryBeforeTheCommit_IsWriteErrorAndLeavesNoFile()
    {
        var (store, fileSystem) = StoreWithFileSystem(Uploads);
        var target = Path.Join(InMemoryContentFileSystem.RootPath, "new.txt");
        var channel = new ActingChannel(new SftpTestChannel(Ascii("C0644 5 new.txt\n"), Ascii("hello")), 2, () => fileSystem.CreateDirectory(target));

        var run = await RunAsync(store, Sink("/new.txt"), ExchangeLimits.Default, channel);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\0\0\u0001scp: /new.txt: write error\n", run.Text);
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(target));
        CollectionAssert.AreEqual(new[] { "SCP upload /new.txt refused: /new.txt: write error: the target can no longer take a file" }, run.Notes);
    }

    private static SshScpCommand Sink(string path, bool preservesTimes = false, bool targetIsDirectory = false) =>
        new(false, preservesTimes, targetIsDirectory, path);

    private static string ReadFile(InMemoryContentFileSystem fileSystem, params string[] segments)
    {
        using var file = fileSystem.OpenFileForAsyncRead(Path.Join([InMemoryContentFileSystem.RootPath, .. segments]));
        using var reader = new StreamReader(file, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private static string[] RootNames(InMemoryContentFileSystem fileSystem) =>
        [.. fileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath)];

    private (ContentStore Store, InMemoryContentFileSystem FileSystem) StoreWithFileSystem(ContentExposureOptions options)
    {
        InMemoryContentFileSystem? captured = null;
        var store = Store(options, clock, fileSystem => captured = fileSystem);

        return (store, captured!);
    }

    private Task<ScpRun> RunAsync(ContentStore store, SshScpCommand command, params byte[][] inbound) =>
        RunAsync(store, command, ExchangeLimits.Default, inbound);

    private Task<ScpRun> RunAsync(ContentStore store, SshScpCommand command, ExchangeLimits limits, params byte[][] inbound) =>
        RunAsync(store, command, limits, new SftpTestChannel(inbound));

    private async Task<ScpRun> RunAsync(ContentStore store, SshScpCommand command, ExchangeLimits limits, ISshChannelDataStream channel)
    {
        var log = new RecordingExchangeLog();
        var handler = new ScpUploadHandler(store, command, Context(clock, TestContext.CancellationToken, limits, log));

        var exit = await handler.RunAsync(channel, TestContext.CancellationToken);
        var written = channel is ActingChannel acting ? acting.Inner.Written : ((SftpTestChannel)channel).Written;

        return new ScpRun(exit, Encoding.UTF8.GetString([.. written]), [.. log.Notes]);
    }

    private sealed record ScpRun(uint Exit, string Text, string[] Notes);

    // Runs an action just before the given read (counting from 1), as a change on disk made while
    // the client's data is arriving.
    private sealed class ActingChannel(SftpTestChannel inner, int actBeforeRead, Action action) : ISshChannelDataStream
    {
        private int reads;

        public SftpTestChannel Inner => inner;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (++reads == actBeforeRead)
            {
                action();
            }

            return inner.ReadAsync(buffer, cancellationToken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
            inner.WriteAsync(data, cancellationToken);
    }
}
