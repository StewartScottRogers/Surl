namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePortRowTests
{
    [TestMethod]
    public void Verdict_UpstreamFailed_IsSurlDefectWhateverThePortDid()
    {
        var row = new UpstreamTestCasePortRow(1, "http", UpstreamTestCaseResult.Fail(1, "protocol"), UpstreamTestCaseResult.Pass(1));

        Assert.AreEqual(CurlPortVerdictKind.SurlDefect, row.Verdict);
    }

    [TestMethod]
    public void Verdict_UpstreamSkipped_IsSurlDefect()
    {
        var row = new UpstreamTestCasePortRow(1, "http", UpstreamTestCaseResult.Skip(1, "x"), UpstreamTestCaseResult.Pass(1));

        Assert.AreEqual(CurlPortVerdictKind.SurlDefect, row.Verdict);
    }

    [TestMethod]
    public void Verdict_BothPassed_IsAgreement()
    {
        var row = new UpstreamTestCasePortRow(1, "ftp", UpstreamTestCaseResult.Pass(1), UpstreamTestCaseResult.Pass(1));

        Assert.AreEqual(CurlPortVerdictKind.Agreement, row.Verdict);
        Assert.AreEqual("test1 (ftp): upstream pass, port pass: Agreement", row.ToString());
    }

    [TestMethod]
    public void Verdict_PortSkipped_IsNotComparable()
    {
        var row = new UpstreamTestCasePortRow(1451, "smb", UpstreamTestCaseResult.Pass(1451), UpstreamTestCaseResult.Skip(1451, "protocol-missing:smb"));

        Assert.AreEqual(CurlPortVerdictKind.NotComparable, row.Verdict);
        Assert.AreEqual("test1451 (smb): upstream pass, port skip protocol-missing:smb: NotComparable", row.ToString());
    }

    [TestMethod]
    public void Verdict_PortFailed_IsPortDefect()
    {
        var row = new UpstreamTestCasePortRow(838, "mail", UpstreamTestCaseResult.Pass(838), UpstreamTestCaseResult.Fail(838, "timeout"));

        Assert.AreEqual(CurlPortVerdictKind.PortDefect, row.Verdict);
        Assert.AreEqual("test838", row.Name);
    }

    [TestMethod]
    public void WithoutUpstreamSkips_LibtestNotBuiltRow_IsDroppedOnlyWhenThatReasonIsTolerated()
    {
        var row = new UpstreamTestCasePortRow(2402, "http", UpstreamTestCaseResult.Skip(2402, "libtest-not-built:lib2402"), UpstreamTestCaseResult.Pass(2402));

        var tolerating = UpstreamTestCasePortRow.WithoutUpstreamSkips([row], new HashSet<string> { "libtest-not-built" });
        var notTolerating = UpstreamTestCasePortRow.WithoutUpstreamSkips([row], new HashSet<string> { "debug-build", "libtest" });

        Assert.IsEmpty(tolerating);
        Assert.AreSame(row, notTolerating.Single());
    }

    [TestMethod]
    public void WithoutUpstreamSkips_UpstreamDidNotSkipForAToleratedReason_KeepsTheRowsInOrder()
    {
        var tolerated = new HashSet<string> { "debug-build" };
        UpstreamTestCasePortRow[] rows =
        [
            new(3, "http", UpstreamTestCaseResult.Fail(3, "debug-build"), UpstreamTestCaseResult.Pass(3)),
            new(1, "http", UpstreamTestCaseResult.Skip(1, "debug-build"), UpstreamTestCaseResult.Pass(1)),
            new(2, "http", UpstreamTestCaseResult.Pass(2), UpstreamTestCaseResult.Skip(2, "debug-build")),
            new(4, "http", new UpstreamTestCaseResult(4, UpstreamTestCaseOutcome.Skip, null), UpstreamTestCaseResult.Pass(4)),
        ];

        var kept = UpstreamTestCasePortRow.WithoutUpstreamSkips(rows, tolerated);

        CollectionAssert.AreEqual(new[] { 3, 2, 4 }, kept.Select(row => row.Number).ToArray());
    }

    [TestMethod]
    public void WithoutUpstreamSkips_NullArguments_ThrowArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePortRow.WithoutUpstreamSkips(null!, new HashSet<string>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePortRow.WithoutUpstreamSkips([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseResult.Pass(1).IsSkippedForAny(null!));
    }
}
