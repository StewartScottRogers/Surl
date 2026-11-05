using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The FTP server hands the content store the account a checked login named, or an anonymous
/// caller, on the scheme it listens on (ADR-0102 decision 2.2): a share the caller may not read is
/// answered as a missing path, and a write in a share it may only read is <c>550 Not permitted</c>
/// before any data connection is used (ADR-0102 decision 2.3).
/// </summary>
[TestClass]
public sealed class FtpShareAccessTests
{
    private const string ShareName = "team";
    private const string SharedText = "shared\n";
    private const string PassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";
    private const string TransferComplete = "226 Transfer complete\r\n";
    private const string NoSuchFile = "550 No such file\r\n";
    private const string NoSuchDirectory = "550 No such directory\r\n";
    private const string NotPermitted = "550 Not permitted\r\n";

    private static readonly IPEndPoint PassiveEndPoint = new(IPAddress.Loopback, 50100);
    private static readonly string SharePath = Path.Join(InMemoryContentFileSystem.RootPath, "shared-folder");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Retr_ReadAccountInTheShare_SendsTheSharedFile()
    {
        var dataConnection = new InMemoryConnection([]);

        var written = await ServeAsAsync("ann", PasswordLoginVerdict.Accepted, "EPSV\r\nRETR team/notes.txt\r\n", ShareStore(Grants(("ann", ContentShareAccess.Read))), Passive(dataConnection));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for team/notes.txt (7 bytes)\r\n" + TransferComplete, written);
        Assert.AreEqual(SharedText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task List_ReadAccountInTheShare_ListsTheShare()
    {
        var dataConnection = new InMemoryConnection([]);

        var written = await ServeAsAsync("ann", PasswordLoginVerdict.Accepted, "EPSV\r\nNLST team\r\n", ShareStore(Grants(("ann", ContentShareAccess.Read))), Passive(dataConnection));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for directory listing\r\n" + TransferComplete, written);
        Assert.AreEqual("notes.txt\r\n", Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Stor_ReadAccountInTheShare_Is550NotPermittedBeforeAnyDataConnection()
    {
        var shareFileSystem = ShareFileSystem();
        var dataConnections = Passive(new InMemoryConnection(Ascii("replaced\n")));

        var written = await ServeAsAsync("ann", PasswordLoginVerdict.Accepted, "EPSV\r\nSTOR team/notes.txt\r\n", ShareStore(Grants(("ann", ContentShareAccess.Read)), shareFileSystem), dataConnections);

        Assert.AreEqual(PassiveReply + NotPermitted, written);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
        Assert.AreEqual(SharedText, ReadShareFile(shareFileSystem));
    }

    [TestMethod]
    public async Task Dele_ReadAccountInTheShare_LeavesTheFile()
    {
        var shareFileSystem = ShareFileSystem();

        var written = await ServeAsAsync("ann", PasswordLoginVerdict.Accepted, "DELE team/notes.txt\r\n", ShareStore(Grants(("ann", ContentShareAccess.Read)), shareFileSystem));

        Assert.AreEqual(NoSuchFile, written);
        Assert.AreEqual(SharedText, ReadShareFile(shareFileSystem));
    }

    [TestMethod]
    public async Task Stor_ReadWriteAccountInTheShare_WritesTheUpload()
    {
        var shareFileSystem = ShareFileSystem();

        var written = await ServeAsAsync(
            "ann", PasswordLoginVerdict.Accepted, "EPSV\r\nSTOR team/notes.txt\r\n", ShareStore(Grants(("ann", ContentShareAccess.ReadWrite)), shareFileSystem), Passive(new InMemoryConnection(Ascii("replaced\n"))));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for team/notes.txt\r\n" + TransferComplete, written);
        Assert.AreEqual("replaced\n", ReadShareFile(shareFileSystem));
    }

    [TestMethod]
    public async Task Retr_AccountGrantedNothing_IsTheReplyAMissingFileGets()
    {
        var dataConnections = Passive(new InMemoryConnection([]));

        var written = await ServeAsAsync("bob", PasswordLoginVerdict.Accepted, "EPSV\r\nRETR team/notes.txt\r\nRETR missing.txt\r\n", ShareStore(Grants(("ann", ContentShareAccess.ReadWrite))), dataConnections);

        Assert.AreEqual(PassiveReply + NoSuchFile + NoSuchFile, written);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
    }

    [TestMethod]
    public async Task List_AccountGrantedNothing_IsTheReplyAMissingDirectoryGets()
    {
        var written = await ServeAsAsync("bob", PasswordLoginVerdict.Accepted, "LIST team\r\nCWD team\r\nSIZE team/notes.txt\r\n", ShareStore(Grants(("ann", ContentShareAccess.ReadWrite))));

        Assert.AreEqual(NoSuchDirectory + NoSuchDirectory + NoSuchFile, written);
    }

    [TestMethod]
    public async Task Size_LoginAcceptedUncheckedInAClosedShare_IsAMissingFile()
    {
        var written = await ServeAsAsync("ann", PasswordLoginVerdict.AcceptedUnchecked, "SIZE team/notes.txt\r\n", ShareStore(Grants(("ann", ContentShareAccess.ReadWrite))));

        Assert.AreEqual(NoSuchFile, written);
    }

    [TestMethod]
    public async Task Size_LoginAcceptedUncheckedInAShareGrantingEveryone_IsTheFileSize()
    {
        var written = await ServeAsAsync("ann", PasswordLoginVerdict.AcceptedUnchecked, "SIZE team/notes.txt\r\n", ShareStore(Grants((ContentShare.EveryoneMember, ContentShareAccess.Read))));

        Assert.AreEqual("213 7\r\n", written);
    }

    [TestMethod]
    public async Task Size_ShareWithNoAccessRows_IsServedAsBeforeShareAccess()
    {
        var written = await ServeAsAsync("anonymous", PasswordLoginVerdict.AcceptedUnchecked, "SIZE team/notes.txt\r\n", ShareStore(grants: null));

        Assert.AreEqual("213 7\r\n", written);
    }

    [TestMethod]
    [DataRow("ftp", "ftp", "213 7\r\n")]
    [DataRow("ftps", "ftp", NoSuchFile)]
    [DataRow("ftps", "ftps", "213 7\r\n")]
    public async Task Size_ShareLimitedToAScheme_IsServedOnlyOnThatScheme(string shareScheme, string listeningScheme, string expectedReply)
    {
        var store = ShareStore(Grants(("ann", ContentShareAccess.Read)), schemes: [shareScheme]);

        var written = await ServeAsAsync("ann", PasswordLoginVerdict.Accepted, "SIZE team/notes.txt\r\n", store, scheme: listeningScheme);

        Assert.AreEqual(expectedReply, written);
    }

    // Logs in as userName, the policy answering verdict, then serves commands and returns what the
    // server wrote after the login's replies.
    private async Task<string> ServeAsAsync(
        string userName, PasswordLoginVerdict verdict, string commands, ContentStore contentStore, IDataConnectionOpener? dataConnections = null, string scheme = "ftp")
    {
        var login = $"USER {userName}\r\nPASS secret\r\n";
        var connection = new InMemoryConnection(Ascii(login + commands));
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: dataConnections, scheme: scheme);

        await Server(new UnitTestRecordingAuthenticationPolicy(verdict), contentStore).ServeAsync(connection, context);

        var written = Text(connection.WrittenBytes);
        Assert.StartsWith(Greeting + AnonymousLoginReplies, written);
        return written[(Greeting + AnonymousLoginReplies).Length..];
    }

    private static Dictionary<string, ContentShareAccess> Grants(params (string Member, ContentShareAccess Access)[] grants) =>
        grants.ToDictionary(grant => grant.Member, grant => grant.Access, StringComparer.Ordinal);

    private static InMemoryContentFileSystem ShareFileSystem()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        fileSystem.CreateDirectory(SharePath);
        using var file = fileSystem.CreateFileForAsyncWrite(Path.Join(SharePath, "notes.txt"));
        file.Write(Encoding.ASCII.GetBytes(SharedText));

        return fileSystem;
    }

    private static ContentStore ShareStore(
        Dictionary<string, ContentShareAccess>? grants, InMemoryContentFileSystem? shareFileSystem = null, string[]? schemes = null)
    {
        var share = new ContentShare(ShareName, SharePath, shareFileSystem ?? ShareFileSystem())
        {
            Grants = grants,
            Schemes = schemes is null ? null : new HashSet<string>(schemes, StringComparer.Ordinal),
        };
        var options = new ContentExposureOptions { AllowUploads = true, ListDirectories = true };

        return new ContentStore(InMemoryContentFileSystem.RootPath, StandardFileSystem(), options, [share], []);
    }

    private static string ReadShareFile(InMemoryContentFileSystem shareFileSystem)
    {
        using var file = shareFileSystem.OpenFileForAsyncRead(Path.Join(SharePath, "notes.txt"));
        using var copy = new MemoryStream();
        file.CopyTo(copy);

        return Encoding.UTF8.GetString(copy.ToArray());
    }

    private static InMemoryDataConnections Passive(IConnection dataConnection) =>
        new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, dataConnection);
}
