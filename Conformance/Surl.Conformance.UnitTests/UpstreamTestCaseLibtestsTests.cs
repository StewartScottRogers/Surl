namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseLibtestsTests
{
    [TestMethod]
    public void AdmittedNumbers_AreStageOneOfAdr0174()
    {
        CollectionAssert.AreEquivalent(new[] { 2402, 2404, 2407, 2502 }, UpstreamTestCaseLibtests.AdmittedNumbers.ToArray());
    }

    [TestMethod]
    [DataRow("lib%TESTNUMBER", 2402, "lib2402")]
    [DataRow("lib2405", 2407, "lib2405")]
    [DataRow("", 2407, "")]
    public void ProgramName_ReadsTheToolWithItsTestNumber(string tool, int number, string expected)
    {
        var testCase = UpstreamTestCaseParser.Parse(
            UpstreamTestCaseText.Bytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n<tool>\n{tool}\n</tool>\n<command>\nx\n</command>\n</client>\n</testcase>\n"),
            UpstreamTestCaseText.Environment());

        Assert.AreEqual(expected, UpstreamTestCaseLibtests.ProgramName(testCase, number));
    }

    [TestMethod]
    public void ProgramName_NullCase_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseLibtests.ProgramName(null!, 1));
    }

    [TestMethod]
    public void LibTestNotBuilt_NamesTheProgram()
    {
        Assert.AreEqual("libtest-not-built:lib2402", UpstreamTestCaseSkipReasons.LibTestNotBuilt("lib2402"));
    }
}
