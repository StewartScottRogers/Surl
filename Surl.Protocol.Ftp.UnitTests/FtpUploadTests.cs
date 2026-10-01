using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// <c>STOR</c> and <c>APPE</c> (ADR-0052, decision 8; ADR-0006, sections 2 and 5), over a
/// passive data connection from <see cref="InMemoryDataConnections"/>, into the in-memory
/// content store.
/// </summary>
[TestClass]
public sealed class FtpUploadTests
{
    private const string PassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";
    private const string TransferComplete = "226 Transfer complete\r\n";
    private const string NotPermitted = "550 Not permitted\r\n";
    private const string Upload = "uploaded body\n";

    private static readonly IPEndPoint PassiveEndPoint = new(IPAddress.Loopback, 50100);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("STOR a.txt")]
    [DataRow("APPE a.txt")]
    [DataRow("MKD x")]
    [DataRow("XMKD x")]
    [DataRow("RMD dir/sub")]
    [DataRow("XRMD dir/sub")]
    [DataRow("DELE a.txt")]
    [DataRow("RNFR a.txt")]
    [DataRow("RNTO b.txt")]
    [DataRow("SITE CHMOD 644 a.txt")]
    public async Task Write_WithoutAllowUploads_Answers550BeforeAnyDataConnection(string command)
    {
        var fileSystem = StandardFileSystem();
        var dataConnections = Passive(new InMemoryConnection(Ascii(Upload)));

        var written = await ServeLoggedInAsync(
            $"EPSV\r\n{command}\r\n", TestContext.CancellationToken, dataConnections, contentStore: Store(fileSystem, allowUploads: false));

        Assert.AreEqual(PassiveReply + NotPermitted, written);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
        Assert.AreEqual(FileText, ReadFile(fileSystem, "a.txt"));
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "dir", "sub")));
    }

    [TestMethod]
    public async Task Stor_NewFile_WritesTheUploadAndAnswers226()
    {
        var fileSystem = StandardFileSystem();
        var dataConnection = new InMemoryConnection(Ascii("uploaded ", "body\n"));

        var written = await ServeLoggedInAsync("EPSV\r\nSTOR dir/b.txt\r\n", TestContext.CancellationToken, Passive(dataConnection), contentStore: Store(fileSystem));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for dir/b.txt\r\n" + TransferComplete, written);
        Assert.AreEqual(Upload, ReadFile(fileSystem, "dir", "b.txt"));
        Assert.IsTrue(dataConnection.Disposed);
        Assert.IsFalse(dataConnection.Aborted);
    }

    [TestMethod]
    public async Task Stor_ExistingFile_ReplacesIt()
    {
        var fileSystem = StandardFileSystem();

        await ServeLoggedInAsync("EPSV\r\nSTOR a.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection(Ascii(Upload))), contentStore: Store(fileSystem));

        Assert.AreEqual(Upload, ReadFile(fileSystem, "a.txt"));
    }

    [TestMethod]
    public async Task Stor_EmptyUpload_WritesAnEmptyFile()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync("EPSV\r\nSTOR empty.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection([])), contentStore: Store(fileSystem));

        Assert.EndsWith(TransferComplete, written);
        Assert.AreEqual(string.Empty, ReadFile(fileSystem, "empty.txt"));
    }

    [TestMethod]
    public async Task Appe_ExistingFile_AppendsTheUpload()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync("EPSV\r\nAPPE a.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection(Ascii("more\n"))), contentStore: Store(fileSystem));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for a.txt\r\n" + TransferComplete, written);
        Assert.AreEqual(FileText + "more\n", ReadFile(fileSystem, "a.txt"));
    }

    [TestMethod]
    public async Task Appe_MissingFile_CreatesIt()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync("EPSV\r\nAPPE new.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection(Ascii(Upload))), contentStore: Store(fileSystem));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for new.txt\r\n" + TransferComplete, written);
        Assert.AreEqual(Upload, ReadFile(fileSystem, "new.txt"));
    }

    [TestMethod]
    public async Task Stor_AfterRestAtTheFilesLength_Appends()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync(
            "EPSV\r\nREST 12\r\nSTOR a.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection(Ascii("more\n"))), contentStore: Store(fileSystem));

        Assert.EndsWith(TransferComplete, written);
        Assert.AreEqual(FileText + "more\n", ReadFile(fileSystem, "a.txt"));
    }

    [TestMethod]
    public async Task Stor_AfterRestZero_WritesAfresh()
    {
        var fileSystem = StandardFileSystem();

        await ServeLoggedInAsync("EPSV\r\nREST 0\r\nSTOR a.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection(Ascii(Upload))), contentStore: Store(fileSystem));

        Assert.AreEqual(Upload, ReadFile(fileSystem, "a.txt"));
    }

    [TestMethod]
    [DataRow("REST 5\r\nSTOR a.txt", DisplayName = "An offset short of the file's length")]
    [DataRow("REST 13\r\nAPPE a.txt", DisplayName = "An offset past the file's length")]
    [DataRow("REST 3\r\nSTOR new.txt", DisplayName = "An offset into a missing file")]
    public async Task Stor_AfterRestAnywhereButTheFilesLength_Answers554(string commands)
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync($"{commands}\r\n", TestContext.CancellationToken, contentStore: Store(fileSystem));

        Assert.AreEqual("350 ", written[..4]);
        Assert.EndsWith("554 Restart offset must equal the file's length\r\n", written);
        Assert.AreEqual(FileText, ReadFile(fileSystem, "a.txt"));
    }

    [TestMethod]
    public async Task Stor_TheRestOffsetIsSpentByTheCommandAfterIt()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync(
            "REST 5\r\nSTOR a.txt\r\nEPSV\r\nSTOR a.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection(Ascii(Upload))), contentStore: Store(fileSystem));

        Assert.EndsWith(TransferComplete, written);
        Assert.AreEqual(Upload, ReadFile(fileSystem, "a.txt"));
    }

    [TestMethod]
    [DataRow("STOR", "501 Syntax error in arguments")]
    [DataRow("APPE", "501 Syntax error in arguments")]
    [DataRow("STOR a.txt", "425 Use PASV or PORT first")]
    [DataRow("STOR missing/b.txt", "553 No such directory")]
    [DataRow("STOR a.txt/b.txt", "553 No such directory")]
    [DataRow("STOR .hidden/b.txt", "553 No such directory")]
    [DataRow("STOR /.surl/b.txt", "553 No such directory")]
    [DataRow("APPE /.surl/state.txt", "553 No such directory")]
    [DataRow("STOR ../b.txt", "550 Not permitted")]
    [DataRow("STOR c:", "550 Not permitted")]
    public async Task Stor_RefusedBeforeTheDataConnectionIsPrepared_IsAnsweredFromTheTable(string command, string reply)
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, contentStore: Store(fileSystem));

        Assert.AreEqual(reply + "\r\n", written);
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
    }

    [TestMethod]
    [DataRow("STOR dir", DisplayName = "A directory in the way")]
    [DataRow("STOR /", DisplayName = "The root")]
    [DataRow("STOR .new.txt", DisplayName = "A hidden new name")]
    [DataRow("STOR /.surl", DisplayName = "The service-state folder")]
    public async Task Stor_ToALocationTheContentStoreRefuses_Answers550BeforeAnyDataConnection(string command)
    {
        var fileSystem = StandardFileSystem();
        var dataConnections = Passive(new InMemoryConnection(Ascii(Upload)));

        var written = await ServeLoggedInAsync($"EPSV\r\n{command}\r\n", TestContext.CancellationToken, dataConnections, contentStore: Store(fileSystem));

        Assert.AreEqual(PassiveReply + NotPermitted, written);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
        Assert.AreEqual("state\n", ReadFile(fileSystem, ".surl", "state.txt"));
    }

    [TestMethod]
    public async Task Stor_PastMaxFileSize_Answers552AndLeavesNothingBehind()
    {
        var fileSystem = StandardFileSystem();
        var dataConnection = new InMemoryConnection(Ascii(Upload));
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(
            "EPSV\r\nSTOR b.txt\r\n", TestContext.CancellationToken, Passive(dataConnection), log, Store(fileSystem, maxUploadBytes: 4));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for b.txt\r\n552 Upload exceeds the size limit\r\n", written);
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
        Assert.IsTrue(dataConnection.Aborted);
        CollectionAssert.AreEqual(
            new[] { "The upload grew past the limit of 4 bytes; the partial upload was deleted and the data connection was reset." },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Appe_PastMaxFileSizeWithTheExistingLength_Answers552AndKeepsTheFile()
    {
        var fileSystem = StandardFileSystem();

        var written = await ServeLoggedInAsync(
            "EPSV\r\nAPPE a.txt\r\n", TestContext.CancellationToken, Passive(new InMemoryConnection(Ascii("more\n"))), contentStore: Store(fileSystem, maxUploadBytes: 14));

        Assert.EndsWith("552 Upload exceeds the size limit\r\n", written);
        Assert.AreEqual(FileText, ReadFile(fileSystem, "a.txt"));
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
    }

    [TestMethod]
    public async Task Stor_NoDataConnectionArrives_Answers425AndWritesNothing()
    {
        var fileSystem = StandardFileSystem();
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, null);

        var written = await ServeLoggedInAsync("EPSV\r\nSTOR b.txt\r\n", TestContext.CancellationToken, dataConnections, contentStore: Store(fileSystem));

        Assert.AreEqual(PassiveReply + "425 Cannot open data connection\r\n", written);
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
    }

    [TestMethod]
    public async Task Stor_DataConnectionResetMidUpload_Answers426AndLeavesNothingBehind()
    {
        var fileSystem = StandardFileSystem();
        var dataConnection = new ReadFailingConnection(Ascii("uploaded "));
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(
            "EPSV\r\nSTOR b.txt\r\n", TestContext.CancellationToken, Passive(dataConnection), log, Store(fileSystem));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for b.txt\r\n426 Connection closed; transfer aborted\r\n", written);
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
        Assert.IsTrue(dataConnection.Aborted);
        CollectionAssert.AreEqual(
            new[] { "The data connection failed before the whole upload arrived; the partial upload was deleted and the data connection was reset." },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Stor_FileSystemFailsTheWrite_Answers451BeforeAnyDataConnection()
    {
        var fileSystem = new UnitTestFailingWritesContentFileSystem();
        var dataConnections = Passive(new InMemoryConnection(Ascii(Upload)));
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync("EPSV\r\nSTOR b.txt\r\n", TestContext.CancellationToken, dataConnections, log, fileSystem.ContentStore());

        Assert.AreEqual(PassiveReply + "451 Cannot write the file\r\n", written);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
        Assert.StartsWith(Path.Join(InMemoryContentFileSystem.RootPath, "b.txt") + " could not be written (IOException: The disk is full.)", log.Notes.Single());
    }

    internal static readonly string[] StandardRootEntries = [".hidden", ".secret.txt", ".surl", "a.txt", "dir"];

    internal static ContentStore Store(InMemoryContentFileSystem fileSystem, bool allowUploads = true, long maxUploadBytes = ContentExposureOptions.DefaultMaxUploadBytes) =>
        new(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = allowUploads, MaxUploadBytes = maxUploadBytes });

    internal static string ReadFile(InMemoryContentFileSystem fileSystem, params string[] segments)
    {
        using var file = fileSystem.OpenFileForAsyncRead(Path.Join([InMemoryContentFileSystem.RootPath, .. segments]));
        using var copy = new MemoryStream();
        file.CopyTo(copy);

        return Encoding.UTF8.GetString(copy.ToArray());
    }

    internal static string[] RootEntries(InMemoryContentFileSystem fileSystem) =>
        [.. fileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath)];

    private static InMemoryDataConnections Passive(IConnection dataConnection) =>
        new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, dataConnection);
}
