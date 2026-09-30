using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// <c>LIST</c>, <c>NLST</c>, <c>MLSD</c> and <c>MLST</c> (ADR-0052, decision 7), over a passive
/// data connection from <see cref="InMemoryDataConnections"/>.
/// </summary>
[TestClass]
public sealed class FtpListingTests
{
    private const string PassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";
    private const string ListingOpening = "150 Opening data connection for directory listing\r\n";
    private const string TransferComplete = "226 Transfer complete\r\n";
    private const string NoSuchDirectory = "550 No such directory\r\n";
    private const string NoSuchFile = "550 No such file\r\n";
    private const string ListedTransfer = PassiveReply + ListingOpening + TransferComplete;

    private static readonly ContentExposureOptions ListingOn = new() { ListDirectories = true };

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// The standard content store with <c>/dir/b.txt</c> (<c>bee\n</c>) added, so <c>/dir/</c>
    /// lists a file and a directory, and <paramref name="addEntries"/> run on its file system.
    /// </summary>
    public static ContentStore ListingContentStore(ContentExposureOptions options, Action<InMemoryContentFileSystem>? addEntries = null)
    {
        var fileSystem = StandardFileSystem();
        WriteFile(fileSystem, "bee\n", "dir", "b.txt");
        addEntries?.Invoke(fileSystem);

        return new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, options);
    }

    [TestMethod]
    [DataRow("LIST", DisplayName = "LIST of the current directory")]
    [DataRow("LIST dir", DisplayName = "LIST of a directory")]
    [DataRow("NLST", DisplayName = "NLST of the current directory")]
    [DataRow("NLST /dir", DisplayName = "NLST of a directory")]
    [DataRow("MLSD", DisplayName = "MLSD of the current directory")]
    [DataRow("MLSD dir/sub", DisplayName = "MLSD of an empty directory")]
    [DataRow("LIST missing", DisplayName = "LIST of a missing path")]
    public async Task Listing_WithoutListDirectories_Answers550AsForAMissingDirectoryBeforeAnyDataConnection(string command)
    {
        var dataConnections = PassiveDataConnections(new InMemoryConnection([]));

        var written = await ServeLoggedInAsync(
            $"EPSV\r\n{command}\r\n", TestContext.CancellationToken, dataConnections, contentStore: ListingContentStore(new ContentExposureOptions()));

        Assert.AreEqual(PassiveReply + NoSuchDirectory, written);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
    }

    [TestMethod]
    public async Task List_Root_LeavesOutTheServiceStateFolderAndDotFiles()
    {
        var (written, listing) = await ListAsync("LIST /", ListingOn);

        Assert.AreEqual(ListedTransfer, written);
        Assert.AreEqual(
            "-rw-r--r-- 1 surl surl           12 Sep 27 12:34 a.txt\r\n"
            + "drwxr-xr-x 1 surl surl            0 Sep 27 12:34 dir\r\n",
            listing);
    }

    [TestMethod]
    public async Task List_RootWithServeDotFiles_ShowsDotFilesButNotTheServiceStateFolder()
    {
        var (written, listing) = await ListAsync("NLST /", new ContentExposureOptions { ListDirectories = true, ServeDotFiles = true });

        Assert.AreEqual(ListedTransfer, written);
        Assert.AreEqual(".hidden\r\n.secret.txt\r\na.txt\r\ndir\r\n", listing);
    }

    [TestMethod]
    public async Task Mlsd_Directory_SendsEachEntrysFacts()
    {
        var (written, listing) = await ListAsync("MLSD dir", ListingOn);

        Assert.AreEqual(ListedTransfer, written);
        Assert.AreEqual("type=file;size=4;modify=20260927123456; b.txt\r\ntype=dir;modify=20260927123456; sub\r\n", listing);
    }

    [TestMethod]
    [DataRow("LIST dir/sub")]
    [DataRow("NLST dir/sub")]
    [DataRow("MLSD dir/sub")]
    public async Task Listing_EmptyDirectory_SendsNoBytesAndCompletes(string command)
    {
        var dataConnection = new InMemoryConnection([]);

        var written = await ServeLoggedInAsync(
            $"EPSV\r\n{command}\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection), contentStore: ListingContentStore(ListingOn));

        Assert.AreEqual(ListedTransfer, written);
        Assert.IsEmpty(dataConnection.WrittenBytes);
        Assert.IsTrue(dataConnection.WritesCompleted);
        Assert.IsTrue(dataConnection.Disposed);
    }

    [TestMethod]
    public async Task Listing_NameWithASpaceAndANonAsciiCharacter_SendsTheNameInUtf8()
    {
        var store = ListingContentStore(ListingOn, fileSystem => WriteFile(fileSystem, "x", "names", "b c ü.txt"));

        var (written, listing) = await ListAsync("NLST names", store);
        var (_, longListing) = await ListAsync("LIST names", store);

        Assert.AreEqual(ListedTransfer, written);
        Assert.AreEqual("b c ü.txt\r\n", listing);
        Assert.AreEqual("-rw-r--r-- 1 surl surl            1 Sep 27 12:34 b c ü.txt\r\n", longListing);
    }

    [TestMethod]
    [DataRow("LIST a.txt", "-rw-r--r-- 1 surl surl           12 Sep 27 12:34 a.txt\r\n", DisplayName = "LIST of a file")]
    [DataRow("NLST dir/b.txt", "b.txt\r\n", DisplayName = "NLST of a file in a directory")]
    [DataRow("LIST -la a.txt", "-rw-r--r-- 1 surl surl           12 Sep 27 12:34 a.txt\r\n", DisplayName = "LIST with ls options")]
    [DataRow("NLST -a -l a.txt", "a.txt\r\n", DisplayName = "NLST with two ls options")]
    public async Task Listing_OneFileWithoutListDirectories_ListsThatFile(string command, string expectedListing)
    {
        var (written, listing) = await ListAsync(command, new ContentExposureOptions());

        Assert.AreEqual(ListedTransfer, written);
        Assert.AreEqual(expectedListing, listing);
    }

    [TestMethod]
    [DataRow("LIST -a")]
    [DataRow("NLST -")]
    public async Task Listing_LsOptionsAlone_ListsTheCurrentDirectory(string command)
    {
        var (written, listing) = await ListAsync(command, ListingOn);

        Assert.AreEqual(ListedTransfer, written);
        StringAssert.Contains(listing, "a.txt");
        StringAssert.Contains(listing, "dir");
    }

    [TestMethod]
    [DataRow("MLSD a.txt", DisplayName = "MLSD of a file")]
    [DataRow("LIST .secret.txt", DisplayName = "A dot-file")]
    [DataRow("NLST .hidden", DisplayName = "A dot-directory")]
    [DataRow("LIST /.surl", DisplayName = "The service-state folder")]
    [DataRow("LIST ..", DisplayName = "Above the root")]
    [DataRow("LIST c:", DisplayName = "A name the content store refuses")]
    [DataRow("LIST missing", DisplayName = "A missing path")]
    public async Task Listing_WithListDirectories_AnswersAnythingButAnExposedDirectoryOrListedFileAs550(string command)
    {
        var dataConnections = PassiveDataConnections(new InMemoryConnection([]));

        var written = await ServeLoggedInAsync($"EPSV\r\n{command}\r\n", TestContext.CancellationToken, dataConnections, contentStore: ListingContentStore(ListingOn));

        Assert.AreEqual(PassiveReply + NoSuchDirectory, written);
    }

    [TestMethod]
    public async Task Listing_AfterCwd_ListsTheCurrentDirectory()
    {
        var dataConnection = new InMemoryConnection([]);

        var written = await ServeLoggedInAsync(
            "CWD dir\r\nEPSV\r\nNLST\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection), contentStore: ListingContentStore(ListingOn));

        Assert.AreEqual("250 Directory changed\r\n" + ListedTransfer, written);
        Assert.AreEqual("b.txt\r\nsub\r\n", Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Listing_NoDataConnectionPrepared_Answers425()
    {
        var written = await ServeLoggedInAsync("LIST\r\n", TestContext.CancellationToken, contentStore: ListingContentStore(ListingOn));

        Assert.AreEqual("425 Use PASV or PORT first\r\n", written);
    }

    [TestMethod]
    public async Task Listing_DataConnectionNotOpened_Answers425()
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, 50100), acceptedConnection: null);

        var written = await ServeLoggedInAsync("EPSV\r\nLIST\r\n", TestContext.CancellationToken, dataConnections, contentStore: ListingContentStore(ListingOn));

        Assert.AreEqual(PassiveReply + "425 Cannot open data connection\r\n", written);
    }

    [TestMethod]
    public async Task Listing_ClientClosesTheDataConnectionEarly_Answers426AndNotesWhy()
    {
        var dataConnection = new InMemoryConnection([]);
        dataConnection.Abort();
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(
            "EPSV\r\nLIST\r\nABOR\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection), log, ListingContentStore(ListingOn));

        Assert.AreEqual(PassiveReply + ListingOpening + "426 Connection closed; transfer aborted\r\n226 Abort successful\r\n", written);
        Assert.IsTrue(dataConnection.Disposed);
        CollectionAssert.AreEqual(
            new[] { "The client closed the data connection before the whole listing was sent; the data connection was reset." },
            log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("LIST a.txt", "a.txt could not be listed (IOException: disk fault); answered 550.", "550 No such directory\r\n")]
    [DataRow("MLST a.txt", "a.txt could not be read (IOException: disk fault); answered 550.", "550 No such file\r\n")]
    [DataRow("MLST dir", "could not be read (IOException: disk fault); answered 550.", "550 No such file\r\n")]
    public async Task Listing_FileStatusUnreadable_Answers550AndNotesWhy(string command, string noteEnd, string reply)
    {
        var fileSystem = new UnitTestThrowingContentFileSystem(new IOException("disk fault"), failsStatus: true);
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, log: log, contentStore: fileSystem.ContentStore());

        Assert.AreEqual(reply, written);
        StringAssert.EndsWith(log.Notes.Single(), noteEnd);
    }

    [TestMethod]
    [DataRow("MLST a.txt", "/a.txt", "type=file;size=12;modify=20260927123456;", DisplayName = "A file")]
    [DataRow("MLST dir/b.txt", "/dir/b.txt", "type=file;size=4;modify=20260927123456;", DisplayName = "A file in a directory")]
    [DataRow("MLST dir", "/dir", "type=dir;modify=20260927123456;", DisplayName = "A directory")]
    [DataRow("MLST /", "/", "type=dir;", DisplayName = "The root")]
    [DataRow("MLST", "/", "type=dir;", DisplayName = "The current directory")]
    public async Task Mlst_ExposedEntryWithoutListDirectories_AnswersItsFactsOnTheControlConnection(string command, string path, string facts)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, contentStore: ListingContentStore(new ContentExposureOptions()));

        Assert.AreEqual($"250-Listing {path}\r\n {facts} {path}\r\n250 End\r\n", written);
    }

    [TestMethod]
    public async Task Mlst_NameWithASpaceAndANonAsciiCharacter_RendersTheNameAsReplyText()
    {
        var store = ListingContentStore(ListingOn, fileSystem => WriteFile(fileSystem, "x", "b c ü.txt"));

        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(AnonymousLogin), Encoding.UTF8.GetBytes("MLST b c ü.txt\r\n")]);

        await Server(contentStore: store).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(
            Greeting + AnonymousLoginReplies
            + @"250-Listing /b c \xC3\xBC.txt" + "\r\n" + @" type=file;size=1;modify=20260927123456; /b c \xC3\xBC.txt" + "\r\n250 End\r\n",
            Text(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("MLST missing.txt", DisplayName = "A missing path")]
    [DataRow("MLST .secret.txt", DisplayName = "A dot-file")]
    [DataRow("MLST /.surl", DisplayName = "The service-state folder")]
    [DataRow("MLST ..", DisplayName = "Above the root")]
    [DataRow("MLST c:", DisplayName = "A name the content store refuses")]
    public async Task Mlst_AnythingButAnExposedEntry_Answers550(string command)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, contentStore: ListingContentStore(ListingOn));

        Assert.AreEqual(NoSuchFile, written);
    }

    [TestMethod]
    public async Task Mlst_DirectoryItsParentDoesNotListByThatName_AnswersTypeAlone()
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestUnlistingContentFileSystem(), new ContentExposureOptions());

        var written = await ServeLoggedInAsync("MLST dir\r\n", TestContext.CancellationToken, contentStore: store);

        Assert.AreEqual("250-Listing /dir\r\n type=dir; /dir\r\n250 End\r\n", written);
    }

    private async Task<(string Written, string Listing)> ListAsync(string command, ContentExposureOptions options) =>
        await ListAsync(command, ListingContentStore(options));

    private async Task<(string Written, string Listing)> ListAsync(string command, ContentStore contentStore)
    {
        var dataConnection = new InMemoryConnection([]);
        var written = await ServeLoggedInAsync(
            $"EPSV\r\n{command}\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection), contentStore: contentStore);
        Assert.IsTrue(dataConnection.WritesCompleted);

        return (written, Encoding.UTF8.GetString(dataConnection.WrittenBytes));
    }

    private static InMemoryDataConnections PassiveDataConnections(InMemoryConnection dataConnection) =>
        new InMemoryDataConnections().ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, 50100), dataConnection);

    private static void WriteFile(InMemoryContentFileSystem fileSystem, string text, params string[] segments)
    {
        var directory = Path.Join([InMemoryContentFileSystem.RootPath, .. segments[..^1]]);
        if (fileSystem.GetEntryKind(directory) == ContentEntryKind.None)
        {
            fileSystem.CreateDirectory(directory);
        }

        using var file = fileSystem.CreateFileForAsyncWrite(Path.Join(directory, segments[^1]));
        file.Write(Encoding.UTF8.GetBytes(text));
    }
}
