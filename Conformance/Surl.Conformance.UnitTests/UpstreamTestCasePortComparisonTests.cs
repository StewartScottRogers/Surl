namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePortComparisonTests
{
    private static readonly UpstreamTestCasePortRow[] Rows =
    [
        Row(3, "http", UpstreamTestCaseResult.Pass(3)),
        Row(1, "http", UpstreamTestCaseResult.Fail(1, "protocol")),
        Row(2, "ftp", UpstreamTestCaseResult.Pass(2)),
        Row(4, "smb", UpstreamTestCaseResult.Skip(4, "protocol-missing:smb")),
    ];

    [TestMethod]
    public void Compare_EveryPortDefectListed_Passes()
    {
        var comparison = UpstreamTestCasePortComparison.Compare(Rows, new HashSet<int> { 1 });

        Assert.IsTrue(comparison.Passes);
        Assert.AreEqual(string.Empty, comparison.DescribeFailures());
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, comparison.Rows.Select(row => row.Number).ToArray());
        Assert.AreEqual(1, comparison.PortDefects.Single().Number);
        Assert.IsEmpty(comparison.SurlDefects);
    }

    [TestMethod]
    public void Compare_PortDefectNotListed_FailsNamingIt()
    {
        var comparison = UpstreamTestCasePortComparison.Compare(Rows, new HashSet<int>());

        Assert.IsFalse(comparison.Passes);
        Assert.AreEqual(1, comparison.UnlistedPortDefects.Single().Number);
        StringAssert.Contains(comparison.DescribeFailures(), "does not list; add it (ADR-0089 decision 6): test1 (http)");
    }

    [TestMethod]
    public void Compare_ListedFindingNoLongerAppearing_FailsNamingIt()
    {
        var comparison = UpstreamTestCasePortComparison.Compare(Rows, new HashSet<int> { 1, 3 });

        Assert.IsFalse(comparison.Passes);
        CollectionAssert.AreEqual(new[] { 3 }, comparison.FindingsNoLongerAppearing.ToArray());
        StringAssert.Contains(comparison.DescribeFailures(), "lists test3, but it is no longer a port defect");
    }

    [TestMethod]
    public void Compare_UpstreamFailedACase_FailsAsASurlDefect()
    {
        UpstreamTestCasePortRow[] rows = [new(9, "mail", UpstreamTestCaseResult.Fail(9, "timeout"), UpstreamTestCaseResult.Pass(9))];

        var comparison = UpstreamTestCasePortComparison.Compare(rows, new HashSet<int>());

        Assert.IsFalse(comparison.Passes);
        Assert.AreEqual(9, comparison.SurlDefects.Single().Number);
        StringAssert.Contains(comparison.DescribeFailures(), "Surl defect");
    }

    [TestMethod]
    public void FormatFamilyTable_Rows_CountsEachFamilyInOrdinalOrderAndTheTotal()
    {
        var comparison = UpstreamTestCasePortComparison.Compare(Rows, new HashSet<int> { 1 });

        var table = comparison.FormatFamilyTable();

        Assert.AreEqual(
            "| Family | Cases | Upstream passed | Port passed | Port defects | Not comparable |\n" +
            "| --- | ---: | ---: | ---: | ---: | ---: |\n" +
            "| ftp | 1 | 1 | 1 | 0 | 0 |\n" +
            "| http | 2 | 2 | 1 | 1 | 0 |\n" +
            "| smb | 1 | 1 | 0 | 0 | 1 |\n" +
            "| **All** | 4 | 4 | 2 | 1 | 1 |\n",
            table);
    }

    [TestMethod]
    public void ListedFindings_FindingsTable_ReadsTheCaseNumbersOfItsFirstColumn()
    {
        string[] lines =
        [
            "| Case | Family |",
            "| --- | --- |",
            "| `test798` | http |",
            "  | `test1402` | http |",
            "| `testx` | http |",
            "| `KexInit_Method` | ssh |",
            "Some prose naming `test5`.",
        ];

        var listed = UpstreamTestCasePortComparison.ListedFindings(lines);

        Assert.IsTrue(listed.SetEquals([798, 1402]));
    }

    [TestMethod]
    public void NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePortComparison.Compare(null!, new HashSet<int>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePortComparison.Compare(Rows, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePortComparison.ListedFindings(null!));
    }

    // Upstream passed; the port ended as given.
    private static UpstreamTestCasePortRow Row(int number, string family, UpstreamTestCaseResult port) =>
        new(number, family, UpstreamTestCaseResult.Pass(number), port);
}
