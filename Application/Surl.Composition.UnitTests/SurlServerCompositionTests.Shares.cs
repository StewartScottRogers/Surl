using Surl.Cli;
using Surl.Content;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Smb;
using Surl.Protocol.Tftp;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    private static readonly string SharedTeamPath = Path.Combine(Path.GetTempPath(), "surl-start-shares", "team");

    [TestMethod]
    public async Task ComposeAsync_SharesAccessAndBuckets_ReachTheOneContentStoreEveryFileServerHolds()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var seams = new StartTestSeams().Seams with { CanOpenShareDirectory = _ => true, ShareFileSystem = fileSystem };
        var commandLine = StartTestSeams.Parse(
            "--user", "alice:secret", "--user", "bob:secret", "--group", "staff=alice",
            "http://127.0.0.1:0", "ftp://127.0.0.1:0", "tftp://127.0.0.1:0", "smb://127.0.0.1:0", "gopher://127.0.0.1:0") with
        {
            Shares = [new ConfiguredShare("team", SharedTeamPath, IsReadOnly: false, ["ftp"])],
            ShareAccess = [new ConfiguredShareAccess("team", [new ConfiguredGrant(new ConfiguredMember("staff", true), "rw")])],
            Buckets = [new ConfiguredBucket("photos", "/team/photos")],
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var contentStore = serving.Stores.Content;
        var share = contentStore.Shares.Single();
        Assert.AreEqual(Path.GetFullPath(SharedTeamPath), share.Path);
        Assert.AreSame(fileSystem, share.FileSystem);
        Assert.AreEqual(ContentShareAccess.ReadWrite, share.Grants!["alice"]);
        Assert.IsFalse(share.Grants.ContainsKey("bob"));
        Assert.AreEqual("/team/photos", contentStore.Buckets.Single().ContentPath);
        Assert.IsTrue(share.IsVisibleTo("ftps"));
        Assert.IsFalse(share.IsVisibleTo("http"));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<HttpProtocolServer>().First()));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<FtpProtocolServer>().Single()));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<TftpProtocolServer>().Single()));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<SmbProtocolServer>().Single()));
        Assert.AreSame(contentStore, ComposedServerFields.Single<ContentStore>(serving.Servers.OfType<GopherProtocolServer>().Single()));
    }

    [TestMethod]
    public async Task ComposeAsync_SharePathThatCannotBeOpened_IsRefusedWithCouldNotReadFileBeforeAnyListenerBinds()
    {
        var seams = new StartTestSeams().Seams with { CanOpenShareDirectory = _ => false };
        var commandLine = StartTestSeams.Parse("http://127.0.0.1:0") with
        {
            Shares = [new ConfiguredShare("team", SharedTeamPath, false, null)],
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"(37) Could not open directory {SharedTeamPath} for share team" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_ShareGrantingEveryone_WritesItsWarningAndVerboseNotes()
    {
        var seams = new StartTestSeams();
        var commandLine = StartTestSeams.Parse("-v", "http://127.0.0.1:0") with
        {
            Shares = [new ConfiguredShare("team", SharedTeamPath, true, null)],
            ShareAccess = [new ConfiguredShareAccess("team", [new ConfiguredGrant(new ConfiguredMember("everyone", true), "r")])],
            Buckets = [new ConfiguredBucket("photos", "/team/photos")],
        };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = await SurlServerComposition.ComposeAsync(
            commandLine, seams.Seams with { CanOpenShareDirectory = _ => true }, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        var lines = log.ToString().Split(Environment.NewLine);
        CollectionAssert.Contains(lines, "surl: warning: --share-access: share team grants r to every caller, anonymous included");
        CollectionAssert.Contains(lines, $"* Share team: {Path.GetFullPath(SharedTeamPath)}, read-only, access 1 grants");
        CollectionAssert.Contains(lines, "* Bucket photos: /team/photos");
    }
}
