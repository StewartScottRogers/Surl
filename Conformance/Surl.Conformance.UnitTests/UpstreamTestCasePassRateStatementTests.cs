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

    [TestMethod]
    public void ExclusionsFile_NoExcludedCase_IsOnAPassingList()
    {
        var excluded = CommittedExclusions();
        var bothExcludedAndPassing = new List<string>();

        foreach (var list in Directory.EnumerateFiles(Folder, UpstreamTestCasePassingList.FileNameFor("*")).Order(StringComparer.Ordinal))
        {
            bothExcludedAndPassing.AddRange(UpstreamTestCasePassingList.Parse(File.ReadAllText(list))
                .Where(excluded.Contains)
                .Select(number => $"{number} in {Path.GetFileName(list)}"));
        }

        Assert.IsEmpty(
            bothExcludedAndPassing,
            "Excluded as test-server-choice yet listed as passing; drop the exclusions row or the list line: " + string.Join(", ", bothExcludedAndPassing));
    }

    [TestMethod]
    public void CurrentTable_EachPlatformsTestServerChoiceCount_IsTheExclusionsFilesRowCount()
    {
        var excluded = CommittedExclusions();

        foreach (var row in ReadPassRateFile().Current)
        {
            Assert.AreEqual(
                excluded.Count,
                row.TestServerChoice,
                $"{row.Platform}: the current table's test-server-choice and {UpstreamTestCaseExclusions.Path}'s row count differ; update both together.");
        }
    }

    /// <summary>
    /// The case numbers the committed <c>Upstream-Test-Case-Exclusions.md</c> lists, which every
    /// run's selection rules take as <see cref="UpstreamTestCaseSelectionRules.TestServerChoices"/>.
    /// </summary>
    /// <returns>The excluded case numbers.</returns>
    internal static IReadOnlySet<int> CommittedExclusions() =>
        UpstreamTestCaseExclusions.Parse(File.ReadAllText(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamTestCaseExclusions.Path)));

    private static UpstreamTestCasePassRateFile ReadPassRateFile() =>
        UpstreamTestCasePassRateFile.Parse(File.ReadAllText(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamTestCasePassRateFile.Path)));
}
