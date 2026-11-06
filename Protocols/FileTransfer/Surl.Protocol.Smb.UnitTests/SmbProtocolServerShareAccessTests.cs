using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// The session's account (or an anonymous caller for a login <c>--allow-anonymous</c> accepted
/// unchecked) and scheme handed to the content store's share access check on tree connect,
/// create, read and write, with ADR-0102 decision 2.3's replies: a share the caller may not read
/// is <c>ERRSRV/ERRinvnetname</c> as a missing one is, and a write it may not make is
/// <c>ERRDOS/ERRnoaccess</c>. Requests are laid out as upstream curl 8.21.0 sends them.
/// </summary>
[TestClass]
public sealed class SmbProtocolServerShareAccessTests
{
    private const uint FileCreated = 2;

    private static readonly byte[] Upload = Encoding.ASCII.GetBytes("upload me\n");

    public TestContext TestContext { get; set; } = null!;

    private static string CloseResponseHex => ResponseHex(SmbCommand.Close, 0, 1, SmbSession.UserId);

    [TestMethod]
    public async Task NtCreate_AReadAccount_ReadsTheShareAndItsUploadIsNoAccess()
    {
        var shareFiles = ShareFiles();
        var store = StoreWith(TeamShare(shareFiles, ("alice", ContentShareAccess.Read)));

        var written = await ServeOnTeamAsync(
            store, new SecretPasswordPolicy(), NtCreateHex(1, "doc.txt"), ReadHex(1, 1, 0), CloseHex(1, 1), UploadNtCreateHex("up.txt"));

        Assert.AreEqual(
            TreeConnectResponseHex(1) + NtCreateResponseHex(1, 5) + ReadResponseHex("team\n"u8) + CloseResponseHex
            + ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.NoAccess),
            written);
        Assert.IsNull(StoredBytes(shareFiles, "up.txt"));
    }

    [TestMethod]
    public async Task Write_AReadWriteAccount_StoresTheUploadInTheShare()
    {
        var shareFiles = ShareFiles();
        var store = StoreWith(TeamShare(shareFiles, ("bob", ContentShareAccess.ReadWrite)));

        var written = await ServeOnTeamAsync(
            store, AcceptedAs("bob"), UploadNtCreateHex("up.txt"), WriteHex(1, 0, Upload), CloseHex(1, 1));

        Assert.AreEqual(
            TreeConnectResponseHex(1) + UploadNtCreateResponseHex(1, FileCreated) + WriteResponseHex((ushort)Upload.Length) + CloseResponseHex,
            written);
        CollectionAssert.AreEqual(Upload, StoredBytes(shareFiles, "up.txt"));
    }

    [TestMethod]
    public async Task TreeConnect_AnAccountWithNoGrant_IsInvalidNetworkNameAsAMissingShareIs()
    {
        var log = new RecordingExchangeLog();
        var store = StoreWith(TeamShare(ShareFiles(), ("alice", ContentShareAccess.ReadWrite)));

        var written = await ServeOnTeamAsync(store, AcceptedAs("carol"), log, TreeConnectHex("nosuch"));

        Assert.AreEqual(
            ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.InvalidNetworkName, treeId: 0)
            + ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.InvalidNetworkName, treeId: 0),
            written);
        CollectionAssert.AreEqual(
            new[] { "SMB tree connect team: no such share", "SMB tree connect nosuch: no such share" },
            log.Notes.Where(note => note.StartsWith("SMB tree connect", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task TreeConnect_AnAccountWithNoGrant_StillReachesARootFolder()
    {
        var store = StoreWith(TeamShare(ShareFiles(), ("alice", ContentShareAccess.ReadWrite)));

        var written = await ServeOnTeamAsync(store, AcceptedAs("carol"), TreeConnectHex("share"), NtCreateHex(1, "file.txt"));

        Assert.AreEqual(
            ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.InvalidNetworkName, treeId: 0) + TreeConnectResponseHex(1) + NtCreateResponseHex(1, 10),
            written);
    }

    [TestMethod]
    [DataRow(SmbLoginOutcome.AcceptedUnchecked, null, DisplayName = "A guest --allow-anonymous accepted unchecked")]
    [DataRow(SmbLoginOutcome.Accepted, "", DisplayName = "An accepted login naming no account")]
    public async Task TreeConnect_AnAnonymousCallerOnAShareWithGrants_IsInvalidNetworkName(SmbLoginOutcome outcome, string? accountName)
    {
        var store = StoreWith(TeamShare(ShareFiles(), ("alice", ContentShareAccess.ReadWrite)));

        var written = await ServeOnTeamAsync(store, new FixedVerdictPolicy(new SmbLoginVerdict(outcome, accountName, null)));

        Assert.AreEqual(ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.InvalidNetworkName, treeId: 0), written);
    }

    [TestMethod]
    public async Task NtCreate_AGuestOnAShareGrantingEveryoneRead_ReadsButCannotUpload()
    {
        var shareFiles = ShareFiles();
        var store = StoreWith(TeamShare(shareFiles, (ContentShare.EveryoneMember, ContentShareAccess.Read)));

        var written = await ServeOnTeamAsync(store, Guest(), NtCreateHex(1, "doc.txt"), UploadNtCreateHex("up.txt"));

        Assert.AreEqual(
            TreeConnectResponseHex(1) + NtCreateResponseHex(1, 5) + ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.NoAccess),
            written);
        Assert.IsNull(StoredBytes(shareFiles, "up.txt"));
    }

    [TestMethod]
    public async Task NtCreate_AGuestOnAShareWithNoGrants_ReadsAndUploadsAsToday()
    {
        var shareFiles = ShareFiles();
        var store = StoreWith(new ContentShare("team", InMemoryContentFileSystem.RootPath, shareFiles));

        var written = await ServeOnTeamAsync(store, Guest(), NtCreateHex(1, "doc.txt"), UploadNtCreateHex("up.txt"), WriteHex(2, 0, Upload), CloseHex(1, 2));

        Assert.AreEqual(
            TreeConnectResponseHex(1) + NtCreateResponseHex(1, 5) + UploadNtCreateResponseHex(2, FileCreated)
            + WriteResponseHex((ushort)Upload.Length) + CloseResponseHex,
            written);
        CollectionAssert.AreEqual(Upload, StoredBytes(shareFiles, "up.txt"));
    }

    [TestMethod]
    [DataRow("smbs", true, DisplayName = "smbs, the scheme the share is limited to")]
    [DataRow("smb", false, DisplayName = "smb, a scheme the share is not limited to")]
    public async Task TreeConnect_AShareLimitedToSmbs_IsReachedOnlyOverSmbs(string scheme, bool isReached)
    {
        var share = new ContentShare("team", InMemoryContentFileSystem.RootPath, ShareFiles())
        {
            Schemes = new HashSet<string>(StringComparer.Ordinal) { "smbs" },
            Grants = new Dictionary<string, ContentShareAccess>(StringComparer.Ordinal) { ["alice"] = ContentShareAccess.Read },
        };

        var written = await ServeOnTeamOverAsync(scheme, StoreWith(share), new SecretPasswordPolicy());

        Assert.AreEqual(isReached ? TreeConnectResponseHex(1) : ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.InvalidNetworkName, treeId: 0), written);
    }

    private Task<string> ServeOnTeamAsync(ContentStore store, ISmbAuthenticationPolicy policy, params string[] requestsHex) =>
        ServeOnTeamOverAsync("smb", store, policy, null, requestsHex);

    private Task<string> ServeOnTeamAsync(ContentStore store, ISmbAuthenticationPolicy policy, IExchangeLog? log, params string[] requestsHex) =>
        ServeOnTeamOverAsync("smb", store, policy, log, requestsHex);

    // Serves the negotiate, the session setup the policy judges and a tree connect to team, then
    // the requests; returns what the server wrote after the session setup response.
    private async Task<string> ServeOnTeamOverAsync(string scheme, ContentStore store, ISmbAuthenticationPolicy policy, IExchangeLog? log = null, params string[] requestsHex)
    {
        var written = await ServeAsync(
            TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, TreeConnectHex("team"), .. requestsHex], policy, log, contentStore: store, scheme: scheme);
        Assert.StartsWith(NegotiateResponseHex, written);
        return written[(NegotiateResponseHex.Length + SessionSetupResponseHex.Length)..];
    }

    private static FixedVerdictPolicy AcceptedAs(string accountName) =>
        new(new SmbLoginVerdict(SmbLoginOutcome.Accepted, accountName, new CheckedLogin("ntlmv1", accountName, IsAccepted: true)));

    private static FixedVerdictPolicy Guest() => new(new SmbLoginVerdict(SmbLoginOutcome.AcceptedUnchecked, null, null));

    // The share's own files: doc.txt holding "team\n".
    private static InMemoryContentFileSystem ShareFiles()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        using var file = fileSystem.CreateFileForAsyncWrite(Path.Join(InMemoryContentFileSystem.RootPath, "doc.txt"));
        file.Write("team\n"u8);
        return fileSystem;
    }

    private static ContentShare TeamShare(InMemoryContentFileSystem shareFiles, params (string Member, ContentShareAccess Access)[] grants) =>
        new("team", InMemoryContentFileSystem.RootPath, shareFiles)
        {
            Grants = grants.ToDictionary(grant => grant.Member, grant => grant.Access, StringComparer.Ordinal),
        };

    // The standard root, uploads on, with the share team beside its folders.
    private static ContentStore StoreWith(ContentShare share) =>
        new(InMemoryContentFileSystem.RootPath, StandardFileSystem(), new ContentExposureOptions { AllowUploads = true }, [share], []);
}
