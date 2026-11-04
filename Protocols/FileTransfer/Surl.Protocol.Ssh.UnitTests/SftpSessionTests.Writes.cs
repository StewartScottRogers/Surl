using Surl.Content;
using static Surl.Protocol.Ssh.SftpTestPackets;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The <c>sftp</c> subsystem's write side at channel level (ADR-0054, decisions 9 to 11 and 13):
/// uploads, and the requests behind curl's <c>-Q</c> commands, each packet built by hand from
/// draft-ietf-secsh-filexfer-02 and fed to <see cref="SftpSession"/> as channel data. BL-172 proves
/// these answers against the pinned upstream curl build (ADR-0003).
/// </summary>
public sealed partial class SftpSessionTests
{
    private const uint CurlUpload = 0x1A; // WRITE|CREAT|TRUNC
    private const uint CurlAppend = 0x0E; // WRITE|APPEND|CREAT
    private const uint CurlResume = 0x02; // WRITE
    private const uint CurlFileMode = 0x81A4; // --create-file-mode 0644 with the regular-file bits
    private const uint CurlDirectoryMode = 0x41ED; // 040755

    private static readonly ContentExposureOptions UploadsOn = new() { AllowUploads = true };

    private static readonly string[] StandardRootNames = [".hidden.txt", ".surl", "a.txt", "dir"];

    private InMemoryContentFileSystem files = null!;

