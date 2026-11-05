namespace Surl.Conformance;

/// <summary>
/// Checks the committed pass-rate file against the committed passing lists (ADR-0080 decision 5):
/// the fast half of "the pass rate only rises", run by every lane and every CI leg.
/// </summary>
[TestClass]
public sealed class UpstreamTestCasePassRateStatementTests
{
    private static readonly string Folder = Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamTestCasePassingList.Folder);

    [TestMethod]
    public void CurrentTable_EachPlatformsPassedCount_EqualsItsPassingListsLineCount()
    {
        var file = ReadPassRateFile();

        foreach (var row in file.Current)
        {
            var listPath = Path.Combine(Folder, UpstreamTestCasePassingList.FileNameFor(row.Platform));
            var listed = File.Exists(listPath) ? UpstreamTestCasePassingList.Parse(File.ReadAllText(listPath)) : [];

            Assert.AreEqual(
                row.Passed ?? 0,
                listed.Count,
                $"{row.Platform}: the current table's Passed and {UpstreamTestCasePassingList.FileNameFor(row.Platform)}'s line count differ; update both together.");
        }
    }

    [TestMethod]
    public void CurrentTable_HasOneRowPerReferencePlatform()
    {
        var platforms = ReadPassRateFile().Current.Select(row => row.Platform).ToList();

        CollectionAssert.AreEquivalent(new[] { "linux-x64", "osx-arm64", "win-x64" }, platforms);
    }

    [TestMethod]
    public void ReleasesTable_EachPlatformsPassedCount_NeverFalls()
    {
        var falls = ReadPassRateFile().FindFallingReleases();

        Assert.IsEmpty(falls, string.Join("; ", falls));
    }

    private static UpstreamTestCasePassRateFile ReadPassRateFile() =>
        UpstreamTestCasePassRateFile.Parse(File.ReadAllText(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamTestCasePassRateFile.Path)));
}
