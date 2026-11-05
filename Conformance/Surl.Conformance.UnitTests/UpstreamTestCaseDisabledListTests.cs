namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseDisabledListTests
{
    [TestMethod]
    public void Parse_NumbersCommentsAndBlankLines_ReadsTheNumbers()
    {
        var source = "# comment 99\n  # indented comment 98\n\n594\n1209 # because\n";

        var disabled = UpstreamTestCaseDisabledList.Parse(UpstreamTestCaseText.Bytes(source), UpstreamTestCaseText.Environment());

        CollectionAssert.AreEquivalent(new[] { 594, 1209 }, disabled.ToArray());
    }

    [TestMethod]
    public void Parse_ConditionalLines_KeepOnlyThoseTheFeaturesShow()
    {
        var source = "%if win32\n100\n%else\n200\n%endif\n";
        var environment = UpstreamTestCaseText.Environment(features: new HashSet<string>(["win32"], StringComparer.Ordinal));

        var disabled = UpstreamTestCaseDisabledList.Parse(UpstreamTestCaseText.Bytes(source), environment);

        CollectionAssert.AreEquivalent(new[] { 100 }, disabled.ToArray());
    }

    [TestMethod]
    public void Parse_LineWithoutANumber_ThrowsNamingTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => UpstreamTestCaseDisabledList.Parse(UpstreamTestCaseText.Bytes("1\nrubbish\n"), UpstreamTestCaseText.Environment()));

        Assert.AreEqual(2, exception.LineNumber);
    }
}
