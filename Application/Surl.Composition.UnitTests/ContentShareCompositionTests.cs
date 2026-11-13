using Surl.Cli;
using Surl.Content;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ContentShareCompositionTests
{
    private static readonly string TeamPath = Path.Combine(Path.GetTempPath(), "surl-shares", "team");
    private static readonly string MediaPath = Path.Combine(Path.GetTempPath(), "surl-shares", "media");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Compose_NullArguments_Throw()
    {
        var groups = Groups();
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.Compose(null!, groups, _ => true, fileSystem));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.Compose(new SurlCommandLine(), null!, _ => true, fileSystem));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.Compose(new SurlCommandLine(), groups, null!, fileSystem));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.Compose(new SurlCommandLine(), groups, _ => true, null!));
    }

    [TestMethod]
    public void Compose_NoShareOrBucket_ComposesNone()
    {
        var (shares, buckets, exitCode, failure) = Compose(new SurlCommandLine());

        Assert.IsEmpty(shares!);
        Assert.IsEmpty(buckets!);
        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Compose_ShareWithFlagsAndGroupAccess_ReachesTheShareExpanded()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var commandLine = new SurlCommandLine
        {
            Shares = [new ConfiguredShare("team", TeamPath, IsReadOnly: true, ["ftp", "sftp"])],
            ShareAccess = [Access("team", ("staff", true, "rw"), ("carol", false, "r"), ("everyone", true, "r"))],
        };

        var (shares, _, _, failure) = ContentShareComposition.Compose(commandLine, Groups(), _ => true, fileSystem);

        Assert.IsNull(failure);
        var share = shares!.Single();
        Assert.AreEqual("team", share.Name);
        Assert.AreEqual(Path.GetFullPath(TeamPath), share.Path);
        Assert.AreSame(fileSystem, share.FileSystem);
        Assert.IsTrue(share.IsReadOnly);
        CollectionAssert.AreEquivalent(new[] { "ftp", "ftps", "sftp", "sftps" }, share.Schemes!.ToArray());
        Assert.AreEqual(ContentShareAccess.ReadWrite, share.Grants!["alice"]);
        Assert.AreEqual(ContentShareAccess.ReadWrite, share.Grants["bob"]);
        Assert.AreEqual(ContentShareAccess.Read, share.Grants["carol"]);
        Assert.AreEqual(ContentShareAccess.Read, share.Grants[ContentShare.EveryoneMember]);
        Assert.HasCount(4, share.Grants);
    }

    [TestMethod]
    public void Compose_ShareNoAccessNames_IsOpenToEveryScheme()
    {
        var (shares, _, _, _) = Compose(new SurlCommandLine { Shares = [new ConfiguredShare("team", TeamPath, false, null)] });

        Assert.IsNull(shares!.Single().Grants);
        Assert.IsNull(shares!.Single().Schemes);
        Assert.IsFalse(shares!.Single().IsReadOnly);
    }

    [TestMethod]
    public void Compose_TwoRowsGrantingOneMember_KeepTheWiderAccess()
    {
        var commandLine = new SurlCommandLine
        {
            Shares = [new ConfiguredShare("team", TeamPath, false, null)],
            ShareAccess = [Access("team", ("alice", false, "rw")), Access("team", ("alice", false, "r"), ("bob", false, "r"))],
        };

        var share = Compose(commandLine).Shares!.Single();

        Assert.AreEqual(ContentShareAccess.ReadWrite, share.Grants!["alice"]);
        Assert.AreEqual(ContentShareAccess.Read, share.Grants["bob"]);
    }

    [TestMethod]
    public void Compose_AccessMemberNamingNothing_IsRefusedWithFailedInit()
    {
        var commandLine = new SurlCommandLine
        {
            Shares = [new ConfiguredShare("team", TeamPath, false, null)],
            ShareAccess = [Access("team", ("nobody", true, "r"))],
        };

        AssertRefused(commandLine, SurlExitCode.FailedInit, "(2) option --share-access: member @nobody names no account or group");
    }

    [TestMethod]
    public void Compose_ShareNamedLikeTheServiceStateFolder_IsRefusedWithFailedInit() =>
        AssertRefused(
            new SurlCommandLine { Shares = [new ConfiguredShare(".surl", TeamPath, false, null)] },
            SurlExitCode.FailedInit,
            "(2) option --share: share .surl is not a valid folder name");

    [TestMethod]
    public void Compose_SharePathThatCannotBeOpened_IsRefusedWithCouldNotReadFile()
    {
        var commandLine = new SurlCommandLine { Shares = [new ConfiguredShare("team", TeamPath, false, null)] };
        string? probed = null;

        var (shares, _, exitCode, failure) = ContentShareComposition.Compose(
            commandLine, Groups(), path => { probed = path; return false; }, new InMemoryContentFileSystem(TimeProvider.System));

        Assert.IsNull(shares);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual($"(37) Could not open directory {TeamPath} for share team", failure);
        Assert.AreEqual(Path.GetFullPath(TeamPath), probed);
    }

    [TestMethod]
    public void Compose_ShareInsideTheDataDirectory_IsRefusedWithFailedInit() =>
        AssertRefused(
            new SurlCommandLine
            {
                DataDirectory = Path.Combine(Path.GetTempPath(), "surl-shares"),
                Shares = [new ConfiguredShare("team", TeamPath, false, null)],
            },
            SurlExitCode.FailedInit,
            "(2) option --share: share team overlaps --directory");

    [TestMethod]
    public void Compose_ShareHoldingAnEarlierShare_IsRefusedWithFailedInit() =>
        AssertRefused(
            new SurlCommandLine
            {
                Shares =
                [
                    new ConfiguredShare("team", TeamPath, false, null),
                    new ConfiguredShare("all", Path.Combine(Path.GetTempPath(), "surl-shares"), false, null),
                ],
            },
            SurlExitCode.FailedInit,
            "(2) option --share: share all overlaps team");

    [TestMethod]
    public void Compose_ShareAtTheSamePathInAnotherCase_IsRefusedWithFailedInit() =>
        AssertRefused(
            new SurlCommandLine
            {
                Shares = [new ConfiguredShare("team", TeamPath, false, null), new ConfiguredShare("again", TeamPath.ToUpperInvariant() + Path.DirectorySeparatorChar, false, null)],
            },
            SurlExitCode.FailedInit,
            "(2) option --share: share again overlaps team");

    [TestMethod]
    public void Compose_ShareAtAFileSystemRoot_OverlapsEveryLaterShare() =>
        AssertRefused(
            new SurlCommandLine
            {
                Shares = [new ConfiguredShare("root", Path.GetPathRoot(Path.GetFullPath(TeamPath))!, false, null), new ConfiguredShare("team", TeamPath, false, null)],
            },
            SurlExitCode.FailedInit,
            "(2) option --share: share team overlaps root");

    [TestMethod]
    public void Compose_SiblingSharesAndAnUnrelatedDataDirectory_DoNotOverlap()
    {
        var commandLine = new SurlCommandLine
        {
            DataDirectory = Path.Combine(Path.GetTempPath(), "surl-data"),
            Shares = [new ConfiguredShare("team", TeamPath, false, null), new ConfiguredShare("team2", TeamPath + "2", false, null), new ConfiguredShare("media", MediaPath, false, null)],
        };

        var (shares, _, _, failure) = Compose(commandLine);

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { "team", "team2", "media" }, shares!.Select(share => share.Name).ToArray());
    }

    [TestMethod]
    public void Compose_Buckets_ReachTheStoreRecordsEncodedAsRequestPaths()
    {
        var commandLine = new SurlCommandLine
        {
            Buckets = [new ConfiguredBucket("photos", "/team/photos"), new ConfiguredBucket("albums", "/my albums")],
        };

        var (_, buckets, _, failure) = Compose(commandLine);

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { "photos", "albums" }, buckets!.Select(bucket => bucket.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "/team/photos", "/my%20albums" }, buckets!.Select(bucket => bucket.ContentPath).ToArray());
    }

    [TestMethod]
    public void Compose_BucketNamedLikeTheServiceStateFolder_IsRefusedWithFailedInit() =>
        AssertRefused(
            new SurlCommandLine { Buckets = [new ConfiguredBucket(".surl", "/media")] },
            SurlExitCode.FailedInit,
            "(2) option --bucket: bucket .surl is not a valid folder name");

    [TestMethod]
    public void Compose_BucketPathARequestMayNotName_IsRefusedWithFailedInit() =>
        AssertRefused(
            new SurlCommandLine { Buckets = [new ConfiguredBucket("photos", "/media/../photos")] },
            SurlExitCode.FailedInit,
            "(2) option --bucket: bucket photos maps to /media/../photos, which is not a content path");

    [TestMethod]
    public void WithTlsTwins_AddsEachSchemesTwin()
    {
        Assert.IsNull(ContentShareComposition.WithTlsTwins(null));
        CollectionAssert.AreEquivalent(new[] { "ftp", "ftps" }, ContentShareComposition.WithTlsTwins(["ftp"])!.ToArray());
        Assert.IsTrue(ContentShareComposition.WithTlsTwins(["gophers"])!.IsSupersetOf(["gopher", "gophers"]));
        Assert.IsTrue(ContentShareComposition.WithTlsTwins(["ws"])!.IsSupersetOf(["ws", "wss"]));
    }

    [TestMethod]
    public void WriteWarnings_ShareGrantingEveryone_WarnsFromInfoUp()
    {
        var store = Store(
            new ContentShare("open", Path.GetFullPath(TeamPath), new InMemoryContentFileSystem(TimeProvider.System))
            {
                Grants = new Dictionary<string, ContentShareAccess> { [ContentShare.EveryoneMember] = ContentShareAccess.ReadWrite },
            },
            new ContentShare("read", Path.GetFullPath(MediaPath), new InMemoryContentFileSystem(TimeProvider.System))
            {
                Grants = new Dictionary<string, ContentShareAccess> { [ContentShare.EveryoneMember] = ContentShareAccess.Read },
            },
            new ContentShare("closed", Path.GetFullPath(TeamPath + "c"), new InMemoryContentFileSystem(TimeProvider.System))
            {
                Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.Read },
            },
            new ContentShare("plain", Path.GetFullPath(TeamPath + "p"), new InMemoryContentFileSystem(TimeProvider.System)));

        Assert.AreEqual(
            "surl: warning: --share-access: share open grants rw to every caller, anonymous included" + Environment.NewLine
            + "surl: warning: --share-access: share read grants r to every caller, anonymous included" + Environment.NewLine,
            Write(log => ContentShareComposition.WriteWarnings(LogLevel.Info, store, log)));
        Assert.AreEqual(string.Empty, Write(log => ContentShareComposition.WriteWarnings(LogLevel.Error, store, log)));
    }

    [TestMethod]
    public void WriteStartLines_SharesAndBuckets_AreNotedFromVerboseUp()
    {
        var teamPath = Path.GetFullPath(TeamPath);
        var mediaPath = Path.GetFullPath(MediaPath);
        var store = Store(
            [
                new ContentShare("team", teamPath, new InMemoryContentFileSystem(TimeProvider.System))
                {
                    IsReadOnly = true,
                    Schemes = new HashSet<string> { "ftps", "ftp" },
                    Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.Read, ["bob"] = ContentShareAccess.ReadWrite },
                },
                new ContentShare("media", mediaPath, new InMemoryContentFileSystem(TimeProvider.System)),
            ],
            [new ContentBucket("photos", "/media/photos")]);

        Assert.AreEqual(
            $"* Share team: {teamPath}, read-only, schemes ftp, ftps, access 2 grants" + Environment.NewLine
            + $"* Share media: {mediaPath}" + Environment.NewLine
            + "* Bucket photos: /media/photos" + Environment.NewLine,
            Write(log => ContentShareComposition.WriteStartLines(LogLevel.Verbose, store, log)));
        Assert.AreEqual(string.Empty, Write(log => ContentShareComposition.WriteStartLines(LogLevel.Info, store, log)));
    }

    [TestMethod]
    public void Writers_NullArguments_Throw()
    {
        var store = Store();
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.WriteWarnings(LogLevel.Info, null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.WriteWarnings(LogLevel.Info, store, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.WriteStartLines(LogLevel.Info, null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.WriteStartLines(LogLevel.Info, store, null!));
    }

    [TestMethod]
    public void PrepareBucketFolders_NullArguments_Throw()
    {
        var store = Store();
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.PrepareBucketFolders(null!, store, fileSystem));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.PrepareBucketFolders(new SurlCommandLine(), null!, fileSystem));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentShareComposition.PrepareBucketFolders(new SurlCommandLine(), store, null!));
    }

    [TestMethod]
    public void PrepareBucketFolders_BucketInsideAShareWhoseFolderIsMissing_IsRefusedForThatBucket()
    {
        var shareFileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        shareFileSystem.CreateDirectory(Path.GetFullPath(TeamPath));
        var commandLine = new SurlCommandLine { Buckets = [new ConfiguredBucket("photos", "/team/photos")] };
        var store = Store([new ContentShare("team", Path.GetFullPath(TeamPath), shareFileSystem)], [new ContentBucket("photos", "/team/photos")]);

        var failure = ContentShareComposition.PrepareBucketFolders(commandLine, store, new InMemoryContentFileSystem(TimeProvider.System));

        Assert.AreEqual("(37) Could not open directory /team/photos for bucket photos", failure);
    }

    [TestMethod]
    public void PrepareBucketFolders_BucketInsideAShareWhoseFolderExists_IsAccepted()
    {
        var shareFileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        shareFileSystem.CreateDirectory(Path.GetFullPath(TeamPath));
        shareFileSystem.CreateDirectory(Path.Combine(Path.GetFullPath(TeamPath), "photos"));
        var commandLine = new SurlCommandLine { Buckets = [new ConfiguredBucket("photos", "/team/photos")] };
        var store = Store([new ContentShare("team", Path.GetFullPath(TeamPath), shareFileSystem)], [new ContentBucket("photos", "/team/photos")]);

        Assert.IsNull(ContentShareComposition.PrepareBucketFolders(commandLine, store, new InMemoryContentFileSystem(TimeProvider.System)));
    }

    [TestMethod]
    public void PrepareBucketFolders_BucketUnderTheDataDirectory_MustBeAnExistingFolder()
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "surl-bucket-data"));
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(dataDirectory);
        var commandLine = new SurlCommandLine { DataDirectory = dataDirectory, Buckets = [new ConfiguredBucket("photos", "/photos")] };
        var store = Store([], [new ContentBucket("photos", "/photos")]);

        Assert.AreEqual(
            "(37) Could not open directory /photos for bucket photos",
            ContentShareComposition.PrepareBucketFolders(commandLine, store, fileSystem));

        fileSystem.CreateDirectory(Path.Combine(dataDirectory, "photos"));

        Assert.IsNull(ContentShareComposition.PrepareBucketFolders(commandLine, store, fileSystem));
    }

    [TestMethod]
    public void PrepareBucketFolders_BucketInTheInMemoryRoot_IsCreatedEmptyAndSeededAgain()
    {
        var commandLine = new SurlCommandLine { Buckets = [new ConfiguredBucket("photos", "/media/photos")] };
        var store = Store([], [new ContentBucket("photos", "/media/photos")]);

        Assert.IsNull(ContentShareComposition.PrepareBucketFolders(commandLine, store, new InMemoryContentFileSystem(TimeProvider.System)));

        var mapping = store.MapRequestPath("/media/photos");
        Assert.AreEqual(ContentEntryKind.Directory, store.GetEntryKind(mapping));
        Assert.IsNull(ContentShareComposition.PrepareBucketFolders(commandLine, store, new InMemoryContentFileSystem(TimeProvider.System)));
    }

    [TestMethod]
    public async Task PrepareBucketFolders_BucketInTheInMemoryRootBehindAFile_IsRefused()
    {
        var commandLine = new SurlCommandLine { Buckets = [new ConfiguredBucket("photos", "/media/photos")] };
        var store = Store([], [new ContentBucket("photos", "/media")]);
        await store.SeedFileAsync("/media", new MemoryStream([1]), TestContext.CancellationToken);

        Assert.AreEqual(
            "(37) Could not open directory /media/photos for bucket photos",
            ContentShareComposition.PrepareBucketFolders(commandLine, store, new InMemoryContentFileSystem(TimeProvider.System)));
    }

    [TestMethod]
    public void PrepareBucketFolders_BucketWithNoSegment_IsLeftToTheInMemoryRoot()
    {
        var commandLine = new SurlCommandLine { Buckets = [new ConfiguredBucket("everything", "/")] };
        var store = Store([], [new ContentBucket("everything", "/")]);

        Assert.IsNull(ContentShareComposition.PrepareBucketFolders(commandLine, store, new InMemoryContentFileSystem(TimeProvider.System)));
    }

    private static AccountGroupExpansion Groups() =>
        AccountGroupExpansion.Compose(
            [new ConfiguredGroup("staff", [new ConfiguredMember("alice", false), new ConfiguredMember("bob", false)])],
            ["alice", "bob", "carol"]).Expansion!;

    private static ConfiguredShareAccess Access(string share, params (string Name, bool IsGroup, string Access)[] grants) =>
        new(share, [.. grants.Select(grant => new ConfiguredGrant(new ConfiguredMember(grant.Name, grant.IsGroup), grant.Access))]);

    private static (IReadOnlyList<ContentShare>? Shares, IReadOnlyList<ContentBucket>? Buckets, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine) =>
        ContentShareComposition.Compose(commandLine, Groups(), _ => true, new InMemoryContentFileSystem(TimeProvider.System));

    private static void AssertRefused(SurlCommandLine commandLine, SurlExitCode expectedExitCode, string expectedFailure)
    {
        var (shares, buckets, exitCode, failure) = Compose(commandLine);

        Assert.IsNull(shares);
        Assert.IsNull(buckets);
        Assert.AreEqual(expectedExitCode, exitCode);
        Assert.AreEqual(expectedFailure, failure);
    }

    private static ContentStore Store(params ContentShare[] shares) => Store(shares, []);

    private static ContentStore Store(ContentShare[] shares, ContentBucket[] buckets) =>
        new(InMemoryContentFileSystem.RootPath, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions(), shares, buckets);

    private static string Write(Action<TextWriter> write)
    {
        using var log = new StringWriter();
        write(log);
        return log.ToString();
    }
}
