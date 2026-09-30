using System.Buffers.Binary;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SftpTestPackets;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The <c>sftp</c> subsystem's read side at channel level (ADR-0054, decisions 5 to 8 and 13):
/// every packet is built by hand from draft-ietf-secsh-filexfer-02 and the ADR's worked bytes, fed
/// to <see cref="SftpSession"/> as channel data, and its replies compared byte for byte. BL-172
/// proves these answers against the pinned upstream curl build (ADR-0003).
/// </summary>
[TestClass]
public sealed class SftpSessionTests
{
    private const uint RegularFile = 0x81A4;
    private const uint Directory = 0x41ED;

    private static readonly ContentExposureOptions ListingOn = new() { ListDirectories = true };

    private static readonly HashSet<string> FixedMessages =
    [
        "Success", "End of file", "No such file", "Permission denied", "File already exists", "Directory not empty",
        "Is a directory", "Not a directory", "Not a symbolic link", "File too large", "Handle not open for writing",
        "Handle not open for reading", "Too many open handles", "Invalid handle", "Write failed", "Read failed",
        "Bad message", "Operation unsupported", "Permissions and owners are not kept", "Symbolic links cannot be created",
        "Extension not supported",
    ];

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Init_Version3_IsAnsweredVersion3WithNoExtensionsAndClientEofEndsWithExitStatus0()
    {
        var run = await RunRawAsync(Store(new(), clock), null, Framed(1, UInt32(3), Text("ext@example.com"), Text("1")));

        Assert.AreEqual(0u, run.Exit);
        CollectionAssert.AreEqual(Version3, run.Replies.Single());
        CollectionAssert.AreEqual(
            new[] { "SFTP session started: client version 3, answering 3", "SFTP session ended: client EOF" },
            run.Notes);
    }

    [TestMethod]
    public async Task Init_HigherVersion_IsAnsweredVersion3()
    {
        var run = await RunRawAsync(Store(new(), clock), null, Framed(1, UInt32(6)));

        CollectionAssert.AreEqual(Version3, run.Replies.Single());
        CollectionAssert.Contains(run.Notes, "SFTP session started: client version 6, answering 3");
    }

    [TestMethod]
    public async Task Eof_BeforeInit_EndsWithExitStatus0AndNoReply()
    {
        var run = await RunRawAsync(Store(new(), clock), null);

        Assert.AreEqual(0u, run.Exit);
        Assert.IsEmpty(run.Replies);
        CollectionAssert.AreEqual(new[] { "SFTP session ended: client EOF" }, run.Notes);
    }

    [TestMethod]
    public async Task Init_VersionBelow3_EndsWithExitStatus1AndNoReply()
    {
        var run = await RunRawAsync(Store(new(), clock), null, Framed(1, UInt32(2)));

        Assert.AreEqual(1u, run.Exit);
        Assert.IsEmpty(run.Replies);
        CollectionAssert.AreEqual(new[] { "SFTP session ended: client version 2 below 3" }, run.Notes);
    }

    [TestMethod]
    public async Task FirstPacket_NotInit_EndsWithExitStatus1AndNoReply()
    {
        var run = await RunRawAsync(Store(new(), clock), null, Request(17, 1, Text("/a.txt")));

        Assert.AreEqual(1u, run.Exit);
        Assert.IsEmpty(run.Replies);
        CollectionAssert.AreEqual(new[] { "SFTP session ended: malformed packet" }, run.Notes);
    }

    [TestMethod]
    public async Task Init_Again_IsBadMessageAndTheSessionGoesOn()
    {
        var run = await RunAsync(Store(new(), clock), Framed(1, UInt32(3)), Request(16, 2, Text(".")));

        CollectionAssert.AreEqual(Status(3, 5, "Bad message"), run.Replies[0]);
        Assert.AreEqual(104, run.Replies[1][4]);
    }

    [TestMethod]
    public async Task RealPath_Dot_IsTheRootAsTheAdrWorksIt()
    {
        var run = await RunAsync(Store(new(), clock), Request(16, 1, Text(".")));

        CollectionAssert.AreEqual(
            new byte[] { 0, 0, 0, 0x17, 0x68, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0x2F, 0, 0, 0, 1, 0x2F, 0, 0, 0, 0 },
            run.Replies.Single());
    }

