using static Surl.Conformance.UpstreamTestCaseText;

namespace Surl.Conformance;

/// <summary>
/// Checks inline cases' verify sections against hand-written runs, each section passing and
/// failing, as upstream's <c>singletest_check</c> (<c>tests/runtests.pl</c> at tag
/// <c>curl-8_21_0</c>) would judge them.
/// </summary>
[TestClass]
public sealed class UpstreamTestCaseVerifyCheckerTests
{
    private static readonly HashSet<string> NoFeatures = new(StringComparer.Ordinal);

    [TestMethod]
    public void Check_NoVerifySectionAndExitZero_Passes()
    {
        var testCase = UpstreamTestCaseParser.Parse(Bytes("<testcase>\n</testcase>\n"), Environment());

        Assert.IsTrue(Checker().Check(testCase, Run()).Passed);
    }

    [TestMethod]
    public void Check_NoErrorCodeAndExitSeven_FailsErrorCode()
    {
        var result = Checker().Check(Case(""), Run(exitCode: 7));

        Assert.AreEqual("errorcode", result.FailedSection);
        Assert.AreEqual("curl exited 7, expected 0", result.Difference);
    }

    [TestMethod]
    [DataRow("7, 56", 56)]
    [DataRow("7,56", 7)]
    [DataRow(",7", 0)]
    [DataRow("99999999999", 0)]
    [DataRow("6abc", 6)]
    public void Check_ErrorCodeListingTheExitCode_Passes(string codes, int exitCode) =>
        Assert.IsTrue(Checker().Check(Case($"<errorcode>\n{codes}\n</errorcode>\n"), Run(exitCode: exitCode)).Passed);

    [TestMethod]
    public void Check_ErrorCodeNotListingTheExitCode_Fails()
    {
        var result = Checker().Check(Case("<errorcode>\n7, 56\n</errorcode>\n"), Run(exitCode: 6));

        Assert.AreEqual("errorcode", result.FailedSection);
        Assert.AreEqual("curl exited 6, expected 7, 56", result.Difference);
    }

    [TestMethod]
    public void Check_RunStoppedAtItsTimeout_FailsErrorCode()
    {
        var result = Checker().Check(Case(""), Run(exitCode: null));

        Assert.AreEqual("errorcode", result.FailedSection);
        Assert.AreEqual("curl was stopped at its timeout, expected exit code 0", result.Difference);
    }

    [TestMethod]
    public void Check_BlankErrorCodeLine_AcceptsNoExitCode() =>
        Assert.AreEqual("errorcode", Checker().Check(Case("<errorcode>\n\n</errorcode>\n"), Run()).FailedSection);

    private const string StrippedProtocol = """
        <strip>
        ^User-Agent:

        </strip>
        <strippart>
        s/^(EPRT \|1\|)(\S*)/$1/
        </strippart>
        <protocol>
        USER anonymous
        User-Agent: listed in the case
        EPRT |1|
        QUIT
        </protocol>

        """;

    [TestMethod]
    public void Check_ProtocolWithStripAndStrippart_PassesWhatTheyRemove()
    {
        var run = Run(server: "USER anonymous\nUser-Agent: curl/8.21.0\nEPRT |1|127.0.0.1|40000|\nQUIT\n");

        Assert.IsTrue(Checker().Check(Case(StrippedProtocol), run).Passed);
    }

    [TestMethod]
    public void Check_ProtocolDiffering_NamesTheFirstDifferingLineWithEscapedBytes()
    {
        var run = Run(server: "USER anonymous\nEPRT |1|x\nQUIT\\\u0001\r\n");

        var result = Checker().Check(Case(StrippedProtocol), run);

        Assert.AreEqual("protocol", result.FailedSection);
        Assert.AreEqual(@"line 3 differs: expected ""QUIT\n"", got ""QUIT\x5C\x01\r\n""", result.Difference);
    }

    [TestMethod]
    public void Check_ProtocolShorterThanExpected_ShowsNoLine()
    {
        var result = Checker().Check(Case("<protocol>\nA\nB\n</protocol>\n"), Run(server: "A\n"));

        Assert.AreEqual(@"line 2 differs: expected ""B\n"", got no line", result.Difference);
    }

