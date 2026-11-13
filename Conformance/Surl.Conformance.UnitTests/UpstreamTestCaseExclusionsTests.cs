namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseExclusionsTests
{
    private const string Header = """
        # Upstream test-case exclusions

        Prose before the table, with a | pipe that is not a table.

        | Case | Byte verified | Why |
        | --- | --- | --- |

        """;

    [TestMethod]
    public void Parse_RowsNamingNumbersOrTestNames_ReadsEachCase()
    {
        var cases = UpstreamTestCaseExclusions.Parse(Header + "| 802 | UIDVALIDITY | RFC 9051 |\n| `test818` | NOOP | RFC 9051 |\n\nAfter the table.\n");

        CollectionAssert.AreEquivalent(new[] { 802, 818 }, cases.ToList());
    }

    [TestMethod]
    public void Parse_CaseColumnNotFirstAndCarriageReturns_ReadsTheCaseColumn()
    {
        var cases = UpstreamTestCaseExclusions.Parse("| Why | Case |\r\n| --- | --- |\r\n| banner | 897 |\r\n");

        CollectionAssert.AreEquivalent(new[] { 897 }, cases.ToList());
    }

    [TestMethod]
    public void Parse_EmptyTable_IsAnEmptySet()
    {
        Assert.IsEmpty(UpstreamTestCaseExclusions.Parse(Header));
    }

    [TestMethod]
    [DataRow("| case 802 | UIDVALIDITY | RFC 9051 |")]
    [DataRow("| test | UIDVALIDITY | RFC 9051 |")]
    [DataRow("| -802 | UIDVALIDITY | RFC 9051 |")]
    public void Parse_CaseCellNamingNoNumber_ThrowsNamingTheCell(string row)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamTestCaseExclusions.Parse(Header + row + "\n"));

        Assert.Contains("is neither test<N> nor <N>", exception.Message);
    }

    [TestMethod]
    public void Parse_RowWithTooFewCells_Throws()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamTestCaseExclusions.Parse(Header + "| 802 | UIDVALIDITY |\n"));

        Assert.Contains("2 cells, not 3", exception.Message);
    }

    [TestMethod]
    public void Parse_CaseListedTwice_Throws()
    {
        var exception = Assert.ThrowsExactly<FormatException>(
            () => UpstreamTestCaseExclusions.Parse(Header + "| 802 | a | b |\n| test802 | c | d |\n"));

        Assert.Contains("case 802 twice", exception.Message);
    }

    [TestMethod]
    public void Parse_NoTableWithACaseColumn_Throws()
    {
        var exception = Assert.ThrowsExactly<FormatException>(
            () => UpstreamTestCaseExclusions.Parse("# Exclusions\n\n| Number | Why |\n| --- | --- |\n| 802 | a |\n"));

        Assert.Contains("no table with a 'Case' column", exception.Message);
    }

    [TestMethod]
    public void Parse_NullText_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseExclusions.Parse(null!));
    }

    [TestMethod]
    public void Path_IsBesideThePassingLists()
    {
        Assert.AreEqual("Documentation/Conformance/Upstream-Test-Case-Exclusions.md", UpstreamTestCaseExclusions.Path);
    }
}