    [TestMethod]
    [DataRow("", "/", DisplayName = "the empty path is the home directory")]
    [DataRow("a.txt", "/a.txt", DisplayName = "a relative path resolves against /")]
    [DataRow("//x", "/x", DisplayName = "repeated slashes collapse")]
    [DataRow("/../x", "/x", DisplayName = "a climb above / stops there")]
    [DataRow("/a/./b/../c/", "/a/c", DisplayName = "dot segments resolve and the trailing slash goes")]
    [DataRow("/missing/deep", "/missing/deep", DisplayName = "existence is not checked")]
    [DataRow("/dir/ü.txt", "/dir/ü.txt", DisplayName = "UTF-8 is kept")]
    public async Task RealPath_ResolvesTheCanonicalPathWithoutCheckingIt(string path, string canonical)
    {
        var run = await RunAsync(Store(new(), clock), Request(16, 9, Text(path)));

        CollectionAssert.AreEqual(Framed(104, UInt32(9), UInt32(1), Text(canonical), Text(canonical), UInt32(0)), run.Replies.Single());
    }

    [TestMethod]
    public async Task RealPath_NotUtf8_IsNoSuchFile()
    {
        var run = await RunAsync(Store(new(), clock), Request(16, 4, Bytes([0x2F, 0xFF])));

        CollectionAssert.AreEqual(Status(4, 2, "No such file"), run.Replies.Single());
        CollectionAssert.Contains(run.Notes, @"SFTP REALPATH /\xFF -> NO_SUCH_FILE: answered as absent");
    }

