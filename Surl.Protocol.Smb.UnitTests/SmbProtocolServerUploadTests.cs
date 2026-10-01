using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// The NT create, writes and close of an upload to the content store (ADR-0073, decisions 4 and
/// 5), with each expected response built field by field in <see cref="SmbTestExchange"/>.
/// </summary>
[TestClass]
public sealed class SmbProtocolServerUploadTests
{
    private const uint FileCreated = 2;
    private const uint FileOverwritten = 3;

    private static readonly byte[] Upload = Encoding.ASCII.GetBytes("upload me\n");

    public TestContext TestContext { get; set; } = null!;

    private static string CloseResponseHex => ResponseHex(SmbCommand.Close, 0, 1, SmbSession.UserId);

    [TestMethod]
    public async Task UploadWriteAndClose_ANewFile_StoreItAndNoteTheOpenAndTheBytesWritten()
    {
        var fileSystem = StandardFileSystem();
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(
            UploadContentStore(fileSystem), TestContext.CancellationToken, log, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), CloseHex(1, 1));

        Assert.AreEqual(UploadNtCreateResponseHex(1, FileCreated) + WriteResponseHex((ushort)Upload.Length) + CloseResponseHex, written);
        CollectionAssert.AreEqual(Upload, StoredBytes(fileSystem, "share", "up.txt"));
        CollectionAssert.AreEqual(
            new[] { @"SMB open share\up.txt for writing", @"SMB close share\up.txt: 10 bytes written" },
            log.Notes.Skip(2).ToArray());
    }

    [TestMethod]
    public async Task UploadNtCreate_AnExistingFile_IsOverwrittenAndReplacedAtTheClose()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeConnectedAsync(
            UploadContentStore(fileSystem), TestContext.CancellationToken, null, UploadNtCreateHex("file.txt"), WriteHex(1, 0, Upload), CloseHex(1, 1));

        Assert.AreEqual(UploadNtCreateResponseHex(1, FileOverwritten) + WriteResponseHex((ushort)Upload.Length) + CloseResponseHex, written);
        CollectionAssert.AreEqual(Upload, StoredBytes(fileSystem, "share", "file.txt"));
    }

    [TestMethod]
    public async Task Write_AtOffsetsOutOfOrder_LandsEachAtItsOffset()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeConnectedAsync(
            UploadContentStore(fileSystem),
            TestContext.CancellationToken,
            null,
            UploadNtCreateHex("up.txt"),
            WriteHex(1, 5, "world"u8),
            WriteHex(1, 0, "hello"u8),
            CloseHex(1, 1));

        Assert.AreEqual(UploadNtCreateResponseHex(1, FileCreated) + WriteResponseHex(5) + WriteResponseHex(5) + CloseResponseHex, written);
        CollectionAssert.AreEqual("helloworld"u8.ToArray(), StoredBytes(fileSystem, "share", "up.txt"));
    }

    [TestMethod]
    public async Task Upload_BeforeTheClose_LeavesTheTargetAsItWas()
    {
        var fileSystem = StandardFileSystem();

        await ServeConnectedAsync(UploadContentStore(fileSystem), TestContext.CancellationToken, null, UploadNtCreateHex("file.txt"), WriteHex(1, 0, Upload));

        CollectionAssert.AreEqual("hello smb\n"u8.ToArray(), StoredBytes(fileSystem, "share", "file.txt"));
    }

    [TestMethod]
    public async Task Upload_LeftOpenWhenTheConnectionEnds_IsDiscarded()
    {
        var fileSystem = StandardFileSystem();
        var before = fileSystem.EnumerateDirectoryEntryNames(SharePath()).ToArray();

        await ServeConnectedAsync(UploadContentStore(fileSystem), TestContext.CancellationToken, null, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload));

        Assert.IsNull(StoredBytes(fileSystem, "share", "up.txt"));
        CollectionAssert.AreEquivalent(before, fileSystem.EnumerateDirectoryEntryNames(SharePath()).ToArray());
    }

    [TestMethod]
    public async Task TreeDisconnect_WithAnUploadOpen_DiscardsIt()
    {
        var fileSystem = StandardFileSystem();
        var before = fileSystem.EnumerateDirectoryEntryNames(SharePath()).ToArray();

        var written = await ServeConnectedAsync(
            UploadContentStore(fileSystem), TestContext.CancellationToken, null, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), TreeDisconnectHex(1), CloseHex(1, 1));

        Assert.AreEqual(
            UploadNtCreateResponseHex(1, FileCreated) + WriteResponseHex((ushort)Upload.Length) + ResponseHex(SmbCommand.TreeDisconnect, 0, 1, SmbSession.UserId)
            + ErrorHex(SmbCommand.Close, SmbStatus.InvalidTreeId),
            written);
        Assert.IsNull(StoredBytes(fileSystem, "share", "up.txt"));
        CollectionAssert.AreEquivalent(before, fileSystem.EnumerateDirectoryEntryNames(SharePath()).ToArray());
    }

    [TestMethod]
    [DataRow("sub", DisplayName = "A directory at the name")]
    [DataRow(".up.txt", DisplayName = "A hidden name")]
    public async Task UploadNtCreate_WhereNoFileMayBeWritten_IsNoAccessAndNoted(string fileName)
    {
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(UploadContentStore(StandardFileSystem()), TestContext.CancellationToken, log, UploadNtCreateHex(fileName));

        Assert.AreEqual(ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.NoAccess), written);
        Assert.AreEqual($"SMB open share\\{fileName} refused: ERRnoaccess", log.Notes[^1]);
    }

    [TestMethod]
    public async Task UploadNtCreate_WithUploadsOff_IsNoAccessAndStoresNothing()
    {
        var fileSystem = StandardFileSystem();
        var contentStore = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions());

        var written = await ServeConnectedAsync(contentStore, TestContext.CancellationToken, null, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), CloseHex(1, 1));

        Assert.AreEqual(
            ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.NoAccess) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.BadFileId) + ErrorHex(SmbCommand.Close, SmbStatus.BadFileId),
            written);
        Assert.IsNull(StoredBytes(fileSystem, "share", "up.txt"));
    }

    [TestMethod]
    public async Task UploadNtCreate_IntoADirectoryThatDoesNotExist_IsBadPathAndNoted()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(UploadContentStore(StandardFileSystem()), TestContext.CancellationToken, log, UploadNtCreateHex(@"nodir\up.txt"));

        Assert.AreEqual(ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.BadPath), written);
        Assert.AreEqual(@"SMB open share\nodir\up.txt refused: ERRbadpath", log.Notes[^1]);
    }

    [TestMethod]
    public async Task UploadNtCreate_ANameThePathRulesRefuse_IsBadFile()
    {
        var written = await ServeConnectedAsync(UploadContentStore(StandardFileSystem()), TestContext.CancellationToken, null, UploadNtCreateHex(@"..\up.txt"));

        Assert.AreEqual(ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.BadFile), written);
    }

    [TestMethod]
    public async Task Read_OnAFileOpenedForWriting_IsBadAccess()
    {
        var written = await ServeConnectedAsync(UploadContentStore(StandardFileSystem()), TestContext.CancellationToken, null, UploadNtCreateHex("up.txt"), ReadHex(1, 1, 0));

        Assert.AreEqual(UploadNtCreateResponseHex(1, FileCreated) + ErrorHex(SmbCommand.ReadAndX, SmbStatus.BadAccess), written);
    }

    [TestMethod]
    [DataRow(long.MinValue, DisplayName = "Past 2^63")]
    [DataRow(long.MaxValue, DisplayName = "Past the largest file")]
    public async Task Write_AtAnOffsetNoFileCanHave_IsServerError(long offset)
    {
        var written = await ServeConnectedAsync(UploadContentStore(StandardFileSystem()), TestContext.CancellationToken, null, UploadNtCreateHex("up.txt"), WriteHex(1, offset, Upload));

        Assert.AreEqual(UploadNtCreateResponseHex(1, FileCreated) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.ServerError), written);
    }

    [TestMethod]
    public async Task Write_PastMaxFileSize_IsDiskFullThenTheCloseSucceedsAndNothingIsLeft()
    {
        var fileSystem = StandardFileSystem();
        var before = fileSystem.EnumerateDirectoryEntryNames(SharePath()).ToArray();
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(
            UploadContentStore(fileSystem, maxUploadBytes: 8), TestContext.CancellationToken, log, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), WriteHex(1, 0, "x"u8), CloseHex(1, 1));

        Assert.AreEqual(
            UploadNtCreateResponseHex(1, FileCreated) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.DiskFull) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.DiskFull) + CloseResponseHex,
            written);
        CollectionAssert.AreEquivalent(before, fileSystem.EnumerateDirectoryEntryNames(SharePath()).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                @"SMB open share\up.txt for writing",
                @"SMB write share\up.txt refused: past --max-filesize 8",
                @"SMB write share\up.txt refused: past --max-filesize 8",
                @"SMB close share\up.txt: upload discarded",
            },
            log.Notes.Skip(2).ToArray());
    }

    [TestMethod]
    public async Task Write_WhenTheContentStoreCannotWrite_IsGeneralFailureUntilTheCloseDiscardsIt()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider(), maxTotalBytes: 4);
        fileSystem.CreateDirectory(SharePath());
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(
            UploadContentStore(fileSystem), TestContext.CancellationToken, log, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), WriteHex(1, 0, "x"u8), CloseHex(1, 1));

        Assert.AreEqual(
            UploadNtCreateResponseHex(1, FileCreated) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.GeneralFailure) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.GeneralFailure)
            + CloseResponseHex,
            written);
        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(SharePath()));
        Assert.StartsWith(@"SMB write share\up.txt failed: ", log.Notes[^2]);
        Assert.AreEqual(@"SMB close share\up.txt: upload discarded", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Close_WhenTheRenameFails_IsGeneralFailureAndNoted()
    {
        var fileSystem = new UnitTestUploadFailingContentFileSystem { RenameFails = true };
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(
            fileSystem.ContentStore(), TestContext.CancellationToken, log, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), CloseHex(1, 1), CloseHex(1, 1));

        Assert.AreEqual(
            UploadNtCreateResponseHex(1, FileCreated) + WriteResponseHex((ushort)Upload.Length) + ErrorHex(SmbCommand.Close, SmbStatus.GeneralFailure)
            + ErrorHex(SmbCommand.Close, SmbStatus.BadFileId),
            written);
        Assert.IsNull(StoredBytes(fileSystem.Inner, "share", "up.txt"));
        Assert.AreEqual(@"SMB close share\up.txt failed: The disk failed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Close_WhenADirectoryHasTakenTheName_IsNoAccessAndNoted()
    {
        var fileSystem = new UnitTestUploadFailingContentFileSystem { DirectoryAppearingWhenAnUploadOpens = Path.Join(SharePath(), "up.txt") };
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(
            fileSystem.ContentStore(), TestContext.CancellationToken, log, UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), CloseHex(1, 1));

        Assert.AreEqual(UploadNtCreateResponseHex(1, FileCreated) + WriteResponseHex((ushort)Upload.Length) + ErrorHex(SmbCommand.Close, SmbStatus.NoAccess), written);
        Assert.AreEqual(@"SMB close share\up.txt refused: ERRnoaccess", log.Notes[^1]);
    }

    private static string SharePath() => Path.Join(InMemoryContentFileSystem.RootPath, "share");
}
