using System.Text;
using Surl.Content;
using static Surl.Protocol.Tftp.UploadExchange;

namespace Surl.Protocol.Tftp;

/// <summary>
/// TFTP has no login, so the server hands the content store an anonymous caller on <c>tftp</c>
/// (ADR-0102 decision 2.2): a share closed to anonymous callers, or limited to other schemes, is
/// a missing file (ERROR 1), and a write in a share anonymous may only read, or in a
/// <c>readonly</c> share, is ERROR 2 before any ACK (ADR-0102 decision 2.3).
/// </summary>
[TestClass]
public sealed class TftpShareAccessTests
{
    private const string ShareName = "team";
    private const string SharedText = "shared\n";

    private static readonly byte[] FileNotFound = [0, 5, 0, 1, .. "File not found"u8, 0];
    private static readonly string SharePath = Path.Join(Path.GetTempPath(), "surl-tftp-share-tests");
    private static readonly string SharedFilePath = Path.Join(SharePath, "notes.txt");
    private static readonly DateTimeOffset FileTime = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Rrq_ShareOpenToEveryone_SendsTheSharedFile()
    {
        var (flow, _) = await ServeAsync(ShareStore(Grants(ContentShareAccess.Read)), ReadRequest("team/notes.txt"), [ScriptedDatagramFlow.Datagram(Ack(1))]);

        AssertSent([Data(1, Encoding.ASCII.GetBytes(SharedText))], flow);
    }

    [TestMethod]
    public async Task Rrq_ShareWithNoAccessRows_IsServedAsBeforeShareAccess()
    {
        var (flow, _) = await ServeAsync(ShareStore(grants: null), ReadRequest("team/notes.txt"), [ScriptedDatagramFlow.Datagram(Ack(1))]);

        AssertSent([Data(1, Encoding.ASCII.GetBytes(SharedText))], flow);
    }

    [TestMethod]
    public async Task Rrq_ShareClosedToAnonymous_IsTheReplyAMissingFileGets()
    {
        var store = ShareStore(new Dictionary<string, ContentShareAccess>(StringComparer.Ordinal) { ["ann"] = ContentShareAccess.ReadWrite });

        var (flow, log) = await ServeAsync(store, ReadRequest("team/notes.txt"), []);
        var (missingFlow, _) = await ServeAsync(store, ReadRequest("missing.txt"), []);

        AssertSent([FileNotFound], flow);
        AssertSent([FileNotFound], missingFlow);
        Assert.EndsWith("; answered with ERROR 1.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("tftp", true)]
    [DataRow("ftp", false)]
    public async Task Rrq_ShareLimitedToAScheme_IsServedOnlyOverThatScheme(string shareScheme, bool isServed)
    {
        var store = ShareStore(Grants(ContentShareAccess.Read), schemes: [shareScheme]);

        var (flow, _) = await ServeAsync(store, ReadRequest("team/notes.txt"), [ScriptedDatagramFlow.Datagram(Ack(1))]);

        AssertSent([isServed ? Data(1, Encoding.ASCII.GetBytes(SharedText)) : FileNotFound], flow);
    }

    [TestMethod]
    public async Task Wrq_ShareAnonymousMayOnlyRead_AnswersError2BeforeAnyAckAndKeepsTheFile()
    {
        var shareFileSystem = ShareFileSystem();

        var (flow, log) = await ServeAsync(ShareStore(Grants(ContentShareAccess.Read), shareFileSystem), WriteRequest("team/notes.txt"), []);

        AssertSent([AccessViolation], flow);
        Assert.AreEqual(SharedText, Encoding.ASCII.GetString(shareFileSystem.ReadFile(SharedFilePath)!));
        Assert.EndsWith("; answered with ERROR 2.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Wrq_ReadOnlyShare_AnswersError2BeforeAnyAck()
    {
        var shareFileSystem = ShareFileSystem();

        var (flow, _) = await ServeAsync(ShareStore(Grants(ContentShareAccess.ReadWrite), shareFileSystem, isReadOnly: true), WriteRequest("team/notes.txt"), []);

        AssertSent([AccessViolation], flow);
        Assert.AreEqual(SharedText, Encoding.ASCII.GetString(shareFileSystem.ReadFile(SharedFilePath)!));
    }

    [TestMethod]
    public async Task Wrq_ShareClosedToAnonymous_AnswersError2()
    {
        var store = ShareStore(new Dictionary<string, ContentShareAccess>(StringComparer.Ordinal) { ["ann"] = ContentShareAccess.ReadWrite });

        var (flow, _) = await ServeAsync(store, WriteRequest("team/notes.txt"), []);

        AssertSent([AccessViolation], flow);
    }

    [TestMethod]
    public async Task Wrq_ShareGrantingEveryoneReadWrite_WritesTheUpload()
    {
        var shareFileSystem = ShareFileSystem();

        var (flow, _) = await ServeAsync(
            ShareStore(Grants(ContentShareAccess.ReadWrite), shareFileSystem), WriteRequest("team/notes.txt"), [ScriptedDatagramFlow.Datagram(Data(1, "new\n"u8.ToArray()))]);

        AssertSent([Ack(0), Ack(1)], flow);
        Assert.AreEqual("new\n", Encoding.ASCII.GetString(shareFileSystem.ReadFile(SharedFilePath)!));
    }

    private static Dictionary<string, ContentShareAccess> Grants(ContentShareAccess everyoneAccess) =>
        new(StringComparer.Ordinal) { [ContentShare.EveryoneMember] = everyoneAccess };

    private static UnitTestInMemoryContentFileSystem ShareFileSystem() => new UnitTestInMemoryContentFileSystem()
        .AddDirectory(SharePath)
        .AddFile(SharedFilePath, Encoding.ASCII.GetBytes(SharedText), FileTime);

    private static ContentStore ShareStore(
        Dictionary<string, ContentShareAccess>? grants, UnitTestInMemoryContentFileSystem? shareFileSystem = null, string[]? schemes = null, bool isReadOnly = false)
    {
        var share = new ContentShare(ShareName, SharePath, shareFileSystem ?? ShareFileSystem())
        {
            Grants = grants,
            Schemes = schemes is null ? null : new HashSet<string>(schemes, StringComparer.Ordinal),
            IsReadOnly = isReadOnly,
        };

        return new ContentStore(Root, FileSystem(), new ContentExposureOptions { AllowUploads = true }, [share], []);
    }

    private static byte[] ReadRequest(string fileName) => [0, 1, .. Encoding.UTF8.GetBytes(fileName + "\0octet\0")];
}