    [TestMethod]
    public async Task Stat_File_IsItsAttributesAsTheAdrWorksThem()
    {
        var run = await RunAsync(Store(new(), clock), Request(17, 7, Text("/a.txt")));

        CollectionAssert.AreEqual(
            new byte[] { 0, 0, 0, 0x1D, 0x69, 0, 0, 0, 7, 0, 0, 0, 0x0D, 0, 0, 0, 0, 0, 0, 0, 0x0C, 0, 0, 0x81, 0xA4, 0x6A, 0xB9, 0x0D, 0x70, 0x6A, 0xB9, 0x0D, 0x70 },
            run.Replies.Single());
        Assert.IsFalse(run.Notes.Any(note => note.Contains("STAT", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow((byte)17, "/dir", DisplayName = "STAT")]
    [DataRow((byte)7, "dir/", DisplayName = "LSTAT, with a trailing slash")]
    [DataRow((byte)17, "/dir/../dir/.", DisplayName = "STAT through dot segments")]
    public async Task Stat_Directory_IsItsAttributesWithSize0(byte type, string path)
    {
        var run = await RunAsync(Store(new(), clock), Request(type, 2, Text(path)));

        CollectionAssert.AreEqual(AttributesReply(2, 0, Directory, WrittenAt), run.Replies.Single());
    }

    [TestMethod]
    public async Task Stat_Root_IsADirectory()
    {
        var run = await RunAsync(Store(new(), clock), Request(17, 2, Text("/")));

        Assert.AreEqual(105, run.Replies.Single()[4]);
        CollectionAssert.AreEqual(UInt32(Directory), run.Replies.Single()[21..25]);
    }

    [TestMethod]
    [DataRow("/missing.txt", DisplayName = "a missing file")]
    [DataRow("/.hidden.txt", DisplayName = "a hidden file")]
    [DataRow("/.surl/lock", DisplayName = "the service state folder")]
    [DataRow("/.surl", DisplayName = "the service state folder itself")]
    [DataRow("/a.txt/", DisplayName = "a file named as a directory")]
    [DataRow("/a:b", DisplayName = "a path the store refuses")]
    [DataRow("/a\0b", DisplayName = "a NUL")]
    public async Task Stat_AnythingAnsweredAsAbsent_IsNoSuchFile(string path)
    {
        var run = await RunAsync(Store(new(), clock), Request(17, 3, Text(path)), Request(7, 4, Text(path)));

        CollectionAssert.AreEqual(Status(3, 2, "No such file"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(4, 2, "No such file"), run.Replies[1]);
    }

    [TestMethod]
    public async Task Stat_HiddenFileWithDotFilesServed_IsItsAttributes()
    {
        var run = await RunAsync(Store(new() { ServeDotFiles = true }, clock), Request(17, 3, Text("/.hidden.txt")), Request(17, 4, Text("/.surl/lock")));

        Assert.AreEqual(105, run.Replies[0][4]);
        CollectionAssert.AreEqual(Status(4, 2, "No such file"), run.Replies[1]);
    }

    [TestMethod]
    public async Task Stat_BeforeTheEpoch_ClampsTheTimesTo0()
    {
        var store = Store(new(), clock, fileSystem =>
        {
            WriteFile(fileSystem, "old", "old.txt");
            fileSystem.SetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "old.txt"), new DateTimeOffset(1960, 1, 1, 0, 0, 0, TimeSpan.Zero));
        });

        var run = await RunAsync(store, Request(17, 3, Text("/old.txt")));

        CollectionAssert.AreEqual(Framed(105, UInt32(3), UInt32(0x0D), UInt64(3), UInt32(RegularFile), UInt32(0), UInt32(0)), run.Replies.Single());
    }

    [TestMethod]
    public async Task OpenReadClose_File_ServesItsBytesThenEofAndCountsThem()
    {
        var run = await RunAsync(
            Store(new(), clock),
            Open(1, "/a.txt"),
            Read(2, 0, 0, 30000),
            Read(3, 0, 12, 30000),
            Read(4, 0, 6, 5),
            Read(5, 0, 1000, 1),
            Request(8, 6, Handle(0)),
            Request(4, 7, Handle(0)));

        CollectionAssert.AreEqual(HandleReply(1, 0), run.Replies[0]);
        CollectionAssert.AreEqual(DataReply(2, "hello world\n"), run.Replies[1]);
        CollectionAssert.AreEqual(Status(3, 1, "End of file"), run.Replies[2]);
        CollectionAssert.AreEqual(DataReply(4, "world"), run.Replies[3]);
        CollectionAssert.AreEqual(Status(5, 1, "End of file"), run.Replies[4]);
        CollectionAssert.AreEqual(AttributesReply(6, 12, RegularFile, WrittenAt), run.Replies[5]);
        CollectionAssert.AreEqual(Status(7, 0, "Success"), run.Replies[6]);
        CollectionAssert.IsSubsetOf(new[] { "SFTP OPEN /a.txt READ -> HANDLE", "SFTP CLOSE /a.txt: read 17 bytes" }, run.Notes);
    }

    [TestMethod]
    public async Task Read_ZeroBytesAskedFor_IsEmptyData()
    {
        var run = await RunAsync(Store(new(), clock), Open(1, "/a.txt"), Read(2, 0, 0, 0));

        CollectionAssert.AreEqual(DataReply(2, string.Empty), run.Replies[1]);
    }

    [TestMethod]
    public async Task Read_LargeFile_IsCutAt261120BytesAReply()
    {
        var store = Store(new(), clock, fileSystem => WriteFile(fileSystem, new string('x', 300000), "big.bin"));

        var run = await RunAsync(store, Open(1, "/big.bin"), Read(2, 0, 0, 400000), Read(3, 0, 261120, 400000));

        Assert.AreEqual(4 + 1 + 4 + 4 + 261120, run.Replies[1].Length);
        Assert.AreEqual(4 + 1 + 4 + 4 + 38880, run.Replies[2].Length);
    }

    [TestMethod]
    public async Task Open_FlagsZero_OpensForReading()
    {
        var run = await RunAsync(Store(new(), clock), Open(1, "/a.txt", flags: 0));

        CollectionAssert.AreEqual(HandleReply(1, 0), run.Replies.Single());
        CollectionAssert.Contains(run.Notes, "SFTP OPEN /a.txt 0 -> HANDLE");
    }

    [TestMethod]
    public async Task Open_WithEveryAttributeCurlCouldSend_ParsesThemAndIgnoresThem()
    {
        var attributes = Concat(UInt32(0x8000000F), UInt64(5), UInt32(1000), UInt32(1000), UInt32(0x81A4), UInt32(1), UInt32(2), UInt32(1), Text("x@example.com"), Text("y"));

        var run = await RunAsync(Store(new(), clock), Request(3, 1, Text("/a.txt"), UInt32(1), attributes));

        CollectionAssert.AreEqual(HandleReply(1, 0), run.Replies.Single());
    }

    [TestMethod]
    [DataRow("/missing.txt", DisplayName = "a missing file")]
    [DataRow("/.hidden.txt", DisplayName = "a hidden file")]
    [DataRow("/.surl/lock", DisplayName = "the service state folder")]
    public async Task Open_AnythingAnsweredAsAbsent_IsNoSuchFile(string path)
    {
        var run = await RunAsync(Store(new(), clock), Open(1, path));

        CollectionAssert.AreEqual(Status(1, 2, "No such file"), run.Replies.Single());
        CollectionAssert.Contains(run.Notes, $"SFTP OPEN {path} READ -> NO_SUCH_FILE: answered as absent");
    }

    [TestMethod]
    public async Task Open_Directory_IsFailureIsADirectory()
    {
        var run = await RunAsync(Store(new(), clock), Open(1, "/dir"));

        CollectionAssert.AreEqual(Status(1, 4, "Is a directory"), run.Replies.Single());
    }

    [TestMethod]
    [DataRow(0x1Au, "WRITE|CREAT|TRUNC", DisplayName = "curl's upload")]
    [DataRow(0x0Eu, "WRITE|APPEND|CREAT", DisplayName = "curl's append")]
    [DataRow(0x21u, "READ|EXCL", DisplayName = "EXCL")]
    [DataRow(0x41u, "READ|0x40", DisplayName = "a bit draft-02 does not define")]
    public async Task Open_AnyFlagButRead_IsOperationUnsupportedUntilTheWriteSide(uint flags, string rendered)
    {
        var run = await RunAsync(Store(new() { AllowUploads = true }, clock), Open(1, "/new.txt", flags));

        CollectionAssert.AreEqual(Status(1, 8, "Operation unsupported"), run.Replies.Single());
        CollectionAssert.Contains(run.Notes, $"SFTP OPEN /new.txt {rendered} -> OP_UNSUPPORTED: only reads are served");
    }

    [TestMethod]
    public async Task Read_FileGoneSinceOpen_IsNoSuchFile()
    {
        InMemoryContentFileSystem? files = null;
        var store = Store(new(), clock, fileSystem => files = fileSystem);

        // The channel deletes the file just before the READ's length field is read.
        var deleting = new DeletingChannel(files!, Path.Join(InMemoryContentFileSystem.RootPath, "a.txt"), Init3, Open(1, "/a.txt"), Read(2, 0, 0, 10), Request(8, 3, Handle(0)));
        await new SftpSession(store, Context(clock, TestContext.CancellationToken)).RunAsync(deleting, TestContext.CancellationToken);

        var replies = deleting.WrittenPackets();
        CollectionAssert.AreEqual(Status(2, 2, "No such file"), replies[2]);
        CollectionAssert.AreEqual(Status(3, 2, "No such file"), replies[3]);
    }

    [TestMethod]
    public async Task Read_StoreFailure_IsFailureReadFailedWithTheMessageInTheNoteOnly()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestThrowingContentFileSystem(clock, new IOException(@"disk C:\secret\a.txt failed")), new());

        var run = await RunAsync(store, Open(1, "/a.txt"), Read(2, 0, 0, 10));

        CollectionAssert.AreEqual(Status(2, 4, "Read failed"), run.Replies[1]);
        CollectionAssert.Contains(run.Notes, @"SFTP READ -> FAILURE: disk C:\secret\a.txt failed");
    }

