namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortFindingsFileTests
{
    private static readonly string[] Lines =
    [
        "## Findings",
        "| `Everywhere` | x | y |",
        "### Findings (linux-x64)",
        "| `OnLinux` | x | y |",
        "### Findings (win-x64)",
        "| `OnWindows` | x | y |",
        "## Notes (see the log)",
        "| `AfterNotes` | x | y |",
    ];

    [TestMethod]
    public void LinesForPlatform_Linux_KeepsTheUnnamedTablesAndLinuxsOwn()
    {
        var kept = CurlPortFindingsFile.LinesForPlatform(Lines, "linux-x64").Where(line => line.StartsWith("| `", StringComparison.Ordinal)).ToList();

        CollectionAssert.AreEqual(
            new[] { "| `Everywhere` | x | y |", "| `OnLinux` | x | y |", "| `AfterNotes` | x | y |" }, kept);
    }

    [TestMethod]
    public void LinesForPlatform_Windows_KeepsTheUnnamedTablesAndWindowssOwn()
    {
        var kept = CurlPortFindingsFile.LinesForPlatform(Lines, "win-x64").Where(line => line.StartsWith("| `", StringComparison.Ordinal)).ToList();

        CollectionAssert.AreEqual(
            new[] { "| `Everywhere` | x | y |", "| `OnWindows` | x | y |", "| `AfterNotes` | x | y |" }, kept);
    }

    [TestMethod]
    public void ThisPlatform_Always_NamesOneOfThePinnedPlatforms()
    {
        CollectionAssert.Contains(new[] { "win-x64", "osx-arm64", "linux-x64" }, CurlPortFindingsFile.ThisPlatform());
    }
}
