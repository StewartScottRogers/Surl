namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredContentConflicts"/>: the duplicates and conflicts among <c>--share</c>,
/// <c>--share-access</c> and <c>--bucket</c> (ADR-0101 decision 5 rows 14 and 15).
/// </summary>
[TestClass]
public sealed class ConfiguredContentConflictsTests
{
    [TestMethod]
    public void Find_NothingGiven_FindsNone() =>
        Assert.IsNull(ConfiguredContentConflicts.Find([], [], []));

    [TestMethod]
    public void Find_DistinctSharesAccessAndBuckets_FindsNone() =>
        Assert.IsNull(ConfiguredContentConflicts.Find(
            [Share("--share", "team"), Share("--share", "media")],
            [Access("--share-access", "team"), Access("--share-access", "media")],
            [Bucket("--bucket", "photos")]));

    [TestMethod]
    public void Find_NamesDifferingOnlyInCase_AreDifferentNames() =>
        Assert.IsNull(ConfiguredContentConflicts.Find(
            [Share("--share", "team"), Share("--share", "TEAM")], [], [Bucket("--bucket", "Team")]));

    [TestMethod]
    public void Find_ShareTwice_NamesTheLaterOption() =>
        Assert.AreEqual(
            new ConfiguredContentConflicts.Refusal("share", "share team is given twice"),
            ConfiguredContentConflicts.Find([Share("--share", "team"), Share("share", "team")], [], []));

    [TestMethod]
    public void Find_BucketTwice_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredContentConflicts.Refusal("--bucket", "bucket photos is given twice"),
            ConfiguredContentConflicts.Find([], [], [Bucket("--bucket", "photos"), Bucket("--bucket", "photos")]));

    [TestMethod]
    public void Find_ShareAccessShareTwice_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredContentConflicts.Refusal("--share-access", "share team is given twice"),
            ConfiguredContentConflicts.Find(
                [Share("--share", "team")], [Access("--share-access", "team"), Access("--share-access", "team")], []));

    [TestMethod]
    public void Find_BucketNamedAsAShare_IsRefusedAtTheBucket() =>
        Assert.AreEqual(
            new ConfiguredContentConflicts.Refusal("--bucket", "bucket team is also a --share"),
            ConfiguredContentConflicts.Find([Share("--share", "team")], [], [Bucket("--bucket", "team")]));

    [TestMethod]
    public void Find_ShareAccessNamingNoShare_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredContentConflicts.Refusal("--share-access", "share media names no --share"),
            ConfiguredContentConflicts.Find(
                [Share("--share", "team")], [Access("--share-access", "team"), Access("--share-access", "media")], []));

    [TestMethod]
    public void Find_SeveralRefusals_FindsADuplicateBeforeAConflict() =>
        Assert.AreEqual(
            new ConfiguredContentConflicts.Refusal("--bucket", "bucket b is given twice"),
            ConfiguredContentConflicts.Find(
                [Share("--share", "team")], [Access("--share-access", "none")], [Bucket("--bucket", "b"), Bucket("--bucket", "b")]));

    [TestMethod]
    public void Find_NullList_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredContentConflicts.Find(null!, [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredContentConflicts.Find([], null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredContentConflicts.Find([], [], null!));
    }

    private static (string, ConfiguredShare) Share(string optionName, string name) =>
        (optionName, new ConfiguredShare(name, "p", IsReadOnly: false, Protocols: null));

    private static (string, ConfiguredShareAccess) Access(string optionName, string share) =>
        (optionName, new ConfiguredShareAccess(share, [new ConfiguredGrant(new("alice", IsGroup: false), "r")]));

    private static (string, ConfiguredBucket) Bucket(string optionName, string name) =>
        (optionName, new ConfiguredBucket(name, "/p"));
}