    [TestMethod]
    public void Check_ProtocolAndNothingRecorded_SaysTheServerHalfRecordedNothing()
    {
        var result = Checker().Check(Case("<protocol>\nA\n</protocol>\n"), Run());

        Assert.AreEqual("protocol", result.FailedSection);
        Assert.AreEqual("the server half recorded nothing", result.Difference);
    }

    [TestMethod]
    [DataRow("abc", true)]
    [DataRow("abc\n", false)]
    public void Check_ProtocolNonewline_CutsTheExpectedFinalNewline(string server, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("<protocol nonewline=\"yes\">\nabc\n</protocol>\n"), Run(server: server)).Passed);

    [TestMethod]
    [DataRow("A\r\nB\r\n", true)]
    [DataRow("A\nB\n", false)]
    public void Check_ProtocolCrlfYes_MakesEveryExpectedNewlineCrlf(string server, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("<protocol crlf=\"yes\">\nA\nB\n</protocol>\n"), Run(server: server)).Passed);

    [TestMethod]
    [DataRow("GET / HTTP/1.1\r\nHost: x\r\n\r\nbody\n", true)]
    [DataRow("GET / HTTP/1.1\r\nHost: x\r\n\r\nbody\r\n", false)]
    [DataRow("GET / HTTP/1.1\nHost: x\n\nbody\n", false)]
    public void Check_ProtocolCrlfHeaders_MakesOnlyHeaderNewlinesCrlf(string server, bool passes) =>
        Assert.AreEqual(
            passes,
            Checker().Check(Case("<protocol crlf=\"headers\">\nGET / HTTP/1.1\nHost: x\n\nbody\n</protocol>\n"), Run(server: server)).Passed);

    [TestMethod]
    public void Check_Proxy_ComparesWhatTheProxyReceived()
    {
        var testCase = Case("<proxy>\nCONNECT x:1 HTTP/1.1\n</proxy>\n");

        Assert.IsTrue(Checker().Check(testCase, Run(proxy: "CONNECT x:1 HTTP/1.1\n")).Passed);
        Assert.AreEqual("proxy", Checker().Check(testCase, Run(proxy: "CONNECT y:1 HTTP/1.1\n")).FailedSection);
    }

    [TestMethod]
    [DataRow("b\n", true)]
    [DataRow("a\n", true)]
    [DataRow("c\n", false)]
    public void Check_ExpectedAlternatives_MatchEitherChoice(string server, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("<protocol>\n%alternatives[a,b]\n</protocol>\n"), Run(server: server)).Passed);

    [TestMethod]
    public void Check_ExpectedAlternativesDiffering_ShowsTheFirstChoice() =>
        Assert.AreEqual(
            @"line 1 differs: expected ""a\n"", got ""c\n""",
            Checker().Check(Case("<protocol>\n%alternatives[a,b]\n</protocol>\n"), Run(server: "c\n")).Difference);

    [TestMethod]
    [DataRow("a\r\nb\n", true)]
    [DataRow("a\nb\n", true)]
    [DataRow("a\nc\n", false)]
    public void Check_StdoutModeText_ComparesWithCrlfOnBothSides(string stdout, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("<stdout mode=\"text\">\na\nb\n</stdout>\n"), Run(stdout: stdout)).Passed);

    [TestMethod]
    public void Check_StdoutWithoutModeText_ComparesBytes()
    {
        var result = Checker().Check(Case("<stdout>\na\nb\n</stdout>\n"), Run(stdout: "a\r\nb\n"));

        Assert.AreEqual("stdout", result.FailedSection);
        Assert.AreEqual(@"line 1 differs: expected ""a\n"", got ""a\r\n""", result.Difference);
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow("x", false)]
    public void Check_StdoutEmptyMarker_ExpectsNothing(string stdout, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("<stdout>\n%EMPTY\n</stdout>\n"), Run(stdout: stdout)).Passed);

    [TestMethod]
    public void Check_EmptyStdoutPart_IsNotChecked() =>
        Assert.IsTrue(Checker().Check(Case("<stdout>\n</stdout>\n"), Run(stdout: "anything")).Passed);

    [TestMethod]
    [DataRow("abc", true)]
    [DataRow("abc\r\n", false)]
    public void Check_StdoutNonewlineAndCrlf_CutsThenRewrites(string stdout, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("<stdout nonewline=\"yes\" crlf=\"yes\">\nabc\n</stdout>\n"), Run(stdout: stdout)).Passed);

    [TestMethod]
    public void Check_StdoutStripfile_DropsTheLinesItEmpties()
    {
        var testCase = Case("<stripfile>\ns/^Date:.*\\n//\n$_ = '' if /^0$/\n</stripfile>\n<stdout>\nbody\n</stdout>\n");

        Assert.IsTrue(Checker().Check(testCase, Run(stdout: "Date: today\nbody\n0")).Passed);
    }

    [TestMethod]
    public void Check_StdoutLoadfile_ComparesWithTheFileWithLfNewlines()
    {
        var files = new UnitTestInMemoryRunFiles().With("data/expected.txt", "one\r\ntwo\n");
        var testCase = Case("<stdout loadfile=\"data/expected.txt\">\nignored\n</stdout>\n");

        Assert.IsTrue(Checker(files).Check(testCase, Run(stdout: "one\ntwo\n")).Passed);
    }

    [TestMethod]
    public void Check_StdoutLoadfileMissing_Fails()
    {
        var result = Checker().Check(Case("<stdout loadfile=\"data/missing.txt\">\n</stdout>\n"), Run());

        Assert.AreEqual("stdout", result.FailedSection);
        Assert.AreEqual("the loadfile data/missing.txt cannot be read", result.Difference);
    }

    [TestMethod]
    public void Check_Stderr_ComparesWhatCurlWroteToStderr()
    {
        var testCase = Case("<stderr mode=\"text\">\ncurl: (6) Could not resolve host\n</stderr>\n");

        Assert.IsTrue(Checker().Check(testCase, Run(stderr: "curl: (6) Could not resolve host\r\n")).Passed);
        Assert.AreEqual("stderr", Checker().Check(testCase, Run(stderr: "curl: (7) Failed\n")).FailedSection);
    }

    [TestMethod]
    public void Check_EmptyStderrPart_IsNotChecked() =>
        Assert.IsTrue(Checker().Check(Case("<stderr>\n</stderr>\n"), Run(stderr: "anything")).Passed);

    [TestMethod]
    public void Check_StderrModeWarn_AlwaysPassesAsRuntestsJoinLeavesBothSidesEmpty() =>
        Assert.IsTrue(Checker().Check(Case("<stderr mode=\"warn\">\nWarning: one\n</stderr>\n"), Run(stderr: "Warning: two\n")).Passed);

    [TestMethod]
    public void Check_StderrWithCaCertFeature_StripsTheEmbeddedBundleNote()
    {
        var testCase = Case("<stderr>\nerror\n</stderr>\n");
        var run = Run(stderr: "Note: Using embedded CA bundle (230 KiB)\nerror\n");

        Assert.IsTrue(new UpstreamTestCaseVerifyChecker(new UnitTestInMemoryRunFiles(), new HashSet<string> { "CAcert" }).Check(testCase, run).Passed);
        Assert.IsFalse(Checker().Check(testCase, run).Passed);
    }

    [TestMethod]
    public void Check_FileName_ComparesTheFileCurlWrote()
    {
        var testCase = Case("<file name=\"log/out.txt\">\nhello\n</file>\n");

        Assert.IsTrue(Checker(new UnitTestInMemoryRunFiles().With("log/out.txt", "hello\n")).Check(testCase, Run()).Passed);

        var result = Checker(new UnitTestInMemoryRunFiles().With("log/out.txt", "bye\n")).Check(testCase, Run());

        Assert.AreEqual("file (log/out.txt)", result.FailedSection);
        Assert.AreEqual(@"line 1 differs: expected ""hello\n"", got ""bye\n""", result.Difference);
    }

    [TestMethod]
    public void Check_FileMissing_ComparesAsEmpty()
    {
        Assert.IsTrue(Checker().Check(Case("<file name=\"log/none\">\n</file>\n"), Run()).Passed);
        Assert.AreEqual("file (log/none)", Checker().Check(Case("<file name=\"log/none\">\nx\n</file>\n"), Run()).FailedSection);
    }

    [TestMethod]
    public void Check_FileWithoutName_Fails()
    {
        var result = Checker().Check(Case("<file2>\nx\n</file2>\n"), Run());

        Assert.AreEqual("file2", result.FailedSection);
        Assert.AreEqual("the part has no name attribute", result.Difference);
    }

    [TestMethod]
    public void Check_File1ModeTextCrlfAndStripfile1_AppliesEachToItsFile()
    {
        var files = new UnitTestInMemoryRunFiles().With("log/one", "HTTP/1.1 200 OK\nsecret\nbody\n");
        var testCase = Case("""
            <stripfile1>
            s/^secret.*\n//
            </stripfile1>
            <file1 name="log/one" mode="text" crlf="headers" nonewline="yes">
            HTTP/1.1 200 OK
            body
            </file1>

            """);

        var result = Checker(files).Check(testCase, Run());

        Assert.AreEqual("file1 (log/one)", result.FailedSection);
        Assert.AreEqual(@"line 2 differs: expected ""body\r"", got ""body\r\n""", result.Difference);
    }

    [TestMethod]
    public void Check_FileWithCaCertFeature_StripsTheEmbeddedBundleNote()
    {
        var files = new UnitTestInMemoryRunFiles().With("log/out", "Note: Using embedded CA bundle\nok\n");
        var checker = new UpstreamTestCaseVerifyChecker(files, new HashSet<string> { "CAcert" });

        Assert.IsTrue(checker.Check(Case("<file name=\"log/out\">\nok\n</file>\n"), Run()).Passed);
    }

    [TestMethod]
    [DataRow("line one\r\nline two", true)]
    [DataRow("line one\nline two", false)]
    public void Check_UploadCrlfAndNonewline_CutsAndForcesCrlf(string upload, bool passes) =>
        Assert.AreEqual(
            passes,
            Checker().Check(Case("<upload crlf=\"headers\" nonewline=\"yes\">\nline one\nline two\n</upload>\n"), Run(upload: upload)).Passed);

    [TestMethod]
    public void Check_UploadWithStrippart_StripsTheUploadToo()
    {
        var testCase = Case("<strippart>\ns/boundary=\\w+/boundary=x/\n</strippart>\n<upload>\nboundary=x\n</upload>\n");

        Assert.IsTrue(Checker().Check(testCase, Run(upload: "boundary=abc123\n")).Passed);
        Assert.AreEqual("upload", Checker().Check(testCase, Run(upload: "other\n")).FailedSection);
    }

    [TestMethod]
    public void Check_NotExists_FailsWhenAListedEntryExists()
    {
        var testCase = Case("<notexists>\nlog/gone\nlog/kept\n</notexists>\n");

        Assert.IsTrue(Checker().Check(testCase, Run()).Passed);

        var result = Checker(new UnitTestInMemoryRunFiles().With("log/gone", "")).Check(testCase, Run());

        Assert.AreEqual("notexists", result.FailedSection);
        Assert.AreEqual("\"log/gone\" exists", result.Difference);
    }

    [TestMethod]
    [DataRow("hello\n", true)]
    [DataRow("bye\n", false)]
    public void Check_ReplyData_ComparesTheOutputFile(string output, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("", "<data>\nhello\n</data>\n"), Run(output: output)).Passed);

    [TestMethod]
    public void Check_ReplyDataDiffering_NamesData() =>
        Assert.AreEqual("data", Checker().Check(Case("", "<data>\nhello\n</data>\n"), Run(output: "bye\n")).FailedSection);

    [TestMethod]
    public void Check_ReplyDataNocheck_IsNotChecked() =>
        Assert.IsTrue(Checker().Check(Case("", "<data nocheck=\"yes\">\nhello\n</data>\n"), Run(output: "bye\n")).Passed);

    [TestMethod]
    [DataRow("", true)]
    [DataRow("x", false)]
    public void Check_ReplyDataEmptyWithSendzero_ExpectsAnEmptyOutputFile(string output, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("", "<data sendzero=\"yes\">\n</data>\n"), Run(output: output)).Passed);

    [TestMethod]
    public void Check_ReplyDataEmptyWithoutSendzero_IsNotChecked() =>
        Assert.IsTrue(Checker().Check(Case("", "<data>\n</data>\n"), Run(output: "x")).Passed);

    [TestMethod]
    [DataRow("a\r\nb", true)]
    [DataRow("a\nb", true)]
    [DataRow("a\nb\n", false)]
    public void Check_ReplyDataModeTextAndNonewline_ApplyToBothSides(string output, bool passes) =>
        Assert.AreEqual(passes, Checker().Check(Case("", "<data mode=\"text\" nonewline=\"yes\">\na\nb\n</data>\n"), Run(output: output)).Passed);

    [TestMethod]
    [DataRow("one\r\ntwotwo3\r", true)]
    [DataRow("one\ntwo\n", false)]
    public void Check_ReplyDatacheck_ReplacesDataWithEachDatacheckPart(string output, bool passes)
    {
        // Preprocessing already made datacheck4's newline CRLF, as upstream's prepro does for every
        // <data*> part, so nonewline then cuts only its LF.

        var reply = """
            <data>
            ignored
            </data>
            <datacheck mode="text">
            one
            </datacheck>
            <datacheck1 nonewline="yes">
            two
            </datacheck1>
            <datacheck2 nonewline="yes">
            two
            </datacheck2>
            <datacheck3 nonewline="yes">
            </datacheck3>
            <datacheck4 nonewline="yes" crlf="yes">
            3
            </datacheck4>

            """;

        Assert.AreEqual(passes, Checker().Check(Case("", reply), Run(output: output)).Passed);
    }

    [TestMethod]
    public void Check_SeveralSectionsDiffering_ReportsTheFirstInRuntestsOrder()
    {
        var testCase = Case("<errorcode>\n0\n</errorcode>\n<protocol>\nA\n</protocol>\n<stdout>\nout\n</stdout>\n");

        var result = Checker().Check(testCase, Run(exitCode: 7, stdout: "other\n", server: "B\n"));

        Assert.AreEqual("stdout", result.FailedSection);
    }

    [TestMethod]
    public void Check_StripLineOutsideTheSubset_Throws()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => Checker().Check(Case("<strippart>\nchomp;\n</strippart>\n<protocol>\nA\n</protocol>\n"), Run(server: "A\n")));
        Assert.ThrowsExactly<NotSupportedException>(() => Checker().Check(Case("<strip>\n(unclosed\n</strip>\n<protocol>\nA\n</protocol>\n"), Run(server: "A\n")));
    }

    [TestMethod]
    public void Escape_EveryKindOfByte_IsAsNfr020EscapesIt() =>
        Assert.AreEqual(@"a ~\x5C\r\n\x00\x09\x7F\xFF", UpstreamTestCaseVerifyChecker.Escape("a ~\\\r\n\0\t\u007F\u00FF"));

    [TestMethod]
    public void NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpstreamTestCaseVerifyChecker(null!, NoFeatures));
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpstreamTestCaseVerifyChecker(new UnitTestInMemoryRunFiles(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Checker().Check(null!, Run()));
        Assert.ThrowsExactly<ArgumentNullException>(() => Checker().Check(Case(""), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseVerifyChecker.Escape(null!));
    }

    private static UpstreamTestCaseVerifyChecker Checker(UnitTestInMemoryRunFiles? files = null) =>
        new(files ?? new UnitTestInMemoryRunFiles(), NoFeatures);

    private static UpstreamTestCase Case(string verify, string reply = "") =>
        UpstreamTestCaseParser.Parse(
            Bytes($"<testcase>\n<reply>\n{reply}</reply>\n<verify>\n{verify}</verify>\n</testcase>\n"),
            Environment());

    private static UpstreamTestCaseRunRecord Run(
        int? exitCode = 0,
        string stdout = "",
        string stderr = "",
        string output = "",
        string server = "",
        string upload = "",
        string proxy = "") =>
        new(exitCode, Bytes(stdout), Bytes(stderr), Bytes(output), Bytes(server), Bytes(upload), Bytes(proxy));
}
