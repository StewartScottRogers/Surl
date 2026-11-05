using System.Text;
using Surl.Content;
using static Surl.Protocol.Ssh.SftpTestPackets;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The SSH server's content handlers pass the login's account and their scheme to the content
/// store, which checks the share's access (ADR-0102 decisions 2.2 and 2.3): the share <c>team</c>
/// grants <c>alice</c> <c>r</c> and <c>bob</c> <c>rw</c>; <c>carol</c> and an unchecked login get
/// nothing. Each request is built by hand from draft-ietf-secsh-filexfer-02 and ADR-0054's SCP
/// lines, and each refusal is one ADR-0102 decision 2.3's table names.
/// </summary>
[TestClass]
public sealed class SshContentChannelHandlersTests
{
    private const uint CurlUpload = 0x1A; // WRITE|CREAT|TRUNC

    private readonly ManualTimeProvider clock = new();

    private InMemoryContentFileSystem shareFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ForSftp_ReadAccess_ReadsAndListsButEveryWriteIsPermissionDenied()
    {
        var run = await RunSftpAsync(
            "alice",
            Open(1, "/team/doc.txt"),
            Read(2, 0, 0, 100),
            Request(11, 3, Text("/team")),
            Request(3, 4, Text("/team/new.txt"), UInt32(CurlUpload), UInt32(0)),
            Request(13, 5, Text("/team/doc.txt")),
            Request(18, 6, Text("/team/doc.txt"), Text("/team/moved.txt")));

        CollectionAssert.AreEqual(HandleReply(1, 0), run[0]);
        CollectionAssert.AreEqual(DataReply(2, "kept"), run[1]);
        CollectionAssert.AreEqual(HandleReply(3, 1), run[2]);
        CollectionAssert.AreEqual(Status(4, 3, "Permission denied"), run[3]);
        CollectionAssert.AreEqual(Status(5, 3, "Permission denied"), run[4]);
        CollectionAssert.AreEqual(Status(6, 3, "Permission denied"), run[5]);
        CollectionAssert.AreEqual(new[] { "doc.txt" }, ShareNames());
    }

    [TestMethod]
    public async Task ForSftp_ReadWriteAccess_Uploads()
    {
        var run = await RunSftpAsync(
            "bob",
            Request(3, 1, Text("/team/new.txt"), UInt32(CurlUpload), UInt32(0)),
            Request(6, 2, Handle(0), UInt64(0), Text("hello")),
            Request(4, 3, Handle(0)));

        CollectionAssert.AreEqual(HandleReply(1, 0), run[0]);
        CollectionAssert.AreEqual(Status(2, 0, "Success"), run[1]);
        CollectionAssert.AreEqual(Status(3, 0, "Success"), run[2]);
        CollectionAssert.AreEqual(new[] { "doc.txt", "new.txt" }, ShareNames());
    }

    [TestMethod]
    [DataRow("carol")]
    [DataRow(null)]
    [DataRow("")]
    public async Task ForSftp_NoAccess_FindsTheShareMissing(string? accountName)
    {
        var run = await RunSftpAsync(
            accountName,
            Open(1, "/team/doc.txt"),
            Request(17, 2, Text("/team/doc.txt")),
            Request(11, 3, Text("/team")));

        CollectionAssert.AreEqual(Status(1, 2, "No such file"), run[0]);
        CollectionAssert.AreEqual(Status(2, 2, "No such file"), run[1]);
        CollectionAssert.AreEqual(Status(3, 2, "No such file"), run[2]);
    }

    [TestMethod]
    public async Task ForScp_ReadAccess_DownloadsButAnUploadIsPermissionDenied()
    {
        var download = await RunScpAsync("alice", new SshScpCommand(true, false, false, "/team/doc.txt"), [0], [0]);
        var upload = await RunScpAsync("alice", Sink("/team/new.txt"), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(0u, download.Exit);
        Assert.AreEqual("C0644 4 doc.txt\nkept\0", download.Text);
        Assert.AreEqual(1u, upload.Exit);
        Assert.AreEqual("\0\u0001scp: /team/new.txt: Permission denied\n", upload.Text);
        CollectionAssert.AreEqual(new[] { "doc.txt" }, ShareNames());
    }

    [TestMethod]
    public async Task ForScp_ReadWriteAccess_Uploads()
    {
        var upload = await RunScpAsync("bob", Sink("/team/new.txt"), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(0u, upload.Exit);
        CollectionAssert.AreEqual(new[] { "doc.txt", "new.txt" }, ShareNames());
    }

    [TestMethod]
    [DataRow("carol")]
    [DataRow(null)]
    public async Task ForScp_NoAccess_FindsTheShareMissing(string? accountName)
    {
        var download = await RunScpAsync(accountName, new SshScpCommand(true, false, false, "/team/doc.txt"), [0]);
        var upload = await RunScpAsync(accountName, Sink("/team/new.txt"), Ascii("C0644 5 new.txt\n"), Ascii("hello"));

        Assert.AreEqual(1u, download.Exit);
        Assert.AreEqual("\u0001scp: /team/doc.txt: No such file or directory\n", download.Text);
        Assert.AreEqual(1u, upload.Exit);
        Assert.AreEqual("\0\u0001scp: /team/new.txt: No such file or directory\n", upload.Text);
        CollectionAssert.AreEqual(new[] { "doc.txt" }, ShareNames());
    }

    private static SshScpCommand Sink(string path) => new(false, false, false, path);

    private async Task<List<byte[]>> RunSftpAsync(string? accountName, params byte[][] requests)
    {
        var channel = new SftpTestChannel([Init3, .. requests]);
        var handler = new SshContentChannelHandlers(StoreWithTeamShare()).ForSftp(accountName, Context(clock, TestContext.CancellationToken))!;

        Assert.AreEqual(0u, await handler.RunAsync(channel, TestContext.CancellationToken));

        return channel.WrittenPackets()[1..];
    }

    private async Task<(uint Exit, string Text)> RunScpAsync(string? accountName, SshScpCommand command, params byte[][] inbound)
    {
        var channel = new SftpTestChannel(inbound);
        var handler = new SshContentChannelHandlers(StoreWithTeamShare()).ForScp(command, accountName, Context(clock, TestContext.CancellationToken))!;

        var exit = await handler.RunAsync(channel, TestContext.CancellationToken);

        return (exit, Encoding.UTF8.GetString([.. channel.Written]));
    }

    // The root holds the standard files; the share team holds doc.txt, read by alice, written by bob.
    private ContentStore StoreWithTeamShare()
    {
        shareFiles ??= NewShareFiles();
        var rootFiles = new InMemoryContentFileSystem(clock);
        Store(new(), rootFiles);
        var team = new ContentShare("team", InMemoryContentFileSystem.RootPath, shareFiles)
        {
            Grants = new Dictionary<string, ContentShareAccess>
            {
                ["alice"] = ContentShareAccess.Read,
                ["bob"] = ContentShareAccess.ReadWrite,
            },
        };

        return new ContentStore(InMemoryContentFileSystem.RootPath, rootFiles, new() { AllowUploads = true, ListDirectories = true }, [team], []);
    }

    private InMemoryContentFileSystem NewShareFiles()
    {
        var files = new InMemoryContentFileSystem(clock);
        WriteFile(files, "kept", "doc.txt");

        return files;
    }

    private string[] ShareNames() =>
        [.. shareFiles.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).Order(StringComparer.Ordinal)];
}
