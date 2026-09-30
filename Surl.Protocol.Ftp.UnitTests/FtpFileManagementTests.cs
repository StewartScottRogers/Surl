using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;
using static Surl.Protocol.Ftp.FtpUploadTests;

namespace Surl.Protocol.Ftp;

/// <summary>
/// <c>MKD</c>, <c>RMD</c>, <c>DELE</c>, <c>RNFR</c>, <c>RNTO</c> and <c>SITE</c> with
/// <c>--allow-uploads</c> (ADR-0052, decision 8; ADR-0031, decision 5).
/// </summary>
[TestClass]
public sealed class FtpFileManagementTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("MKD x", "257 \"/x\" created", DisplayName = "MKD: a new directory")]
    [DataRow("XMKD dir/x", "257 \"/dir/x\" created", DisplayName = "XMKD: a new directory inside another")]
    [DataRow("MKD dir", "550 Already exists", DisplayName = "MKD: an existing directory")]
    [DataRow("MKD a.txt", "550 Already exists", DisplayName = "MKD: an existing file")]
    [DataRow("MKD missing/x", "550 No such directory", DisplayName = "MKD: a missing parent")]
    [DataRow("MKD .x", "550 Not permitted", DisplayName = "MKD: a hidden new name")]
    [DataRow("MKD /.surl/x", "550 Not permitted", DisplayName = "MKD: under the service-state folder")]
    [DataRow("MKD ../x", "550 Not permitted", DisplayName = "MKD: above the root")]
    [DataRow("RMD dir/sub", "250 Directory removed", DisplayName = "RMD: an empty directory")]
    [DataRow("RMD dir", "550 Directory not empty", DisplayName = "RMD: a directory with an entry")]
    [DataRow("RMD missing", "550 No such directory", DisplayName = "RMD: a missing directory")]
    [DataRow("XRMD a.txt", "550 No such directory", DisplayName = "XRMD: a file")]
    [DataRow("RMD /.surl", "550 No such directory", DisplayName = "RMD: the service-state folder")]
    [DataRow("RMD ../x", "550 No such directory", DisplayName = "RMD: above the root")]
    [DataRow("RMD /", "550 Not permitted", DisplayName = "RMD: the root")]
    [DataRow("DELE a.txt", "250 File deleted", DisplayName = "DELE: a file")]
    [DataRow("DELE missing.txt", "550 No such file", DisplayName = "DELE: a missing file")]
    [DataRow("DELE dir", "550 No such file", DisplayName = "DELE: a directory")]
    [DataRow("DELE .secret.txt", "550 No such file", DisplayName = "DELE: a hidden file")]
    [DataRow("DELE /.surl/state.txt", "550 No such file", DisplayName = "DELE: under the service-state folder")]
    [DataRow("DELE ../a.txt", "550 No such file", DisplayName = "DELE: above the root")]
    [DataRow("RNFR a.txt", "350 Ready for RNTO", DisplayName = "RNFR: a file")]
    [DataRow("RNFR dir", "350 Ready for RNTO", DisplayName = "RNFR: a directory")]
    [DataRow("RNFR missing.txt", "550 No such file", DisplayName = "RNFR: a missing entry")]
    [DataRow("RNFR /.surl/state.txt", "550 No such file", DisplayName = "RNFR: under the service-state folder")]
    [DataRow("RNFR ../a.txt", "550 No such file", DisplayName = "RNFR: above the root")]
    [DataRow("RNTO b.txt", "503 Send RNFR first", DisplayName = "RNTO: without RNFR")]
    [DataRow("SITE CHMOD 644 a.txt", "504 SITE CHMOD is not supported", DisplayName = "SITE: a word and arguments")]
    [DataRow("SITE HELP", "504 SITE HELP is not supported", DisplayName = "SITE: a word alone")]
    [DataRow("SITE \\x", "504 SITE \\x5Cx is not supported", DisplayName = "SITE: a word rendered safely")]
    [DataRow("MKD", "501 Syntax error in arguments", DisplayName = "MKD: no argument")]
    [DataRow("RNTO", "501 Syntax error in arguments", DisplayName = "RNTO: no argument")]
    [DataRow("SITE", "501 Syntax error in arguments", DisplayName = "SITE: no argument")]
    public async Task Command_WithUploadsAllowed_IsAnsweredFromTheTable(string command, string reply)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, contentStore: Store(StandardFileSystem()));

        Assert.AreEqual(reply + "\r\n", written);
    }

    [TestMethod]
    public async Task Mkd_CreatesTheDirectory()
    {
        var fileSystem = StandardFileSystem();

        await ServeLoggedInAsync("CWD dir\r\nMKD x\r\n", TestContext.CancellationToken, contentStore: Store(fileSystem));

        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "dir", "x")));
    }

    [TestMethod]
    public async Task RmdAndDele_RemoveTheEntry()
    {
        var fileSystem = StandardFileSystem();

        await ServeLoggedInAsync("RMD dir/sub\r\nDELE a.txt\r\n", TestContext.CancellationToken, contentStore: Store(fileSystem));

        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "dir", "sub")));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "a.txt")));
    }

    [TestMethod]
    [DataRow("RNFR a.txt\r\nRNTO dir/b.txt", "dir/b.txt", DisplayName = "A file to a new name")]
    [DataRow("RNFR dir\r\nRNTO moved", "moved/sub", DisplayName = "A directory with what is inside it")]
    public async Task Rnto_AfterRnfr_RenamesTheEntry(string commands, string movedPath)
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync(commands + "\r\n", TestContext.CancellationToken, contentStore: Store(fileSystem));

        Assert.AreEqual("350 Ready for RNTO\r\n250 Renamed\r\n", written);
        Assert.AreNotEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join([InMemoryContentFileSystem.RootPath, .. movedPath.Split('/')])));
    }

    [TestMethod]
    public async Task Rnto_OntoAnExistingFile_ReplacesIt()
    {
        var fileSystem = StandardFileSystem();
        using (var file = fileSystem.CreateFileForAsyncWrite(Path.Join(InMemoryContentFileSystem.RootPath, "b.txt")))
        {
            file.Write("bee\n"u8);
        }

        var written = await ServeLoggedInAsync("RNFR b.txt\r\nRNTO a.txt\r\n", TestContext.CancellationToken, contentStore: Store(fileSystem));

        Assert.AreEqual("350 Ready for RNTO\r\n250 Renamed\r\n", written);
        Assert.AreEqual("bee\n", ReadFile(fileSystem, "a.txt"));
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
    }

    [TestMethod]
    [DataRow("RNFR a.txt\r\nRNTO dir", "553 Cannot rename onto a directory", DisplayName = "A file onto a directory")]
    [DataRow("RNFR dir/sub\r\nRNTO /a.txt", "553 Cannot rename a directory onto a file", DisplayName = "A directory onto a file")]
    [DataRow("RNFR a.txt\r\nRNTO missing/b.txt", "553 No such directory", DisplayName = "Into a missing directory")]
    [DataRow("RNFR a.txt\r\nRNTO /.surl/a.txt", "550 Not permitted", DisplayName = "Under the service-state folder")]
    [DataRow("RNFR a.txt\r\nRNTO .a.txt", "550 Not permitted", DisplayName = "To a hidden name")]
    [DataRow("RNFR a.txt\r\nRNTO ../a.txt", "550 Not permitted", DisplayName = "Above the root")]
    [DataRow("RNFR dir\r\nRNTO dir/sub/dir", "550 Not permitted", DisplayName = "A directory into itself")]
    [DataRow("RNFR a.txt\r\nNOOP\r\nRNTO b.txt", "503 Send RNFR first", DisplayName = "RNFR not immediately before")]
    [DataRow("RNFR missing.txt\r\nRNTO b.txt", "503 Send RNFR first", DisplayName = "After a refused RNFR")]
    [DataRow("RNFR a.txt\r\nRNTO b.txt\r\nRNTO c.txt", "503 Send RNFR first", DisplayName = "A second RNTO")]
    public async Task Rnto_Refused_LeavesTheEntryWhereItWas(string commands, string lastReply)
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync(commands + "\r\n", TestContext.CancellationToken, contentStore: Store(fileSystem));

        Assert.EndsWith(lastReply + "\r\n", written);
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "dir", "sub")));
        Assert.AreEqual("state\n", ReadFile(fileSystem, ".surl", "state.txt"));
        Assert.IsFalse(RootEntries(fileSystem).Contains(".a.txt"));
    }

    [TestMethod]
    public async Task Mkd_FileSystemFails_Answers451AndNotesIt()
    {
        var fileSystem = new UnitTestFailingWritesContentFileSystem();
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync("MKD x\r\n", TestContext.CancellationToken, log: log, contentStore: fileSystem.ContentStore());

        Assert.AreEqual("451 The change could not be made\r\n", written);
        CollectionAssert.AreEqual(new[] { "A file-management command failed (IOException: The disk is full.); answered 451." }, log.Notes.ToArray());
    }
}
