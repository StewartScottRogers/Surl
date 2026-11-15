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
    public void Build_TwoFilePartsOfOneName_AreOneFileUnderTheFirstName()
    {
        var commandLine = Build(
            Command("u")
            + "<file name=\"/tmp/log/one\">\n1\n</file>\n"
            + "<file name=\"/tmp/log/other\">\n2\n</file>\n");

        Assert.HasCount(1, commandLine.Files);
        Assert.AreEqual("/tmp/log/one", commandLine.Files[0].Path);
        Assert.AreEqual("1\n2\n", UpstreamTestCaseText.Text(commandLine.Files[0].Contents));
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
    public void Build_TwoSetenvParts_SetsTheAssignmentsOfBothWithTheLaterOneWinning()
    {
        var commandLine = Build(Command("u") + "<setenv>\nA=1\nB=2\n</setenv>\n<setenv>\nB=3\nC=4\n</setenv>\n");

        Assert.AreEqual("1", commandLine.Environment["A"]);
        Assert.AreEqual("3", commandLine.Environment["B"]);
        Assert.AreEqual("4", commandLine.Environment["C"]);
    }

    [TestMethod]
    public void Build_UnclosedQuote_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => Build(Command("'u")));
    }

    [TestMethod]
    public void Build_NonAsciiBytesOffWindows_PassesAStringWhoseUtf8IsTheCaseFilesBytes()
    {
        var client = Command("smtp://h/5 --mail-from Avs%hex[%c3%a4]hex%ndaren@example.com");

        var commandLine = BuildFor(client, onWindows: false);

        var argument = commandLine.Arguments[^1];
        CollectionAssert.AreEqual(
            new byte[] { 0x41, 0x76, 0x73, 0xC3, 0xA4, 0x6E, 0x64, 0x61, 0x72, 0x65, 0x6E, 0x40 },
            System.Text.Encoding.UTF8.GetBytes(argument)[..12]);
    }

    [TestMethod]
    public void Build_NonAsciiBytesOnWindows_PassesTheArgumentsAsRead()
    {
        var client = Command("http://h/5 --mail-from Avs%hex[%c3%a4]hex%ndaren");

        var commandLine = BuildFor(client, onWindows: true);

        Assert.AreEqual("AvsÃ¤ndaren", commandLine.Arguments[^1]);
    }

    [TestMethod]
    public void Build_BytesThatAreNotUtf8OffWindows_PassesTheArgumentUnchanged()
    {
        var commandLine = BuildFor(Command("u a%hex[%e4]hex%b"), onWindows: false);

        Assert.AreEqual("aäb", commandLine.Arguments[^1]);
    }

    [TestMethod]
    public void Build_SetenvWithNonAsciiBytesOffWindows_ConvertsTheValueAsAnArgument()
    {
        var commandLine = BuildFor(Command("u") + "<setenv>\nA=%hex[%c3%a5]hex%\nB=%hex[%e4]hex%\n</setenv>\n", onWindows: false);

        Assert.AreEqual("å", commandLine.Environment["A"]);
        Assert.AreEqual("ä", commandLine.Environment["B"]);
    }

    [TestMethod]
    public void Build_NoPlatformGiven_UsesTheRunningOperatingSystem()
    {
        var commandLine = Build(Command("u %hex[%c3%a5]hex%"));

        Assert.AreEqual(OperatingSystem.IsWindows() ? "Ã¥" : "å", commandLine.Arguments[^1]);
    }

    private static UpstreamTestCaseCommandLine BuildFor(string client, bool onWindows) =>
        UpstreamTestCaseCommandLine.Build(5, Case(client), LogDirectory, onWindows);

    private static string Command(string text, string? option = null) =>
        (option is null ? "<command>\n" : $"<command option=\"{option}\">\n") + text + "\n</command>\n";

    private static UpstreamTestCaseCommandLine Build(string client, string verify = "") =>
        UpstreamTestCaseCommandLine.Build(5, Case(client, verify), LogDirectory);

    private static UpstreamTestCase Case(string client, string verify = "") =>
        UpstreamTestCaseParser.Parse(
            UpstreamTestCaseText.Bytes($"<testcase>\n<client>\n{client}</client>\n<verify>\n{verify}</verify>\n</testcase>\n"),
            UpstreamTestCaseText.Environment());
}
