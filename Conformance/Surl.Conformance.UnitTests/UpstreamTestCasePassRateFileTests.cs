namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePassRateFileTests
{
    private const string Current = """
        # Pass rate

        ## Current

        | Platform | Commit | Passed | Failed |
        | --- | --- | ---: | ---: |
        | `win-x64` | abc | 1,651 | 35 |
        | `linux-x64` | abc | not run | not run |

        """;

    [TestMethod]
    public void Parse_CurrentTable_ReadsEachPlatformsPassedCount()
    {
        var file = UpstreamTestCasePassRateFile.Parse(Current + Releases());

        Assert.AreEqual(new UpstreamTestCasePassRateRow(null, "win-x64", 1651), file.Current[0]);
        Assert.AreEqual(new UpstreamTestCasePassRateRow(null, "linux-x64", null), file.Current[1]);
    }

    [TestMethod]
    public void Parse_EmptyReleasesTable_HasNoReleases()
    {
        var file = UpstreamTestCasePassRateFile.Parse(Current + Releases());

        Assert.IsEmpty(file.Releases);
        Assert.IsEmpty(file.FindFallingReleases());
    }

    [TestMethod]
    public void Parse_ReleasesTable_ReadsVersionPlatformAndPassed()
    {
        var file = UpstreamTestCasePassRateFile.Parse(Current + Releases("| 0.1.0 | win-x64 | 600 |"));

        Assert.AreEqual(new UpstreamTestCasePassRateRow("0.1.0", "win-x64", 600), file.Releases.Single());
    }

    [TestMethod]
    public void FindFallingReleases_CountFallsForAPlatform_NamesBothReleases()
    {
        var file = UpstreamTestCasePassRateFile.Parse(Current + Releases(
            "| 0.1.0 | win-x64 | 600 |", "| 0.1.0 | linux-x64 | 500 |", "| 0.2.0 | win-x64 | 599 |", "| 0.2.0 | linux-x64 | 501 |"));

        var falls = file.FindFallingReleases();

        Assert.AreEqual("win-x64: 0.2.0 passes 599, fewer than 0.1.0's 600", falls.Single());
    }

    [TestMethod]
    public void FindFallingReleases_NotRunRow_IsIgnored()
    {
        var file = UpstreamTestCasePassRateFile.Parse(Current + Releases(
            "| 0.1.0 | win-x64 | 600 |", "| 0.2.0 | win-x64 | not run |", "| 0.3.0 | win-x64 | 600 |"));

        Assert.IsEmpty(file.FindFallingReleases());
    }

    [TestMethod]
    public void Parse_MissingHeading_Throws()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassRateFile.Parse(Current));

        Assert.Contains("## Releases", exception.Message);
    }

    [TestMethod]
    public void Parse_HeadingWithNoTable_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassRateFile.Parse(Current + "## Releases\n\nNone yet.\n"));
    }

    [TestMethod]
    public void Parse_MissingColumn_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<FormatException>(
            () => UpstreamTestCasePassRateFile.Parse(Current + "## Releases\n\n| Platform | Passed |\n| --- | --- |\n"));

        Assert.Contains("'Version'", exception.Message);
    }

    [TestMethod]
    public void Parse_RowWithTooFewCells_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassRateFile.Parse(Current + Releases("| 0.1.0 | win-x64 |")));
    }

    [TestMethod]
    public void Parse_PassedNeitherNumberNorNotRun_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassRateFile.Parse(Current + Releases("| 0.1.0 | win-x64 | many |")));
    }

    [TestMethod]
    public void Parse_NullText_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePassRateFile.Parse(null!));
    }

    [TestMethod]
    public void Parse_TestServerChoiceColumn_ReadsEachRowsCount()
    {
        const string current = """
            ## Current

            | Platform | Passed | test-server-choice |
            | --- | ---: | ---: |
            | `win-x64` | 1,063 | 4 |
            | `linux-x64` | not run | not run |

            """;

        var file = UpstreamTestCasePassRateFile.Parse(current + Releases());

        Assert.AreEqual(new UpstreamTestCasePassRateRow(null, "win-x64", 1063, 4), file.Current[0]);
        Assert.AreEqual(new UpstreamTestCasePassRateRow(null, "linux-x64", null, null), file.Current[1]);
    }

    [TestMethod]
    public void Parse_TestServerChoiceNeitherNumberNorNotRun_ThrowsNamingTheColumn()
    {
        const string current = "## Current\n\n| Platform | Passed | test-server-choice |\n| --- | ---: | ---: |\n| win-x64 | 1 | some |\n\n";

        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassRateFile.Parse(current + Releases()));

        Assert.Contains("test-server-choice cell 'some'", exception.Message);
    }

    private static string Releases(params string[] rows) =>
        "## Releases\n\n| Version | Platform | Passed |\n| --- | --- | ---: |\n" + string.Concat(rows.Select(row => row + "\n")) + "\nAfter the table.\n";
}