    [TestMethod]
    public async Task Read_FileEmptiedSinceItsLengthWasRead_IsEofNotEmptyData()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestShrinkingContentFileSystem(clock), new());

        var run = await RunAsync(store, Open(1, "/a.txt"), Read(2, 0, 0, 10));

        CollectionAssert.AreEqual(Status(2, 1, "End of file"), run.Replies[1]);
    }

    [TestMethod]
    public async Task Packet_LargerThanTheFirstBuffer_IsReadWhole()
    {
        var path = "/" + new string('p', 200000);

        var run = await RunAsync(Store(new(), clock), Request(16, 1, Text(path)));

        CollectionAssert.AreEqual(Framed(104, UInt32(1), UInt32(1), Text(path), Text(path), UInt32(0)), run.Replies.Single());
    }

    [TestMethod]
    public async Task Packet_LargerThanTheFirstBufferCutOff_EndsTheSession()
    {
        var packet = Request(16, 1, Text("/" + new string('p', 200000)));

        var run = await RunRawAsync(Store(new(), clock), null, Init3, packet[..100000]);

        Assert.AreEqual(1u, run.Exit);
        CollectionAssert.Contains(run.Notes, "SFTP session ended: malformed packet");
    }

    [TestMethod]
    public async Task Stat_AccessDenied_IsFailureReadFailed()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestThrowingContentFileSystem(clock, new UnauthorizedAccessException("denied")), new());

        var run = await RunAsync(store, Request(17, 2, Text("/a.txt")));

        CollectionAssert.AreEqual(Status(2, 4, "Read failed"), run.Replies.Single());
    }

    [TestMethod]
    public async Task OpenDir_WithoutListDirectories_IsNoSuchFileWhateverIsThere()
    {
        var run = await RunAsync(Store(new(), clock), Request(11, 1, Text("/dir/")), Request(11, 2, Text("/")));

        CollectionAssert.AreEqual(Status(1, 2, "No such file"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 2, "No such file"), run.Replies[1]);
        CollectionAssert.Contains(run.Notes, "SFTP OPENDIR /dir/ -> NO_SUCH_FILE: listings are off (--list-directories)");
    }

    [TestMethod]
    public async Task OpenDirReadDir_Directory_ListsItsEntriesInLongNameFormThenEof()
    {
        var run = await RunAsync(
            Store(ListingOn, clock),
            Request(11, 1, Text("/dir/")),
            Request(12, 2, Handle(0)),
            Request(12, 3, Handle(0)),
            Request(12, 4, Handle(0)),
            Request(8, 5, Handle(0)),
            Request(4, 6, Handle(0)));

        CollectionAssert.AreEqual(HandleReply(1, 0), run.Replies[0]);
        CollectionAssert.AreEqual(
            Framed(104, UInt32(2), UInt32(1), Text("b.txt"), Text("-rw-r--r-- 1 surl surl" + "            4" + " Sep 27 12:34 b.txt"), Attributes(4, RegularFile, WrittenAt)),
            run.Replies[1]);
        CollectionAssert.AreEqual(Status(3, 1, "End of file"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 1, "End of file"), run.Replies[3]);
        CollectionAssert.AreEqual(AttributesReply(5, 0, Directory, WrittenAt), run.Replies[4]);
        CollectionAssert.AreEqual(Status(6, 0, "Success"), run.Replies[5]);
        CollectionAssert.IsSubsetOf(new[] { "SFTP OPENDIR /dir/ -> HANDLE", "SFTP CLOSE /dir/: listed 1 entries" }, run.Notes);
    }

    [TestMethod]
    public async Task ReadDir_Root_LeavesOutHiddenEntriesAndTheServiceStateFolder()
    {
        var run = await RunAsync(Store(ListingOn, clock), Request(11, 1, Text("/")), Request(12, 2, Handle(0)));

        CollectionAssert.AreEqual(
            Framed(
                104,
                UInt32(2),
                UInt32(2),
                Text("a.txt"),
                Text("-rw-r--r-- 1 surl surl" + "           12" + " Sep 27 12:34 a.txt"),
                Attributes(12, RegularFile, WrittenAt),
                Text("dir"),
                Text("drwxr-xr-x 1 surl surl" + "            0" + " Sep 27 12:34 dir"),
                Attributes(0, Directory, WrittenAt)),
            run.Replies[1]);
    }

    [TestMethod]
    public async Task ReadDir_OldAndFutureWrites_ShowTheYear()
    {
        var store = Store(ListingOn, clock, fileSystem =>
        {
            WriteFile(fileSystem, "o", "old", "o.txt");
            WriteFile(fileSystem, "f", "old", "p.txt");
            fileSystem.SetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "old", "o.txt"), new DateTimeOffset(2025, 3, 4, 5, 6, 7, TimeSpan.Zero));
            fileSystem.SetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "old", "p.txt"), new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
        });

        var run = await RunAsync(store, Request(11, 1, Text("/old")), Request(12, 2, Handle(0)));

        var text = Encoding.UTF8.GetString(run.Replies[1]);
        StringAssert.Contains(text, "-rw-r--r-- 1 surl surl            1 Mar  4  2025 o.txt");
        StringAssert.Contains(text, "-rw-r--r-- 1 surl surl            1 Dec  1  2026 p.txt");
    }

    [TestMethod]
    public async Task ReadDir_ManyEntries_AreSentAtMost100AReply()
    {
        var store = Store(ListingOn, clock, fileSystem =>
        {
            for (var n = 0; n < 150; n++)
            {
                WriteFile(fileSystem, "x", "many", $"f{n:D3}.txt");
            }
        });

        var run = await RunAsync(store, Request(11, 1, Text("/many")), Request(12, 2, Handle(0)), Request(12, 3, Handle(0)), Request(12, 4, Handle(0)));

        Assert.AreEqual(100u, BinaryPrimitives.ReadUInt32BigEndian(run.Replies[1].AsSpan(9)));
        Assert.AreEqual(50u, BinaryPrimitives.ReadUInt32BigEndian(run.Replies[2].AsSpan(9)));
        CollectionAssert.AreEqual(Status(4, 1, "End of file"), run.Replies[3]);
    }

    [TestMethod]
    public async Task ReadDir_LongNames_AreSentWithin262144BytesAReply()
    {
        var store = Store(ListingOn, clock, fileSystem =>
        {
            for (var n = 0; n < 100; n++)
            {
                WriteFile(fileSystem, "x", "long", $"{n:D3}{new string('n', 1400)}");
            }
        });

        var run = await RunAsync(store, Request(11, 1, Text("/long")), Request(12, 2, Handle(0)), Request(12, 3, Handle(0)));

        var first = BinaryPrimitives.ReadUInt32BigEndian(run.Replies[1].AsSpan(9));
        Assert.IsLessThan(100u, first);
        Assert.IsLessThanOrEqualTo(SftpSession.MaxNamesPacketBytes, run.Replies[1].Length);
        Assert.AreEqual(100u - first, BinaryPrimitives.ReadUInt32BigEndian(run.Replies[2].AsSpan(9)));
    }

    [TestMethod]
    [DataRow("/a.txt", 4u, "Not a directory", DisplayName = "a file")]
    [DataRow("/missing/", 2u, "No such file", DisplayName = "a missing directory")]
    [DataRow("/.surl/", 2u, "No such file", DisplayName = "the service state folder")]
    [DataRow("/a:b/", 2u, "No such file", DisplayName = "a path the store refuses")]
    public async Task OpenDir_NotAListableDirectory_IsRefused(string path, uint code, string message)
    {
        var run = await RunAsync(Store(ListingOn, clock), Request(11, 1, Text(path)));

        CollectionAssert.AreEqual(Status(1, code, message), run.Replies.Single());
    }

    [TestMethod]
    public async Task HandleKinds_Crossed_AreRefused()
    {
        var run = await RunAsync(
            Store(ListingOn, clock),
            Request(11, 1, Text("/dir")),
            Open(2, "/a.txt"),
            Read(3, 0, 0, 10),
            Request(12, 4, Handle(1)));

        CollectionAssert.AreEqual(Status(3, 4, "Is a directory"), run.Replies[2]);
        CollectionAssert.AreEqual(Status(4, 4, "Not a directory"), run.Replies[3]);
    }

    [TestMethod]
    public async Task UnknownHandle_IsFailureInvalidHandleForEveryHandleRequest()
    {
        var run = await RunAsync(
            Store(new(), clock),
            Read(1, 99, 0, 10),
            Request(8, 2, Handle(99)),
            Request(12, 3, Handle(99)),
            Request(4, 4, Bytes([0, 0, 0])),
            Open(5, "/a.txt"),
            Request(4, 6, Handle(0)),
            Request(4, 7, Handle(0)));

        foreach (var (reply, id) in new[] { (0, 1u), (1, 2u), (2, 3u), (3, 4u), (6, 7u) })
        {
            CollectionAssert.AreEqual(Status(id, 4, "Invalid handle"), run.Replies[reply]);
        }

        CollectionAssert.Contains(run.Notes, @"SFTP CLOSE handle \x00\x00\x00 -> FAILURE");
    }

    [TestMethod]
    public async Task Handles_PastTheLimit_AreRefusedAndNeverReused()
    {
        var opens = Enumerable.Range(1, 101).Select(id => Open((uint)id, "/a.txt")).ToArray();

        var run = await RunAsync(Store(new(), clock), [.. opens, Request(4, 200, Handle(5)), Open(201, "/a.txt")]);

        CollectionAssert.AreEqual(HandleReply(100, 99), run.Replies[99]);
        CollectionAssert.AreEqual(Status(101, 4, "Too many open handles"), run.Replies[100]);
        CollectionAssert.AreEqual(Status(200, 0, "Success"), run.Replies[101]);
        CollectionAssert.AreEqual(HandleReply(201, 100), run.Replies[102]);
    }

    [TestMethod]
    [DataRow((byte)6, "WRITE", DisplayName = "WRITE")]
    [DataRow((byte)9, "SETSTAT", DisplayName = "SETSTAT")]
    [DataRow((byte)10, "FSETSTAT", DisplayName = "FSETSTAT")]
    [DataRow((byte)13, "REMOVE", DisplayName = "REMOVE")]
    [DataRow((byte)14, "MKDIR", DisplayName = "MKDIR")]
    [DataRow((byte)15, "RMDIR", DisplayName = "RMDIR")]
    [DataRow((byte)18, "RENAME", DisplayName = "RENAME")]
    [DataRow((byte)2, "VERSION", DisplayName = "a server's VERSION")]
    [DataRow((byte)101, "type 101", DisplayName = "a server's STATUS")]
    [DataRow((byte)0, "type 0", DisplayName = "type 0")]
    [DataRow((byte)150, "type 150", DisplayName = "an undefined type")]
    public async Task Request_NotInTheReadSide_IsOperationUnsupported(byte type, string name)
    {
        var run = await RunAsync(Store(new() { AllowUploads = true }, clock), Request(type, 1, Text("/a.txt")));

        CollectionAssert.AreEqual(Status(1, 8, "Operation unsupported"), run.Replies.Single());
        CollectionAssert.Contains(run.Notes, $"SFTP {name} -> OP_UNSUPPORTED");
    }

    [TestMethod]
    public async Task Symlink_And_Extended_AreOperationUnsupportedWithTheirMessages()
    {
        var run = await RunAsync(
            Store(new(), clock),
            Request(20, 1, Text("/l.txt"), Text("/a.txt")),
            Request(200, 2, Text("statvfs@openssh.com"), Text("/")));

        CollectionAssert.AreEqual(Status(1, 8, "Symbolic links cannot be created"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 8, "Extension not supported"), run.Replies[1]);
        CollectionAssert.Contains(run.Notes, "SFTP EXTENDED -> OP_UNSUPPORTED");
    }

    [TestMethod]
    [DataRow("/a.txt", 4u, "Not a symbolic link", DisplayName = "a file")]
    [DataRow("/dir", 4u, "Not a symbolic link", DisplayName = "a directory")]
    [DataRow("/missing", 2u, "No such file", DisplayName = "a missing entry")]
    [DataRow("/.hidden.txt", 2u, "No such file", DisplayName = "a hidden entry")]
    public async Task ReadLink_ReportsNoLink(string path, uint code, string message)
    {
        var run = await RunAsync(Store(new(), clock), Request(19, 1, Text(path)));

        CollectionAssert.AreEqual(Status(1, code, message), run.Replies.Single());
    }

    [TestMethod]
    public async Task Request_ThatDoesNotParse_IsBadMessageAndTheSessionGoesOn()
    {
        var run = await RunAsync(
            Store(new(), clock),
            Request(17, 1, UInt32(50), Ascii("/a")),
            Request(17, 2, Text("/a.txt"), [0]),
            Request(5, 3, Handle(0)),
            Request(17, 4, Text("/a.txt")));

        CollectionAssert.AreEqual(Status(1, 5, "Bad message"), run.Replies[0]);
        CollectionAssert.AreEqual(Status(2, 5, "Bad message"), run.Replies[1]);
        CollectionAssert.AreEqual(Status(3, 5, "Bad message"), run.Replies[2]);
        CollectionAssert.AreEqual(AttributesReply(4, 12, RegularFile, WrittenAt), run.Replies[3]);
        CollectionAssert.Contains(run.Notes, "SFTP STAT -> BAD_MESSAGE");
    }

    [TestMethod]
    public async Task Packet_PastMaxMessage_EndsTheSessionBeforeItsBodyIsRead()
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 20 };
        var fits = Request(17, 1, Text("/a.txt/"));
        Assert.AreEqual(20, fits.Length);

        var run = await RunRawAsync(Store(new(), clock), limits, Init3, fits, Request(17, 2, Text("/a.txt/x")), Request(17, 3, Text("/a.txt")));

        Assert.AreEqual(1u, run.Exit);
        Assert.HasCount(2, run.Replies);
        CollectionAssert.AreEqual(Status(1, 2, "No such file"), run.Replies[1]);
        CollectionAssert.Contains(run.Notes, "SFTP session ended: packet past --max-message");
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0 }, DisplayName = "a length cut off")]
    [DataRow(new byte[] { 0, 0, 0, 4, 17, 0, 0, 0 }, DisplayName = "a length below 5")]
    [DataRow(new byte[] { 0, 0, 0, 9, 17, 0, 0, 0, 1 }, DisplayName = "a body cut off")]
    public async Task Packet_ThatCannotBeFramed_EndsTheSessionWithExitStatus1(byte[] bytes)
    {
        var run = await RunRawAsync(Store(new(), clock), null, Init3, bytes);

        Assert.AreEqual(1u, run.Exit);
        Assert.HasCount(1, run.Replies);
        CollectionAssert.Contains(run.Notes, "SFTP session ended: malformed packet");
    }

    [TestMethod]
    public async Task Packet_TooLargeToHoldWithNoLimit_EndsTheSession()
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 0 };

        var run = await RunRawAsync(Store(new(), clock), limits, Init3, [0xFF, 0xFF, 0xFF, 0xFF]);

        Assert.AreEqual(1u, run.Exit);
        CollectionAssert.Contains(run.Notes, "SFTP session ended: malformed packet");
    }

    [TestMethod]
    public async Task Packets_SplitAcrossChannelData_AreReassembled()
    {
        var stat = Request(17, 1, Text("/a.txt"));

        var run = await RunRawAsync(Store(new(), clock), null, Init3[..3], Init3[3..], stat[..2], stat[2..7], stat[7..]);

        CollectionAssert.AreEqual(AttributesReply(1, 12, RegularFile, WrittenAt), run.Replies[1]);
    }

    [TestMethod]
    public async Task EveryStatusMessage_IsFromTheFixedTableAndNamesNoPath()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestThrowingContentFileSystem(clock, new IOException("/srv/secret")), ListingOn);

        var run = await RunAsync(
            store,
            Request(17, 1, Text("/missing")),
            Open(2, "/.hidden.txt"),
            Open(3, "/dir"),
            Open(4, "/new.txt", 0x1A),
            Request(11, 5, Text("/a.txt")),
            Read(6, 7, 0, 1),
            Request(19, 7, Text("/a.txt")),
            Request(17, 8, UInt32(9)),
            Request(18, 9, Text("/a"), Text("/b")),
            Open(10, "/a.txt"),
            Read(11, 0, 0, 5));

        foreach (var reply in run.Replies.Where(reply => reply[4] == 101))
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(reply.AsSpan(13));
            var message = Encoding.UTF8.GetString(reply, 17, length);
            Assert.Contains(message, FixedMessages, message);
        }
    }

    [TestMethod]
    public async Task Server_WithAContentStore_AnswersTheSftpSubsystem()
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var server = new SshProtocolServer(RsaHostKeys, RsaOffer, new AnonymousAuthenticationPolicy(), new FixedRandomSource(), Store(new(), clock));
        var serving = await client.OpenAsync(server, TimeProvider.System);
        client.Send(Concat([5], String("ssh-userauth")));
        await client.ReceiveAsync();
        client.Send(Concat([50], String("alice"), String("ssh-connection"), String("none")));
        await client.ReceiveAsync();
        client.Send(Concat([90], String("session"), UInt32(7), UInt32(2097152), UInt32(32768)));
        await client.ReceiveAsync();

        client.Send(Concat([98], UInt32(0), String("subsystem"), [1], String("sftp")));
        CollectionAssert.AreEqual(Concat([99], UInt32(7)), await client.ReceiveAsync());
        client.Send(Concat([94], UInt32(0), Bytes(Init3)));
        CollectionAssert.AreEqual(Concat([94], UInt32(7), Bytes(Version3)), await client.ReceiveAsync());
        client.Send(Concat([96], UInt32(0)));
        CollectionAssert.AreEqual(Concat([98], UInt32(7), String("exit-status"), [0], UInt32(0)), await client.ReceiveAsync());

        client.Connection.CloseClientWrites();
        await serving;
    }

    [TestMethod]
    public void ContentChannelHandlers_ServeSftpButNotYetScp()
    {
        var handlers = new SshContentChannelHandlers(Store(new(), clock));
        var context = Context(clock, TestContext.CancellationToken);

        Assert.IsInstanceOfType<SftpSession>(handlers.ForSftp(context));
        Assert.IsNull(handlers.ForScp(new SshScpCommand(true, false, false, "/a.txt"), context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshContentChannelHandlers(null!));
    }

    private async Task<SftpRun> RunAsync(ContentStore store, params byte[][] requests)
    {
        var run = await RunRawAsync(store, null, [Init3, .. requests]);
        CollectionAssert.AreEqual(Version3, run.Replies[0]);
        Assert.AreEqual(0u, run.Exit);

        return run with { Replies = run.Replies[1..] };
    }

    private async Task<SftpRun> RunRawAsync(ContentStore store, ExchangeLimits? limits, params byte[][] inbound)
    {
        var log = new RecordingExchangeLog();
        var channel = new SftpTestChannel(inbound);
        var session = new SftpSession(store, Context(clock, TestContext.CancellationToken, limits, log));

        var exit = await session.RunAsync(channel, TestContext.CancellationToken);

        return new SftpRun(exit, channel.WrittenPackets(), [.. log.Notes]);
    }

    private sealed record SftpRun(uint Exit, List<byte[]> Replies, string[] Notes);

    /// <summary>
    /// A scripted channel that deletes a file once the client's third packet has been read, so the
    /// requests after it find the file gone.
    /// </summary>
    private sealed class DeletingChannel(InMemoryContentFileSystem fileSystem, string path, params byte[][] inbound) : ISshChannelDataStream
    {
        private readonly SftpTestChannel inner = new(inbound);
        private int reads;

        public List<byte[]> WrittenPackets() => inner.WrittenPackets();

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (++reads == 5)
            {
                fileSystem.DeleteFile(path);
            }

            return await inner.ReadAsync(buffer, cancellationToken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => inner.WriteAsync(data, cancellationToken);
    }
}
