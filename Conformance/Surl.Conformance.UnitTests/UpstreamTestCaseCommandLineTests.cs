namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseCommandLineTests
{
    private const string LogDirectory = "/tmp/log";

    [TestMethod]
    public void Build_NullCase_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseCommandLine.Build(1, null!, LogDirectory));
    }

    [TestMethod]
    public void Build_NullLogDirectory_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseCommandLine.Build(1, Case(Command("x")), null!));
    }

    [TestMethod]
    public void Build_PlainCommand_AddsWhatRuntestsAddsBeforeTheCommandsWords()
    {
        var commandLine = Build(Command("http://127.0.0.1:8990/5 -H 'A: b'"));

        CollectionAssert.AreEqual(
            new[]
            {
                "-q", "--output", "/tmp/log/curl5.out", "--include", "--trace-ascii", "/tmp/log/trace5", "--trace-time",
                "http://127.0.0.1:8990/5", "-H", "A: b",
            },
            commandLine.Arguments.ToArray());
        Assert.AreEqual("/tmp/log/curl5.out", commandLine.OutputFile);
        Assert.AreEqual("/tmp/log/stdout5", commandLine.StandardOutputFile);
        Assert.AreEqual("/tmp/log/stderr5", commandLine.StandardErrorFile);
        Assert.IsEmpty(commandLine.StandardInput);
        Assert.IsEmpty(commandLine.Files);
        Assert.IsEmpty(commandLine.Environment);
    }

    [TestMethod]
    public void Build_CaseVerifyingStdout_PassesNoOutput()
    {
        var commandLine = Build(Command("u"), verify: "<stdout>\nx\n</stdout>\n");

        CollectionAssert.DoesNotContain(commandLine.Arguments.ToArray(), "--output");
        Assert.AreEqual("/tmp/log/curl5.out", commandLine.OutputFile);
    }

    [TestMethod]
    public void Build_ForceOutputWithStdout_PassesOutput()
    {
        var commandLine = Build(Command("u", "force-output"), verify: "<stdout>\nx\n</stdout>\n");

        CollectionAssert.Contains(commandLine.Arguments.ToArray(), "--output");
    }

    [TestMethod]
    public void Build_NoOutputNoIncludeNoQAndBinaryTrace_FollowTheOption()
    {
        var commandLine = Build(Command("u", "no-output no-include no-q binary-trace"));

        CollectionAssert.AreEqual(new[] { "--trace", "/tmp/log/trace5", "--trace-time", "u" }, commandLine.Arguments.ToArray());
    }

    [TestMethod]
    public void Build_CommandOverSeveralLines_JoinsThemWithSpaces()
    {
        var commandLine = Build("<command>\nu1\n-d x\n</command>\n");

        CollectionAssert.AreEqual(new[] { "u1", "-d", "x" }, commandLine.Arguments.Skip(7).ToArray());
    }

    [TestMethod]
    public void Build_NoCommand_PassesADash()
    {
        var commandLine = Build("<server>\nhttp\n</server>\n");

        Assert.AreEqual("-", commandLine.Arguments[^1]);
    }

    [TestMethod]
    public void Build_NoClientSection_PassesADashAndNothingElseFromTheCase()
    {
        var testCase = UpstreamTestCaseParser.Parse(UpstreamTestCaseText.Bytes("<testcase>\n</testcase>\n"), UpstreamTestCaseText.Environment());

        var commandLine = UpstreamTestCaseCommandLine.Build(5, testCase, LogDirectory);

        Assert.AreEqual("-", commandLine.Arguments[^1]);
        Assert.IsEmpty(commandLine.Files);
    }

    [TestMethod]
    public void Build_Stdin_IsItsBytesWithNewlinesForcedToCrlfWhenAsked()
    {
        var commandLine = Build(Command("u") + "<stdin crlf=\"yes\" nonewline=\"yes\">\na\nb\n</stdin>\n");

        Assert.AreEqual("a\r\nb", UpstreamTestCaseText.Text(commandLine.StandardInput));
    }

    [TestMethod]
    public void Build_FileParts_AreCreatedInOrderAndAnEmptyNamelessOneIsNot()
    {
        var commandLine = Build(
            Command("u")
            + "<file2 name=\"/tmp/log/two\">\n2\n</file2>\n"
            + "<file name=\"/tmp/log/one\">\n1\n</file>\n"
            + "<file3>\n</file3>\n"
            + "<file4 name=\"/tmp/log/empty\">\n</file4>\n");

        CollectionAssert.AreEqual(new[] { "/tmp/log/one", "/tmp/log/two", "/tmp/log/empty" }, commandLine.Files.Select(file => file.Path).ToArray());
        Assert.AreEqual("1\n", UpstreamTestCaseText.Text(commandLine.Files[0].Contents));
        Assert.IsEmpty(commandLine.Files[2].Contents);
    }

    [TestMethod]
    public void Build_FileWithContentButNoName_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => Build(Command("u") + "<file1>\nx\n</file1>\n"));
    }

    [TestMethod]
    public void Build_Setenv_SetsEachAssignmentAndSetsARemovedVariableEmpty()
    {
        var commandLine = Build(Command("u") + "<setenv>\nA=1=2\n\nB\n</setenv>\n");

        Assert.AreEqual("1=2", commandLine.Environment["A"]);
        Assert.AreEqual(string.Empty, commandLine.Environment["B"]);
        Assert.HasCount(2, commandLine.Environment);
    }

    [TestMethod]
    public void Build_UnclosedQuote_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => Build(Command("'u")));
    }

    private static string Command(string text, string? option = null) =>
        (option is null ? "<command>\n" : $"<command option=\"{option}\">\n") + text + "\n</command>\n";

    private static UpstreamTestCaseCommandLine Build(string client, string verify = "") =>
        UpstreamTestCaseCommandLine.Build(5, Case(client, verify), LogDirectory);

    private static UpstreamTestCase Case(string client, string verify = "") =>
        UpstreamTestCaseParser.Parse(
            UpstreamTestCaseText.Bytes($"<testcase>\n<client>\n{client}</client>\n<verify>\n{verify}</verify>\n</testcase>\n"),
            UpstreamTestCaseText.Environment());
}
