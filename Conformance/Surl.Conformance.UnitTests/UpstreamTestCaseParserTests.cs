using static Surl.Conformance.UpstreamTestCaseText;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseParserTests
{
    private const string Minimal = """
        <?xml version="1.0" encoding="US-ASCII"?>
        <!-- a comment
             over two lines -->
        <testcase id="1" kind='odd'>
        # a shell comment
        <info>
        <keywords>
        HTTP
        </keywords>
        </info>

        <reply>
        <data nocheck="yes" crlf='headers'>
        HTTP/1.1 200 OK
        <data>
        nested
        </data>
        </data>
        <servercmd/>
        </reply>
        <disable>
        test-duphandle
        </disable>
        </testcase>
        """;

    [TestMethod]
    public void Parse_Minimal_ReadsAttributesSectionsAndParts()
    {
        var testCase = Parse(Minimal);

        Assert.AreEqual("1", testCase.Attributes["id"]);
        Assert.AreEqual("odd", testCase.Attributes["kind"]);
        CollectionAssert.AreEqual(new[] { "info", "reply", "disable" }, testCase.Sections.Select(section => section.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "HTTP\n" }, testCase.FindPart("info", "keywords")!.Lines.ToArray());
    }

    [TestMethod]
    public void Parse_PartHoldingItsOwnTag_EndsAtTheMatchingClosingTag()
    {
        var data = Parse(Minimal).FindPart("reply", "data")!;

        // Upstream's prepro recognises only crlf="headers" in double quotes, so this part keeps LF.
        CollectionAssert.AreEqual(new[] { "HTTP/1.1 200 OK\n", "<data>\n", "nested\n", "</data>\n" }, data.Lines.ToArray());
        Assert.AreEqual("yes", data.Attributes["nocheck"]);
        Assert.AreEqual("headers", data.Attributes["crlf"]);
    }

    [TestMethod]
    public void Parse_SelfClosingPart_IsEmpty()
    {
        var servercmd = Parse(Minimal).FindPart("reply", "servercmd")!;

        Assert.IsEmpty(servercmd.Lines);
    }

    [TestMethod]
    public void Parse_TextInASectionOutsideItsParts_IsTheSectionsLines()
    {
        var testCase = Parse(Minimal);

        CollectionAssert.AreEqual(new[] { "test-duphandle\n" }, testCase.FindSection("disable")!.Lines.ToArray());
        Assert.IsEmpty(testCase.FindSection("reply")!.Lines);
    }

    [TestMethod]
    public void Parse_TextAfterATagOnItsLine_IsIgnored()
    {
        var testCase = Parse("<testcase>\n<client>\n<command> # note\n--x\n</command> after\n</client>trailing\n</testcase>\n");

        CollectionAssert.AreEqual(new[] { "--x\n" }, testCase.FindPart("client", "command")!.Lines.ToArray());
    }

    [TestMethod]
    public void Parse_LineStartingWithALongerTagName_StaysInThePart()
    {
        var testCase = Parse("<testcase>\n<reply>\n<data>\n<datacheck>\n</datax>\n<data\n</data>\n</reply>\n</testcase>");

        CollectionAssert.AreEqual(new[] { "<datacheck>\n", "</datax>\n", "<data\n" }, testCase.FindPart("reply", "data")!.Lines.ToArray());
    }

    [TestMethod]
    public void Find_MissingSectionOrPart_IsNull()
    {
        var testCase = Parse(Minimal);

        Assert.IsNull(testCase.FindSection("verify"));
        Assert.IsNull(testCase.FindPart("verify", "stdout"));
        Assert.IsNull(testCase.FindPart("reply", "size"));
    }

    [TestMethod]
    public void Parse_TextBeforeTheTestcase_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(() => Parse("hello\n<testcase>\n</testcase>\n"));

        Assert.AreEqual(1, exception.LineNumber);
        Assert.AreEqual("expected an opening tag, found 'hello'", exception.Problem);
    }

    [TestMethod]
    public void Parse_FirstTagNotTestcase_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(() => Parse("<?xml?>\n<info>\n</info>\n"));

        Assert.AreEqual(2, exception.LineNumber);
        Assert.AreEqual("expected a <testcase> tag, found '<info>'", exception.Problem);
    }

    [TestMethod]
    public void Parse_PartOutsideEverySection_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Parse("<testcase>\n<verify>\n</verify>\n<stdout>\nx\n</stdout>\n</testcase>\n"));

        Assert.AreEqual(4, exception.LineNumber);
        StringAssert.StartsWith(exception.Problem, "<stdout> is outside every section");
    }

    [TestMethod]
    public void Parse_SectionClosedInsideAPart_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Parse("<testcase>\n<verify>\n<stdout>\nx\n</verify>\n</testcase>\n"));

        Assert.AreEqual(5, exception.LineNumber);
        Assert.AreEqual("missing </stdout> tag before </verify>", exception.Problem);
    }

    [TestMethod]
    public void Parse_PartNeverClosed_ReportsTheLastLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Parse("<testcase>\n<verify>\n<stdout>\n<stdout"));

        Assert.AreEqual(4, exception.LineNumber);
        Assert.AreEqual("<stdout> is not closed", exception.Problem);
    }

    [TestMethod]
    public void Parse_CommentNeverClosed_ReportsTheLastLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(() => Parse("<testcase>\n<!-- open\nstill\n"));

        Assert.AreEqual(3, exception.LineNumber);
        Assert.AreEqual("<!--> is not closed", exception.Problem);
    }

    [TestMethod]
    public void Parse_FileEndsInsideTheTestcase_ReportsTheLastLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(() => Parse("<testcase>\n<info>\n</info>\n"));

        Assert.AreEqual(3, exception.LineNumber);
        Assert.AreEqual("the file ends before </testcase>", exception.Problem);
    }

    [TestMethod]
    public void Parse_EmptyFile_ReportsLineOne()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(() => Parse(""));

        Assert.AreEqual(1, exception.LineNumber);
    }

    [TestMethod]
    public void Parse_ClosingTagThatClosesNothing_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Parse("<testcase>\n<info>\n</client>\n</info>\n</testcase>\n"));

        Assert.AreEqual(3, exception.LineNumber);
        Assert.AreEqual("expected </info>, found '</client>'", exception.Problem);
    }

    [TestMethod]
    public void Parse_TextAfterTheTestcase_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Parse("<testcase>\n</testcase>\n\n<!-- fine -->\nnot fine\n"));

        Assert.AreEqual(5, exception.LineNumber);
        Assert.AreEqual("expected nothing after </testcase>, found 'not fine'", exception.Problem);
    }

    [TestMethod]
    public void Parse_LineNumbers_AreTheSourceLinesBeforeConditionalsWereRemoved()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Parse("<testcase>\n%if nothing\n<info>\n%endif\n</info>\n</testcase>\n"));

        Assert.AreEqual(5, exception.LineNumber);
    }

    [TestMethod]
    public void ParseAttributes_RepeatedName_KeepsTheLastValue()
    {
        var attributes = UpstreamTestCaseParser.ParseAttributes(" a=\"1\" b= '2' a=\"3\"");

        Assert.AreEqual("3", attributes["a"]);
        Assert.AreEqual("2", attributes["b"]);
        Assert.HasCount(2, attributes);
    }

    private static UpstreamTestCase Parse(string source) => UpstreamTestCaseParser.Parse(Bytes(source), Environment());
}