    [TestMethod]
    public async Task Upload_AsCurlSendsIt_IsCommittedAtCloseWithThePermissionsNoted()
    {
        var run = await RunAsync(
            WritableStore(),
            OpenCarrying(1, "/new.txt", CurlUpload, CurlFileMode),
            Write(2, 0, 0, "hello "),
            Write(3, 0, 6, "again"),
            Close(4, 0));

        CollectionAssert.AreEqual(HandleReply(1, 0), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 0, "Success"), run.Replies[1]);
        CollectionAssert.AreEqual(Status(3, 0, "Success"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 0, "Success"), run.Replies[3]);
        Assert.AreEqual("hello again", TextOf("new.txt"));
        CollectionAssert.AreEqual(new[] { ".hidden.txt", ".surl", "a.txt", "dir", "new.txt" }, RootNames());
        CollectionAssert.IsSubsetOf(
            new[] { "SFTP OPEN /new.txt WRITE|CREAT|TRUNC -> HANDLE", "SFTP OPEN /new.txt: permissions 100644 not kept", "SFTP CLOSE /new.txt: wrote 11 bytes" },
            run.Notes);
    }

    [TestMethod]
    public async Task Upload_OverAnExistingFile_ReplacesItOnlyAtCloseAndNotesNoPermissions()
    {
        var run = await RunAsync(
            WritableStore(),
            OpenCarrying(1, "/a.txt", CurlUpload, CurlFileMode),
            Write(2, 0, 0, "x"),
            Request(17, 3, Text("/a.txt")),
            Close(4, 0),
            Request(17, 5, Text("/a.txt")));

        CollectionAssert.AreEqual(AttributesReply(3, 12, RegularFile, WrittenAt), run.Replies[2]);
        Assert.AreEqual(1, run.Replies[4][20]);
        Assert.AreEqual("x", TextOf("a.txt"));
        Assert.IsFalse(run.Notes.Any(note => note.Contains("not kept", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Upload_Append_WritesEveryWriteAtTheEndWhateverItsOffset()
    {
        var run = await RunAsync(WritableStore(), Open(1, "/a.txt", CurlAppend), Write(2, 0, 0, "more"), Write(3, 0, 0, "!"), Close(4, 0));

        CollectionAssert.AreEqual(Status(4, 0, "Success"), run.Replies[3]);
        Assert.AreEqual("hello world\nmore!", TextOf("a.txt"));
    }

    [TestMethod]
    public async Task Upload_Resume_WritesAtItsOffsetAfterACopyOfTheFile()
    {
        var run = await RunAsync(WritableStore(), Request(17, 1, Text("/a.txt")), Open(2, "/a.txt", CurlResume), Write(3, 0, 12, "again"), Close(4, 0));

        CollectionAssert.AreEqual(Status(4, 0, "Success"), run.Replies[3]);
        Assert.AreEqual("hello world\nagain", TextOf("a.txt"));
    }

    [TestMethod]
    public async Task Upload_WritePastItsEnd_FillsTheGapWithZeroBytes()
    {
        await RunAsync(WritableStore(), Open(1, "/new.txt", 0x0A), Write(2, 0, 3, "x"), Close(3, 0));

        Assert.AreEqual("\0\0\0x", TextOf("new.txt"));
    }

    [TestMethod]
    public async Task Upload_OpenedWithReadToo_ReadsItsOwnBytesAndFstatReportsItsLength()
    {
        var now = clock.GetUtcNow();

        var run = await RunAsync(
            WritableStore(),
            Open(1, "/new.txt", 0x0B),
            Write(2, 0, 0, "hello"),
            Read(3, 0, 1, 30000),
            Read(4, 0, 5, 30000),
            Request(8, 5, Handle(0)));

        CollectionAssert.AreEqual(DataReply(3, "ello"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 1, "End of file"), run.Replies[3]);
        CollectionAssert.AreEqual(AttributesReply(5, 5, RegularFile, now), run.Replies[4]);
    }

    [TestMethod]
    public async Task Upload_HandleKindsCrossed_AreRefused()
    {
        var run = await RunAsync(
            WritableStore(),
            Open(1, "/a.txt"),
            Open(2, "/new.txt", CurlUpload),
            Write(3, 0, 0, "x"),
            Read(4, 1, 0, 10),
            Write(5, 9, 0, "x"));

        CollectionAssert.AreEqual(Status(3, 4, "Handle not open for writing"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 4, "Handle not open for reading"), run.Replies[3]);
        CollectionAssert.AreEqual(Status(5, 4, "Invalid handle"), run.Replies[4]);
    }

    [TestMethod]
    public async Task Upload_PastMaxFileSize_IsDiscardedWithNothingLeftBehind()
    {
        var run = await RunAsync(
            WritableStore(UploadsOn with { MaxUploadBytes = 4 }),
            Open(1, "/new.txt", 0x1B),
            Write(2, 0, 0, "hel"),
            Write(3, 0, 3, "lo again"),
            Write(4, 0, 0, "h"),
            Read(5, 0, 0, 10),
            Request(8, 6, Handle(0)),
            Request(10, 7, Handle(0), UInt32(0)),
            Close(8, 0),
            Close(9, 0));

        CollectionAssert.AreEqual(Status(2, 0, "Success"), run.Replies[1]);
        foreach (var id in new uint[] { 3, 4, 5, 6, 7, 8 })
        {
            CollectionAssert.AreEqual(Status(id, 4, "File too large"), run.Replies[(int)id - 1]);
        }

        CollectionAssert.AreEqual(Status(9, 4, "Invalid handle"), run.Replies[8]);
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
        CollectionAssert.IsSubsetOf(
            new[] { "SFTP WRITE /new.txt -> FAILURE: past --max-filesize", "SFTP CLOSE /new.txt: upload discarded: past --max-filesize" },
            run.Notes);
    }

    [TestMethod]
    public async Task Upload_PastMaxFileSizeOverAnExistingFile_LeavesItUntouched()
    {
        var run = await RunAsync(WritableStore(UploadsOn with { MaxUploadBytes = 12 }), Open(1, "/a.txt", CurlAppend), Write(2, 0, 0, "x"), Close(3, 0));

        CollectionAssert.AreEqual(Status(2, 4, "File too large"), run.Replies[1]);
        Assert.AreEqual("hello world\n", TextOf("a.txt"));
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task Upload_WriteAtAnOffsetNoFileCouldReach_IsFileTooLarge()
    {
        var run = await RunAsync(WritableStore(), Open(1, "/new.txt", CurlUpload), Write(2, 0, ulong.MaxValue - 1, "xy"));

        CollectionAssert.AreEqual(Status(2, 4, "File too large"), run.Replies[1]);
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task Upload_TheStoreFailsToWrite_IsWriteFailedAndDiscarded()
    {
        var store = Store(UploadsOn, files = new InMemoryContentFileSystem(clock, maxTotalBytes: 40));

        var run = await RunAsync(store, Open(1, "/new.txt", CurlUpload), Write(2, 0, 0, new string('x', 30)), Write(3, 0, 0, "x"), Close(4, 0));

        CollectionAssert.AreEqual(Status(2, 4, "Write failed"), run.Replies[1]);
        CollectionAssert.AreEqual(Status(3, 4, "Write failed"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 4, "Write failed"), run.Replies[3]);
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
        CollectionAssert.Contains(run.Notes, "SFTP WRITE /new.txt -> FAILURE: The in-memory file system is full.");
    }

    [TestMethod]
    [DataRow(typeof(IOException), DisplayName = "a disk fault")]
    [DataRow(typeof(UnauthorizedAccessException), DisplayName = "access refused")]
    public async Task Upload_TheStoreFailsToOpen_IsWriteFailedWithTheMessageInTheNoteOnly(Type failureType)
    {
        var failure = (Exception)Activator.CreateInstance(failureType, @"disk C:\secret failed")!;
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestWriteFailingContentFileSystem(StandardFiles(), failure), UploadsOn);

        var run = await RunAsync(store, Open(1, "/new.txt", CurlUpload), Request(9, 2, Text("/a.txt"), UInt32(1), UInt64(3)));

        CollectionAssert.AreEqual(Status(1, 4, "Write failed"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 4, "Write failed"), run.Replies[1]);
        CollectionAssert.Contains(run.Notes, @"SFTP OPEN /new.txt WRITE|CREAT|TRUNC -> FAILURE: disk C:\secret failed");
    }

    [TestMethod]
    public async Task Upload_TheStoreFailsToCommit_IsWriteFailed()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestChangeFailingContentFileSystem(StandardFiles(), new IOException("rename failed")), UploadsOn);

        var run = await RunAsync(store, Open(1, "/new.txt", CurlUpload), Write(2, 0, 0, "x"), Close(3, 0));

        CollectionAssert.AreEqual(Status(3, 4, "Write failed"), run.Replies[2]);
        CollectionAssert.Contains(run.Notes, "SFTP CLOSE /new.txt: upload discarded: rename failed");
    }

    [TestMethod]
    public async Task Upload_WhoseTargetBecameADirectory_IsWriteFailedAtClose()
    {
        var run = await RunAsync(WritableStore(), Open(1, "/new.txt", CurlUpload), Request(14, 2, Text("/new.txt"), UInt32(0)), Close(3, 0));

        CollectionAssert.AreEqual(Status(2, 0, "Success"), run.Replies[1]);
        CollectionAssert.AreEqual(Status(3, 4, "Write failed"), run.Replies[2]);
        CollectionAssert.Contains(run.Notes, "SFTP CLOSE /new.txt: upload discarded: the target can no longer take a file");
    }

    [TestMethod]
    public async Task Upload_NotClosedWhenTheSessionEnds_IsDiscarded()
    {
        var run = await RunAsync(WritableStore(), Open(1, "/new.txt", CurlUpload), Write(2, 0, 0, "x"), Open(3, "/a.txt", CurlAppend), Open(4, "/b.txt", CurlUpload), Close(5, 2));

        CollectionAssert.AreEqual(new[] { ".hidden.txt", ".surl", "a.txt", "b.txt", "dir" }, RootNames());
        Assert.AreEqual("SFTP session ended: client EOF, 2 uploads discarded", run.Notes[^1]);
    }

    [TestMethod]
    public async Task Upload_WhoseTemporaryFileCannotBeDeleted_DoesNotKeepTheOthersOrTheSessionEndBack()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestChangeFailingContentFileSystem(StandardFiles(), new IOException("locked"), failsDeletes: true), UploadsOn);

        var run = await RunAsync(store, Open(1, "/new.txt", CurlUpload), Open(2, "/b.txt", CurlUpload));

        CollectionAssert.IsSubsetOf(
            new[] { "SFTP upload /new.txt not discarded cleanly: locked", "SFTP upload /b.txt not discarded cleanly: locked" },
            run.Notes);
        Assert.AreEqual("SFTP session ended: client EOF, 2 uploads discarded", run.Notes[^1]);
    }

    [TestMethod]
    public async Task Upload_WhenTheChannelFails_IsDiscardedAndTheFailureGoesOn()
    {
        var channel = new FailingChannel(Init3, Open(1, "/new.txt", CurlUpload), Write(2, 0, 0, "x"));
        var session = new SftpSession(WritableStore(), Context(clock, TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<IOException>(() => session.RunAsync(channel, TestContext.CancellationToken));

        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    [DataRow("/nodir/new.txt", CurlUpload, 2u, "No such file", DisplayName = "the directory missing")]
    [DataRow("/missing.txt", CurlResume, 2u, "No such file", DisplayName = "no CREAT and nothing there")]
    [DataRow("/a:b", CurlUpload, 2u, "No such file", DisplayName = "a path the store refuses")]
    [DataRow("/dir", CurlUpload, 4u, "Is a directory", DisplayName = "a directory")]
    [DataRow("/a.txt", 0x2Au, 4u, "File already exists", DisplayName = "CREAT and EXCL on a file")]
    [DataRow("/.hidden.txt", CurlUpload, 3u, "Permission denied", DisplayName = "a hidden file")]
    [DataRow("/.surl/new.txt", CurlUpload, 3u, "Permission denied", DisplayName = "the service state folder")]
    [DataRow("/a.txt/", CurlUpload, 3u, "Permission denied", DisplayName = "a trailing slash")]
    [DataRow("/new.txt", 0x12u, 5u, "Bad message", DisplayName = "TRUNC without CREAT")]
    [DataRow("/new.txt", 0x22u, 5u, "Bad message", DisplayName = "EXCL without CREAT")]
    [DataRow("/new.txt", 0x42u, 8u, "Operation unsupported", DisplayName = "a bit draft-02 does not define")]
    public async Task Upload_ThatCannotOpen_IsRefusedAndLeavesNothing(string path, uint flags, uint code, string message)
    {
        var run = await RunAsync(WritableStore(), Open(1, path, flags));

        CollectionAssert.AreEqual(Status(1, code, message), run.Replies.Single());
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
        Assert.AreEqual("hello world\n", TextOf("a.txt"));
    }

    [TestMethod]
    public async Task Upload_PastTheHandleLimit_IsRefusedBeforeItOpens()
    {
        var opens = Enumerable.Range(1, 100).Select(id => Open((uint)id, "/a.txt")).ToArray();

        var run = await RunAsync(WritableStore(), [.. opens, Open(101, "/new.txt", CurlUpload)]);

        CollectionAssert.AreEqual(Status(101, 4, "Too many open handles"), run.Replies[100]);
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task EveryWrite_WithoutAllowUploads_IsPermissionDeniedAndChangesNothing()
    {
        var run = await RunAsync(
            WritableStore(new()),
            Open(1, "/new.txt", CurlUpload),
            Request(13, 2, Text("/a.txt")),
            Request(18, 3, Text("/a.txt"), Text("/c.txt")),
            Request(14, 4, Text("/x"), UInt32(0)),
            Request(15, 5, Text("/dir")),
            Request(9, 6, Text("/a.txt"), Times(1)),
            Request(9, 7, Text("/a.txt"), Sizes(1)),
            Open(8, "/a.txt"),
            Request(10, 9, Handle(0), Times(1)));

        for (var id = 1; id <= 7; id++)
        {
            CollectionAssert.AreEqual(Status((uint)id, 3, "Permission denied"), run.Replies[id - 1]);
        }

        CollectionAssert.AreEqual(Status(9, 3, "Permission denied"), run.Replies[8]);
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
        CollectionAssert.Contains(run.Notes, "SFTP OPEN /new.txt WRITE|CREAT|TRUNC -> PERMISSION_DENIED: uploads are off (--allow-uploads)");
    }

    [TestMethod]
    public async Task Remove_File_DeletesIt()
    {
        var run = await RunAsync(WritableStore(), Request(13, 1, Text("/a.txt")));

        CollectionAssert.AreEqual(Status(1, 0, "Success"), run.Replies.Single());
        CollectionAssert.AreEqual(new[] { ".hidden.txt", ".surl", "dir" }, RootNames());
        CollectionAssert.Contains(run.Notes, "SFTP REMOVE /a.txt -> OK");
    }

    [TestMethod]
    [DataRow("/missing.txt", 2u, "No such file", DisplayName = "a missing file")]
    [DataRow("/.hidden.txt", 2u, "No such file", DisplayName = "a hidden file")]
    [DataRow("/a:b", 2u, "No such file", DisplayName = "a path the store refuses")]
    [DataRow("/dir", 4u, "Is a directory", DisplayName = "a directory")]
    public async Task Remove_AnythingButAFile_IsRefused(string path, uint code, string message)
    {
        var run = await RunAsync(WritableStore(), Request(13, 1, Text(path)));

        CollectionAssert.AreEqual(Status(1, code, message), run.Replies.Single());
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task Rename_ToAFreeName_MovesTheEntry()
    {
        var run = await RunAsync(WritableStore(), Request(18, 1, Text("/a.txt"), Text("//dir/../c.txt")), Request(18, 2, Text("/dir"), Text("/d")));

        CollectionAssert.AreEqual(Status(1, 0, "Success"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 0, "Success"), run.Replies[1]);
        CollectionAssert.AreEqual(new[] { ".hidden.txt", ".surl", "c.txt", "d" }, RootNames());
        Assert.AreEqual("hello world\n", TextOf("c.txt"));
        CollectionAssert.Contains(run.Notes, "SFTP RENAME /a.txt //dir/../c.txt -> OK");
    }

    [TestMethod]
    [DataRow("/a.txt", "/dir/b.txt", 4u, "File already exists", DisplayName = "onto an existing file")]
    [DataRow("/a.txt", "/dir", 4u, "File already exists", DisplayName = "onto a directory")]
    [DataRow("/missing.txt", "/c.txt", 2u, "No such file", DisplayName = "a missing source")]
    [DataRow("/.hidden.txt", "/c.txt", 2u, "No such file", DisplayName = "a hidden source")]
    [DataRow("/a.txt", "/nodir/c.txt", 2u, "No such file", DisplayName = "into a missing directory")]
    [DataRow("/a:b", "/c.txt", 2u, "No such file", DisplayName = "a source the store refuses")]
    [DataRow("/a.txt", "/a:b", 2u, "No such file", DisplayName = "a target the store refuses")]
    [DataRow("/a.txt", "/.c.txt", 3u, "Permission denied", DisplayName = "to a hidden name")]
    [DataRow("/a.txt", "/.surl/a.txt", 3u, "Permission denied", DisplayName = "into the service state folder")]
    [DataRow("/", "/root", 3u, "Permission denied", DisplayName = "the root")]
    [DataRow("/dir", "/dir/inner", 3u, "Permission denied", DisplayName = "a directory into itself")]
    public async Task Rename_ThatCannotBeMade_IsRefusedAndChangesNothing(string from, string to, uint code, string message)
    {
        var run = await RunAsync(WritableStore(), Request(18, 1, Text(from), Text(to)));

        CollectionAssert.AreEqual(Status(1, code, message), run.Replies.Single());
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task Rename_TheStoreFails_IsWriteFailedWithTheMessageInTheNoteOnly()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestChangeFailingContentFileSystem(StandardFiles(), new IOException(@"C:\secret locked")), UploadsOn);

        var run = await RunAsync(store, Request(18, 1, Text("/a.txt"), Text("/c.txt")));

        CollectionAssert.AreEqual(Status(1, 4, "Write failed"), run.Replies.Single());
        CollectionAssert.Contains(run.Notes, @"SFTP RENAME -> FAILURE: C:\secret locked");
    }

    [TestMethod]
    public async Task MakeDirectory_AsCurlSendsIt_CreatesItAndNotesThePermissions()
    {
        var run = await RunAsync(WritableStore(), Request(14, 1, Text("/new"), UInt32(4), UInt32(CurlDirectoryMode)), Request(14, 2, Text("/new/deep"), UInt32(0)));

        CollectionAssert.AreEqual(Status(1, 0, "Success"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 0, "Success"), run.Replies[1]);
        Assert.AreEqual(ContentEntryKind.Directory, files.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "new", "deep")));
        CollectionAssert.IsSubsetOf(new[] { "SFTP MKDIR /new -> OK", "SFTP MKDIR /new: permissions 40755 not kept" }, run.Notes);
        Assert.IsFalse(run.Notes.Any(note => note.StartsWith("SFTP MKDIR /new/deep:", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("/dir", 4u, "File already exists", DisplayName = "an existing directory")]
    [DataRow("/a.txt", 4u, "File already exists", DisplayName = "an existing file")]
    [DataRow("/nodir/new", 2u, "No such file", DisplayName = "the parent missing")]
    [DataRow("/a:b", 2u, "No such file", DisplayName = "a path the store refuses")]
    [DataRow("/.new", 3u, "Permission denied", DisplayName = "a hidden name")]
    [DataRow("/.surl/new", 3u, "Permission denied", DisplayName = "under the service state folder")]
    public async Task MakeDirectory_ThatCannotBeMade_IsRefused(string path, uint code, string message)
    {
        var run = await RunAsync(WritableStore(), Request(14, 1, Text(path), UInt32(4), UInt32(CurlDirectoryMode)));

        CollectionAssert.AreEqual(Status(1, code, message), run.Replies.Single());
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
        Assert.IsFalse(run.Notes.Any(note => note.Contains("not kept", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RemoveDirectory_Empty_RemovesIt()
    {
        var run = await RunAsync(WritableStore(), Request(14, 1, Text("/x"), UInt32(0)), Request(15, 2, Text("/x")));

        CollectionAssert.AreEqual(Status(2, 0, "Success"), run.Replies[1]);
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
        CollectionAssert.Contains(run.Notes, "SFTP RMDIR /x -> OK");
    }

    [TestMethod]
    [DataRow("/dir", 4u, "Directory not empty", DisplayName = "a directory holding a file")]
    [DataRow("/a.txt", 4u, "Not a directory", DisplayName = "a file")]
    [DataRow("/missing", 2u, "No such file", DisplayName = "a missing directory")]
    [DataRow("/.surl", 2u, "No such file", DisplayName = "the service state folder")]
    [DataRow("/", 3u, "Permission denied", DisplayName = "the root")]
    public async Task RemoveDirectory_ThatCannotBeRemoved_IsRefused(string path, uint code, string message)
    {
        var run = await RunAsync(WritableStore(), Request(15, 1, Text(path)));

        CollectionAssert.AreEqual(Status(1, code, message), run.Replies.Single());
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task RemoveDirectory_HoldingOnlyAHiddenEntry_IsDirectoryNotEmpty()
    {
        var store = WritableStore(UploadsOn, fileSystem => WriteFile(fileSystem, "x", "empty", ".keep"));

        var run = await RunAsync(store, Request(15, 1, Text("/empty")));

        CollectionAssert.AreEqual(Status(1, 4, "Directory not empty"), run.Replies.Single());
    }

    [TestMethod]
    [DataRow(0x00000004u, DisplayName = "chmod: PERMISSIONS")]
    [DataRow(0x00000002u, DisplayName = "chown and chgrp: UIDGID")]
    [DataRow(0x80000000u, DisplayName = "EXTENDED")]
    public async Task SetStat_PermissionsOrOwners_IsOperationUnsupportedAndChangesNothing(uint flags)
    {
        var attributes = flags switch
        {
            0x00000004u => Concat(UInt32(flags), UInt32(0x8180)),
            0x00000002u => Concat(UInt32(flags), UInt32(1000), UInt32(1000)),
            _ => Concat(UInt32(flags), UInt32(1), Text("x@example.com"), Text("y")),
        };

        var run = await RunAsync(WritableStore(), Request(9, 1, Text("/a.txt"), attributes), Open(2, "/a.txt"), Request(10, 3, Handle(0), attributes));

        CollectionAssert.AreEqual(Status(1, 8, "Permissions and owners are not kept"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(3, 8, "Permissions and owners are not kept"), run.Replies[2]);
    }

    [TestMethod]
    public async Task SetStat_NothingAskedFor_IsSuccessEvenWithoutAllowUploads()
    {
        var run = await RunAsync(WritableStore(new()), Request(9, 1, Text("/a.txt"), UInt32(0)), Request(9, 2, Text("/a.txt"), UInt32(0x10)));

        CollectionAssert.AreEqual(Status(1, 0, "Success"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 3, "Permission denied"), run.Replies[1]);
    }

    [TestMethod]
    [DataRow("/missing.txt", DisplayName = "a missing file")]
    [DataRow("/.hidden.txt", DisplayName = "a hidden file")]
    [DataRow("/a:b", DisplayName = "a path the store refuses")]
    public async Task SetStat_AnythingAnsweredAsAbsent_IsNoSuchFileEvenForPermissions(string path)
    {
        var run = await RunAsync(WritableStore(), Request(9, 1, Text(path), UInt32(4), UInt32(0x8180)));

        CollectionAssert.AreEqual(Status(1, 2, "No such file"), run.Replies.Single());
    }

    [TestMethod]
    [DataRow("/a.txt", 12ul, RegularFile, DisplayName = "a file")]
    [DataRow("/dir", 0ul, Directory, DisplayName = "a directory")]
    public async Task SetStat_ModificationTime_SetsTheLastWriteTime(string path, ulong size, uint mode)
    {
        var time = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

        var run = await RunAsync(WritableStore(), Request(9, 1, Text(path), Times((uint)time.ToUnixTimeSeconds())), Request(17, 2, Text(path)));

        CollectionAssert.AreEqual(Status(1, 0, "Success"), run.Replies[0]);
        CollectionAssert.AreEqual(AttributesReply(2, size, mode, time), run.Replies[1]);
        CollectionAssert.Contains(run.Notes, $"SFTP SETSTAT {path} -> OK");
    }

    [TestMethod]
    [DataRow(5ul, "hello", DisplayName = "cut")]
    [DataRow(14ul, "hello world\n\0\0", DisplayName = "grown with zero bytes")]
    public async Task SetStat_Size_ResizesTheFile(ulong size, string text)
    {
        var run = await RunAsync(WritableStore(), Request(9, 1, Text("/a.txt"), Sizes(size)));

        CollectionAssert.AreEqual(Status(1, 0, "Success"), run.Replies.Single());
        Assert.AreEqual(text, TextOf("a.txt"));
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task SetStat_SizeAndTime_AppliesBoth()
    {
        var run = await RunAsync(WritableStore(), Request(9, 1, Text("/a.txt"), UInt32(9), UInt64(5), UInt32(0), UInt32(1000)), Request(17, 2, Text("/a.txt")));

        CollectionAssert.AreEqual(AttributesReply(2, 5, RegularFile, DateTimeOffset.FromUnixTimeSeconds(1000)), run.Replies[1]);
    }

    [TestMethod]
    [DataRow("/a.txt", 13ul, "File too large", DisplayName = "past --max-filesize")]
    [DataRow("/a.txt", ulong.MaxValue, "File too large", DisplayName = "past any file")]
    [DataRow("/dir", 1ul, "Is a directory", DisplayName = "a directory")]
    public async Task SetStat_SizeThatCannotBeSet_IsRefusedAndChangesNothing(string path, ulong size, string message)
    {
        var run = await RunAsync(WritableStore(UploadsOn with { MaxUploadBytes = 12 }), Request(9, 1, Text(path), Sizes(size)));

        CollectionAssert.AreEqual(Status(1, 4, message), run.Replies.Single());
        Assert.AreEqual("hello world\n", TextOf("a.txt"));
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task SetStat_TheStoreFailsToCommitASize_IsWriteFailed()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestChangeFailingContentFileSystem(StandardFiles(), new IOException("rename failed")), UploadsOn);

        var run = await RunAsync(store, Request(9, 1, Text("/a.txt"), Sizes(3)), Request(9, 2, Text("/a.txt"), Times(1)));

        CollectionAssert.AreEqual(Status(1, 4, "Write failed"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 4, "Write failed"), run.Replies[1]);
    }

    [TestMethod]
    public async Task HandleSetStat_OnAnUpload_ResizesItAndSetsItsTimeAtClose()
    {
        var run = await RunAsync(
            WritableStore(),
            Open(1, "/new.txt", CurlUpload),
            Write(2, 0, 0, "hello again"),
            Request(10, 3, Handle(0), UInt32(9), UInt64(5), UInt32(0), UInt32(1000)),
            Close(4, 0),
            Request(17, 5, Text("/new.txt")));

        CollectionAssert.AreEqual(Status(3, 0, "Success"), run.Replies[2]);
        CollectionAssert.AreEqual(AttributesReply(5, 5, RegularFile, DateTimeOffset.FromUnixTimeSeconds(1000)), run.Replies[4]);
        Assert.AreEqual("hello", TextOf("new.txt"));
        CollectionAssert.Contains(run.Notes, "SFTP FSETSTAT /new.txt -> OK");
    }

    [TestMethod]
    [DataRow(6ul, DisplayName = "past --max-filesize")]
    [DataRow(ulong.MaxValue, DisplayName = "past any file")]
    public async Task HandleSetStat_SizeTooLargeOnAnUpload_DiscardsIt(ulong size)
    {
        var run = await RunAsync(
            WritableStore(UploadsOn with { MaxUploadBytes = 5 }),
            Open(1, "/new.txt", CurlUpload),
            Request(10, 2, Handle(0), Sizes(size)),
            Request(10, 3, Handle(0), Times(1)),
            Close(4, 0));

        CollectionAssert.AreEqual(Status(2, 4, "File too large"), run.Replies[1]);
        CollectionAssert.AreEqual(Status(3, 4, "File too large"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 4, "File too large"), run.Replies[3]);
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task HandleSetStat_OnAReadOrDirectoryHandle_SetsTheTimeButNeverTheSize()
    {
        var run = await RunAsync(
            WritableStore(UploadsOn with { ListDirectories = true }),
            Open(1, "/a.txt"),
            Request(11, 2, Text("/dir")),
            Request(10, 3, Handle(0), Sizes(1)),
            Request(10, 4, Handle(1), Times(1000)),
            Request(10, 5, Handle(0), Times(2000)),
            Request(17, 6, Text("/dir")),
            Request(10, 7, Handle(7), Times(1)));

        CollectionAssert.AreEqual(Status(3, 4, "Handle not open for writing"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 0, "Success"), run.Replies[3]);
        CollectionAssert.AreEqual(Status(5, 0, "Success"), run.Replies[4]);
        CollectionAssert.AreEqual(AttributesReply(6, 0, Directory, DateTimeOffset.FromUnixTimeSeconds(1000)), run.Replies[5]);
        CollectionAssert.AreEqual(Status(7, 4, "Invalid handle"), run.Replies[6]);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(2000), files.GetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "a.txt")));
    }

    [TestMethod]
    public async Task HandleSetStat_WithoutATime_ChangesOnlyWhatItAsksFor()
    {
        var run = await RunAsync(
            WritableStore(),
            Open(1, "/new.txt", CurlUpload),
            Write(2, 0, 0, "hello"),
            Request(10, 3, Handle(0), Sizes(2)),
            Open(4, "/a.txt"),
            Request(10, 5, Handle(1), UInt32(0x10)),
            Close(6, 0));

        CollectionAssert.AreEqual(Status(3, 0, "Success"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(5, 0, "Success"), run.Replies[4]);
        Assert.AreEqual("he", TextOf("new.txt"));
        Assert.AreEqual(WrittenAt, files.GetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "a.txt")));
    }

    [TestMethod]
    public async Task Upload_WhenTheConnectionIsCutOff_IsNotAnsweredAndTheCancellationGoesOn()
    {
        using var cutOff = new CancellationTokenSource();
        await cutOff.CancelAsync();
        var channel = new SftpTestChannel(Init3, Open(1, "/new.txt", CurlUpload));
        var session = new SftpSession(WritableStore(), Context(clock, cutOff.Token));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => session.RunAsync(channel, cutOff.Token));

        Assert.HasCount(1, channel.WrittenPackets());
        CollectionAssert.AreEqual(StandardRootNames, RootNames());
    }

    [TestMethod]
    public async Task HandleSetStat_OnAReadHandleOfAFileSinceRemoved_IsNoSuchFile()
    {
        var run = await RunAsync(WritableStore(), Open(1, "/a.txt"), Request(13, 2, Text("/a.txt")), Request(10, 3, Handle(0), Times(1)));

        CollectionAssert.AreEqual(Status(3, 2, "No such file"), run.Replies[2]);
    }

    [TestMethod]
    public async Task QuoteCommands_AsCurlSendsThem_AreAnsweredAsTheAdrDecides()
    {
        // ADR-0054 decision 11: rename, rm, mkdir, rmdir, mtime and atime succeed; chmod, chown,
        // ln and statvfs are refused, so curl exits 21 for each.
        var run = await RunAsync(
            WritableStore(),
            Request(18, 1, Text("//a.txt"), Text("//c.txt")),
            Request(14, 2, Text("/x"), UInt32(4), UInt32(CurlDirectoryMode)),
            Request(15, 3, Text("/x")),
            Request(17, 4, Text("/c.txt")),
            Request(9, 5, Text("/c.txt"), UInt32(8), UInt32((uint)WrittenAt.ToUnixTimeSeconds()), UInt32(1790000000)),
            Request(9, 6, Text("/c.txt"), UInt32(8), UInt32(1790000001), UInt32(1790000000)),
            Request(9, 7, Text("/c.txt"), UInt32(4), UInt32(0x8180)),
            Request(9, 8, Text("/c.txt"), UInt32(2), UInt32(1000), UInt32(1000)),
            Request(20, 9, Text("/c.txt"), Text("/l.txt")),
            Request(200, 10, Text("statvfs@openssh.com"), Text("/")),
            Request(13, 11, Text("/c.txt")));

        foreach (var id in new uint[] { 1, 2, 3, 5, 6, 11 })
        {
            CollectionAssert.AreEqual(Status(id, 0, "Success"), run.Replies[(int)id - 1]);
        }

        CollectionAssert.AreEqual(Status(7, 8, "Permissions and owners are not kept"), run.Replies[6]);
        CollectionAssert.AreEqual(Status(8, 8, "Permissions and owners are not kept"), run.Replies[7]);
        CollectionAssert.AreEqual(Status(9, 8, "Symbolic links cannot be created"), run.Replies[8]);
        CollectionAssert.AreEqual(Status(10, 8, "Extension not supported"), run.Replies[9]);
        CollectionAssert.AreEqual(new[] { ".hidden.txt", ".surl", "dir" }, RootNames());
    }

    private ContentStore WritableStore(ContentExposureOptions? options = null, Action<InMemoryContentFileSystem>? addEntries = null) =>
        Store(options ?? UploadsOn, files = new InMemoryContentFileSystem(clock), addEntries);

    private InMemoryContentFileSystem StandardFiles()
    {
        files = new InMemoryContentFileSystem(clock);
        Store(new(), files);

        return files;
    }

    private string TextOf(params string[] segments)
    {
        using var stream = files.OpenFileForAsyncRead(Path.Join([InMemoryContentFileSystem.RootPath, .. segments]));
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private string[] RootNames() => [.. files.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).Order(StringComparer.Ordinal)];

    private static byte[] OpenCarrying(uint id, string path, uint flags, uint permissions) =>
        Request(3, id, Text(path), UInt32(flags), UInt32(4), UInt32(permissions));

    private static byte[] Write(uint id, uint handle, ulong offset, string data) => Request(6, id, Handle(handle), UInt64(offset), Text(data));

    private static byte[] Close(uint id, uint handle) => Request(4, id, Handle(handle));

    private static byte[] Times(uint modificationTime) => Concat(UInt32(8), UInt32(modificationTime), UInt32(modificationTime));

    private static byte[] Sizes(ulong size) => Concat(UInt32(1), UInt64(size));

    /// <summary>
    /// A scripted channel whose read throws <see cref="IOException"/> once its packets are spent,
    /// as a connection cut off mid-session does.
    /// </summary>
    private sealed class FailingChannel(params byte[][] inbound) : ISshChannelDataStream
    {
        private readonly SftpTestChannel inner = new(inbound);

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);

            return read == 0 ? throw new IOException("The connection was cut off.") : read;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => inner.WriteAsync(data, cancellationToken);
    }
}
