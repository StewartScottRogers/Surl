using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// The NT create, read and close of a file served from the content store (ADR-0073, decisions 2,
/// 4 and 5), with each expected response built field by field in <see cref="SmbTestExchange"/>.
/// </summary>
[TestClass]
public sealed class SmbProtocolServerReadTests
{
    private static readonly byte[] FileTxt = Encoding.ASCII.GetBytes("hello smb\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NtCreate_AFileOfTheShare_OpensItAsFileIdOneWithItsLengthAndTime()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "file.txt"));

        Assert.AreEqual(NtCreateResponseHex(1, FileTxt.Length), written);
    }

    [TestMethod]
    public async Task ReadAndClose_AnOpenFile_AnswerItsBytesThenSuccessAndNoteBoth()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(TestContext.CancellationToken, log, NtCreateHex(1, "file.txt"), ReadHex(1, 1, 0), CloseHex(1, 1));

        Assert.AreEqual(NtCreateResponseHex(1, FileTxt.Length) + ReadResponseHex(FileTxt) + ResponseHex(SmbCommand.Close, 0, 1, SmbSession.UserId), written);
        CollectionAssert.AreEqual(
            new[] { @"SMB open share\file.txt for reading: 10 bytes", @"SMB close share\file.txt: 10 bytes read" },
            log.Notes.Skip(2).ToArray());
    }

    [TestMethod]
    public async Task Read_ALargeFileInCurlsReads_AnswersEachChunkFromItsOffset()
    {
        var bytes = BigFileBytes();

        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "big.bin"), ReadHex(1, 1, 0), ReadHex(1, 1, 32768));

        Assert.AreEqual(NtCreateResponseHex(1, bytes.Length) + ReadResponseHex(bytes.AsSpan(0, 32768)) + ReadResponseHex(bytes.AsSpan(32768)), written);
    }

    [TestMethod]
    public async Task Read_AskingForMoreThan61440Bytes_Answers61440()
    {
        var bytes = HugeFileBytes();

        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "huge.bin"), ReadHex(1, 1, 100, ushort.MaxValue));

        Assert.AreEqual(NtCreateResponseHex(1, bytes.Length) + ReadResponseHex(bytes.AsSpan(100, SmbSession.MaxReadBytes)), written);
    }

    [TestMethod]
    public async Task Read_AFewBytesFromTheMiddle_AnswersExactlyThose()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "file.txt"), ReadHex(1, 1, 6, 3));

        Assert.AreEqual(NtCreateResponseHex(1, FileTxt.Length) + ReadResponseHex(FileTxt.AsSpan(6, 3)), written);
    }

    [TestMethod]
    [DataRow(10L, (ushort)0x8000, DisplayName = "At the end")]
    [DataRow(1000L, (ushort)0x8000, DisplayName = "Past the end")]
    [DataRow(long.MinValue, (ushort)0x8000, DisplayName = "Past 2^63")]
    [DataRow(0L, (ushort)0, DisplayName = "No bytes asked for")]
    public async Task Read_WithNoBytesToAnswer_AnswersNoBytesWithSuccess(long offset, ushort maxCount)
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "file.txt"), ReadHex(1, 1, offset, maxCount));

        Assert.AreEqual(NtCreateResponseHex(1, FileTxt.Length) + ReadResponseHex([]), written);
    }

    [TestMethod]
    public async Task NtCreate_AnAccentedNameInASubdirectory_OpensIt()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "sub\\café x.txt"));

        Assert.AreEqual(NtCreateResponseHex(1, "accented\n".Length), written);
    }

    [TestMethod]
    [DataRow("nothing.txt", DisplayName = "A missing file")]
    [DataRow(".secret", DisplayName = "A dot-file")]
    [DataRow("sub", DisplayName = "A directory")]
    [DataRow("", DisplayName = "The share itself")]
    [DataRow(@"..\afile.txt", DisplayName = "A path outside the share")]
    [DataRow(@"file.txt\", DisplayName = "A file named as a directory")]
    [DataRow(@"sub\\file.txt", DisplayName = "An empty component")]
    public async Task NtCreate_ANameThatIsNotAServedFile_IsBadFileAndNoted(string fileName)
    {
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(TestContext.CancellationToken, log, NtCreateHex(1, fileName));

        Assert.AreEqual(ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.BadFile), written);
        Assert.AreEqual($"SMB open share\\{fileName} refused: ERRbadfile", log.Notes[^1]);
    }

    [TestMethod]
    [DataRow("000000C0", "05000000", DisplayName = "curl's upload")]
    [DataRow("02000000", "01000000", DisplayName = "FILE_WRITE_DATA")]
    [DataRow("04000000", "01000000", DisplayName = "FILE_APPEND_DATA")]
    [DataRow("00000040", "01000000", DisplayName = "GENERIC_WRITE")]
    [DataRow("00000010", "01000000", DisplayName = "GENERIC_ALL")]
    [DataRow("00000080", "03000000", DisplayName = "FILE_OPEN_IF")]
    public async Task NtCreate_AskingToWriteWithUploadsOff_IsNoAccessAndNoted(string desiredAccessHex, string dispositionHex)
    {
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(TestContext.CancellationToken, log, NtCreateHex(1, "file.txt", desiredAccessHex, dispositionHex));

        Assert.AreEqual(ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.NoAccess), written);
        Assert.AreEqual(@"SMB open share\file.txt refused: ERRnoaccess", log.Notes[^1]);
    }

    [TestMethod]
    public async Task NtCreate_PastSixteenOpenFiles_IsNoFileIdsUntilOneIsClosed()
    {
        var log = new RecordingExchangeLog();
        var opens = Enumerable.Repeat(NtCreateHex(1, "file.txt"), SmbSession.MaxOpenFiles + 1);

        var written = await ServeConnectedAsync(TestContext.CancellationToken, log, [.. opens, CloseHex(1, 5), NtCreateHex(1, "file.txt")]);

        var expected = string.Concat(Enumerable.Range(1, SmbSession.MaxOpenFiles).Select(fileId => NtCreateResponseHex((ushort)fileId, FileTxt.Length)))
            + ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.NoFileIds)
            + ResponseHex(SmbCommand.Close, 0, 1, SmbSession.UserId)
            + NtCreateResponseHex(5, FileTxt.Length);
        Assert.AreEqual(expected, written);
        Assert.Contains(@"SMB open share\file.txt refused: ERRnofids", log.Notes);
    }

    [TestMethod]
    public async Task ReadAndClose_OfAFileIdNeverOpened_AreBadFileId()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "file.txt"), ReadHex(1, 2, 0), CloseHex(1, 2));

        Assert.AreEqual(NtCreateResponseHex(1, FileTxt.Length) + ErrorHex(SmbCommand.ReadAndX, SmbStatus.BadFileId) + ErrorHex(SmbCommand.Close, SmbStatus.BadFileId), written);
    }

    [TestMethod]
    public async Task ReadAndClose_AfterTheClose_AreBadFileId()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "file.txt"), CloseHex(1, 1), ReadHex(1, 1, 0), CloseHex(1, 1));

        Assert.AreEqual(
            NtCreateResponseHex(1, FileTxt.Length) + ResponseHex(SmbCommand.Close, 0, 1, SmbSession.UserId)
            + ErrorHex(SmbCommand.ReadAndX, SmbStatus.BadFileId) + ErrorHex(SmbCommand.Close, SmbStatus.BadFileId),
            written);
    }

    [TestMethod]
    public async Task Read_OnAnotherTreeThanTheOpen_IsBadFileId()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "file.txt"), TreeConnectHex("share"), ReadHex(2, 1, 0));

        Assert.AreEqual(NtCreateResponseHex(1, FileTxt.Length) + TreeConnectResponseHex(2) + ErrorHex(SmbCommand.ReadAndX, SmbStatus.BadFileId, 2), written);
    }

    [TestMethod]
    public async Task TreeDisconnect_ClosesTheTreesOpenFilesOnly()
    {
        var written = await ServeConnectedAsync(
            TestContext.CancellationToken,
            null,
            TreeConnectHex("share"),
            NtCreateHex(1, "file.txt"),
            NtCreateHex(2, "file.txt"),
            TreeDisconnectHex(1),
            TreeConnectHex("share"),
            ReadHex(1, 1, 0),
            ReadHex(2, 2, 0));

        Assert.AreEqual(
            TreeConnectResponseHex(2) + NtCreateResponseHex(1, FileTxt.Length) + NtCreateResponseHex(2, FileTxt.Length, 2)
            + ResponseHex(SmbCommand.TreeDisconnect, 0, 1, SmbSession.UserId) + TreeConnectResponseHex(1)
            + ErrorHex(SmbCommand.ReadAndX, SmbStatus.BadFileId) + ReadResponseHex(FileTxt, 2),
            written);
    }

    [TestMethod]
    public async Task Write_OnAFileOpenedForReading_IsBadAccess()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(1, "file.txt"), WriteHex(1, 1));

        Assert.AreEqual(NtCreateResponseHex(1, FileTxt.Length) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.BadAccess), written);
    }

    [TestMethod]
    public async Task Read_WhenTheContentStoreCannotReadTheFile_IsGeneralFailureAndNoted()
    {
        var log = new RecordingExchangeLog();
        var contentStore = new UnitTestUnreadableContentFileSystem().ContentStore();

        var written = await ServeConnectedAsync(contentStore, TestContext.CancellationToken, log, NtCreateHex(1, "file.txt"), ReadHex(1, 1, 0), CloseHex(1, 1));

        Assert.AreEqual(
            NtCreateResponseHex(1, FileTxt.Length) + ErrorHex(SmbCommand.ReadAndX, SmbStatus.GeneralFailure) + ResponseHex(SmbCommand.Close, 0, 1, SmbSession.UserId),
            written);
        CollectionAssert.AreEqual(
            new[] { @"SMB read share\file.txt failed: The disk is unreadable.", @"SMB close share\file.txt: 0 bytes read" },
            log.Notes.Skip(3).ToArray());
    }
}
