using System.Text;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>-K</c>/<c>--config</c> and <c>--url</c> (ADR-0101 decisions 1, 2, 5 rows 1 to 9 and 6): each syntax
/// row, in-place reading, nesting and every refusal with its exit code and text.
/// </summary>
[TestClass]
public sealed class CommandLineParserConfigFileTests
{
    private const string Url = "http://127.0.0.1:0/";
    private const string TryLine = CommandLineFailure.TryHelpLine;

    // Syntax rows 3, 5 and 6: comments, blank lines and every separator (M2).
    [TestMethod]
    [DataRow("log-file = a.log", DisplayName = "Equals")]
    [DataRow("log-file: a.log", DisplayName = "Colon")]
    [DataRow("log-file=a.log", DisplayName = "Equals with no space")]
    [DataRow("log-file a.log", DisplayName = "Space")]
    [DataRow("log-file\ta.log", DisplayName = "Tab")]
    [DataRow("log-file ==: a.log", DisplayName = "A run of separators")]
    [DataRow("--log-file a.log", DisplayName = "Dashed")]
    [DataRow("--log-file=a.log", DisplayName = "Dashed with = is the command-line word (M3)")]
    [DataRow("  \t log-file = a.log", DisplayName = "Indented")]
    [DataRow("log-file = a.log   ", DisplayName = "Trailing blanks")]
    [DataRow("log-file = a.log # a comment", DisplayName = "Comment after the value")]
    [DataRow("log-file = \"a.log\" # a comment", DisplayName = "Comment after a quoted value")]
    [DataRow("log-file = \"a.log\"#a comment", DisplayName = "Comment right after the quote")]
    public void Parse_ConfigLine_SetsTheOptionAsTheCommandLineWould(string line)
    {
        var commandLine = AssertServes(Files(("a.cfg", $"# comment\n\n   # indented comment\n\t\n{line}\n")), "-K", "a.cfg", Url);

        Assert.AreEqual("a.log", commandLine.LogFile);
    }

    [TestMethod]
    public void Parse_ShortOptionInAConfigFile_TakesTheLinesValue()
    {
        var commandLine = AssertServes(Files(("a.cfg", "-u \"ann:pass word\"\n-m 30\n")), "-K", "a.cfg", Url);

        Assert.AreEqual("pass word", commandLine.Accounts.Single().Password);
        Assert.AreEqual(TimeSpan.FromSeconds(30), commandLine.MaxTime);
    }

    [TestMethod]
    public void Parse_FlagLine_SetsTheFlag()
    {
        var commandLine = AssertServes(Files(("a.cfg", "verbose\nno-trace-time\n")), "--trace-time", "-K", "a.cfg", Url);

        Assert.AreEqual(LogLevel.Verbose, commandLine.LogLevel);
        Assert.IsFalse(commandLine.TraceTime);
    }

    // Syntax row 2: CR LF line ends, and a CR at the end of the file.
    [TestMethod]
    [DataRow("log-file = a.log\r\nverbose\r\n")]
    [DataRow("verbose\r\nlog-file = a.log\r")]
    [DataRow("verbose\nlog-file = a.log")]
    public void Parse_LineEnds_AreDropped(string text)
    {
        var commandLine = AssertServes(Files(("a.cfg", text)), "-K", "a.cfg", Url);

        Assert.AreEqual("a.log", commandLine.LogFile);
        Assert.AreEqual(LogLevel.Verbose, commandLine.LogLevel);
    }

    // Syntax row 1: a byte-order mark is skipped.
    [TestMethod]
    public void Parse_ByteOrderMark_IsSkipped()
    {
        var source = new FakeConfigFileSource().WithBytes("a.cfg", [0xEF, 0xBB, 0xBF, .. "log-file = a.log\n"u8.ToArray()]);

        var commandLine = AssertServes(source, "-K", "a.cfg", Url);

        Assert.AreEqual("a.log", commandLine.LogFile);
    }

    // Syntax row 9: each escape inside double quotes, and an unknown one (M1).
    [TestMethod]
    [DataRow(@"""a\tb""", "a\tb", DisplayName = "Tab")]
    [DataRow(@"""a\nb""", "a\nb", DisplayName = "Line feed")]
    [DataRow(@"""a\rb""", "a\rb", DisplayName = "Carriage return")]
    [DataRow(@"""a\vb""", "a\vb", DisplayName = "Vertical tab")]
    [DataRow(@"""a\\b""", @"a\b", DisplayName = "Backslash")]
    [DataRow(@"""a\""b""", "a\"b", DisplayName = "Double quote")]
    [DataRow(@"""a\qb""", "aqb", DisplayName = "Unknown escape keeps the character")]
    [DataRow(@"""a b""", "a b", DisplayName = "Quoted whitespace")]
    [DataRow(@"C:\data\a.log", @"C:\data\a.log", DisplayName = "Backslash outside quotes is ordinary")]
    [DataRow("'a.log'", "'a.log'", DisplayName = "Single quotes are not quotes (row 10)")]
    [DataRow("a#b", "a#b", DisplayName = "A # inside an unquoted value")]
    public void Parse_QuotedValue_ReadsItsEscapes(string written, string expected)
    {
        var commandLine = AssertServes(Files(("a.cfg", $"log-file = {written}\n")), "-K", "a.cfg", Url);

        Assert.AreEqual(expected, commandLine.LogFile);
    }

    // Syntax row 11: "" is the empty argument, refused as the option's type says (M16).
    [TestMethod]
    public void Parse_EmptyQuotedValue_IsTheBlankArgument() =>
        AssertRefused(
            Files(("a.cfg", "log-file = \"\"\n")),
            ["-K", "a.cfg", Url],
            SurlExitCode.FailedInit,
            "a.cfg:1: option --log-file: blank argument where content is expected",
            withTryLine: true);

    // Syntax row 11 and decision 5 row 7: no value at all (M17).
    [TestMethod]
    public void Parse_OptionWithNoValue_RequiresParameter() =>
        AssertRefused(
            Files(("a.cfg", "\nlog-file\n")),
            ["-K", "a.cfg", Url, "-v"],
            SurlExitCode.FailedInit,
            "a.cfg:2: option --log-file: requires parameter",
            withTryLine: true);

    // Decision 5 row 7: unknown options, undashed and dashed (M8, M9), and a URL alone (M10).
    [TestMethod]
    [DataRow("nosuchoption = 1", "option --nosuchoption: is unknown")]
    [DataRow("--nosuchoption", "option --nosuchoption: is unknown")]
    [DataRow("http://127.0.0.1:1/", "option --http: is unknown")]
    [DataRow("= a.log", "option --: is unknown")]
    [DataRow("-", "option -: is unknown")]
    [DataRow("verbose = 1", "option --verbose: does not take a parameter")]
    [DataRow("-v 1", "option -v: does not take a parameter")]
    [DataRow("--log-file=a.log b.log", "text follows the value of --log-file; quote a value that holds a space")]
    [DataRow("-m30 b", "text follows the value of -m; quote a value that holds a space")]
    [DataRow("-Xv a b", "text follows the value of -Xv; quote a value that holds a space")]
    [DataRow("--verbose=1", "option --verbose=1: does not take a parameter")]
    public void Parse_BadOptionLine_IsRefusedAtItsLine(string line, string reason) =>
        AssertRefused(
            Files(("a.cfg", $"# first\n\n{line}\n")),
            ["-K", "a.cfg", Url],
            SurlExitCode.FailedInit,
            $"a.cfg:3: {reason}",
            withTryLine: true);

    // Decision 5 row 5: text after a value (M4's warning, refused).
    [TestMethod]
    [DataRow("log-file a.log more words")]
    [DataRow("log-file = \"a.log\" trailing")]
    public void Parse_TextAfterAValue_IsRefused(string line) =>
        AssertRefused(
            Files(("a.cfg", line + "\n")),
            ["-K", "a.cfg", Url],
            SurlExitCode.FailedInit,
            "a.cfg:1: text follows the value of --log-file; quote a value that holds a space",
            withTryLine: true);

    // Decision 5 row 6: a missing closing quote, a backslash at the end included.
    [TestMethod]
    [DataRow("log-file = \"a.log")]
    [DataRow("log-file = \"a.log\\\"")]
    [DataRow("log-file = \"a.log\\")]
    public void Parse_NoClosingQuote_IsRefused(string line) =>
        AssertRefused(
            Files(("a.cfg", line + "\n")),
            ["-K", "a.cfg", Url],
            SurlExitCode.FailedInit,
            "a.cfg:1: the value of --log-file has no closing double quote",
            withTryLine: true);

    // A refusal never echoes a secret's value (ArgumentHoldsSecret).
    [TestMethod]
    [DataRow("user = \"ann:secret\" more", "a.cfg:1: text follows the value of --user; quote a value that holds a space")]
    [DataRow("-uann:secret more", "a.cfg:1: text follows the value of -u; quote a value that holds a space")]
    [DataRow("user = \"ann:secret", "a.cfg:1: the value of --user has no closing double quote")]
    [DataRow("-vuann:secret more", "a.cfg:1: text follows the value of -vu; quote a value that holds a space")]
    public void Parse_RefusedSecretLine_NeverEchoesTheSecret(string line, string message) =>
        AssertRefused(Files(("a.cfg", line + "\n")), ["-K", "a.cfg", Url], SurlExitCode.FailedInit, message, withTryLine: true);

    // Decision 1: in place - the file after a command-line option overrides it, and before it is overridden (M14, M15).
    [TestMethod]
    public void Parse_FileAfterACommandLineScalar_OverridesIt()
    {
        var commandLine = AssertServes(Files(("a.cfg", "log-file = file.log\n")), "--log-file", "cmdline.log", "-K", "a.cfg", Url);

        Assert.AreEqual("file.log", commandLine.LogFile);
    }

    [TestMethod]
    public void Parse_CommandLineScalarAfterTheFile_OverridesIt()
    {
        var commandLine = AssertServes(Files(("a.cfg", "log-file = file.log\n")), "-K", "a.cfg", "--log-file", "cmdline.log", Url);

        Assert.AreEqual("cmdline.log", commandLine.LogFile);
    }

    // Decision 1: repeats add in order, across several -K (M18), and --config is -K.
    [TestMethod]
    public void Parse_SeveralConfigFiles_AreReadInOrder()
    {
        var source = Files(("a.cfg", "user = a:1\n"), ("b.cfg", "user = b:2\n"), ("a2.cfg", "user = c:3\n"));

        var commandLine = AssertServes(source, "-u", "z:0", "-K", "a.cfg", "--config", "b.cfg", "--config=a2.cfg", Url, "-u", "y:3");

        CollectionAssert.AreEqual(new[] { "z", "a", "b", "c", "y" }, commandLine.Accounts.Select(account => account.UserName).ToArray());
    }

    // Syntax row 12: url = <listen URL>, and --url on the command line.
    [TestMethod]
    public void Parse_UrlLinesAndUrlOption_AddListenUrlsInOrder()
    {
        var source = Files(("a.cfg", "url = http://127.0.0.1:1/\n--url http://127.0.0.1:2/\n"));

        var commandLine = AssertServes(source, "--url", "http://127.0.0.1:3/", "-K", "a.cfg", "http://127.0.0.1:4/");

        CollectionAssert.AreEqual(new[] { 3, 1, 2, 4 }, commandLine.ListenUrls.Select(listenUrl => listenUrl.Port).ToArray());
    }

    [TestMethod]
    public void Parse_UrlLineWithABadListenUrl_IsRefusedAtItsLineWithTheListenUrlRefusal()
    {
        var result = CommandLineParser.Parse(["-K", "a.cfg"], [], Files(("a.cfg", "url = rtmp://127.0.0.1:0/\n")));

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, result.Failure!.ExitCode);
        Assert.AreEqual("a.cfg:1: (1) Protocol \"rtmp\" not supported", result.Failure.Message);
    }

    [TestMethod]
    public void Parse_UrlOptionWithNoUrl_RequiresParameter() =>
        AssertRefused(Files(), ["--url"], SurlExitCode.FailedInit, "option --url: requires parameter", withTryLine: true);

    // Syntax row 14: config = <file> reads in place (M5).
    [TestMethod]
    public void Parse_NestedConfigFile_IsReadInPlace()
    {
        var source = Files(("a.cfg", "config = b.cfg\nuser = a:1\n"), ("b.cfg", "url = http://127.0.0.1:0/\nuser = b:2\n"));

        var commandLine = AssertServes(source, "-K", "a.cfg");

        CollectionAssert.AreEqual(new[] { "b", "a" }, commandLine.Accounts.Select(account => account.UserName).ToArray());
    }

    // Decision 5's warning: every file read, nested and standard input included, in the order opened.
    [TestMethod]
    public void Parse_ConfigFiles_ListsEveryFileReadInOrderNestedIncluded()
    {
        var source = Files(("a.cfg", "config = b.cfg\nconfig = -\n"), ("b.cfg", "user = b:2\n"), ("-", "url = http://127.0.0.1:0/\n"), ("c.cfg", "verbose\n"));

        var commandLine = AssertServes(source, "-K", "a.cfg", "--config", "c.cfg");

        CollectionAssert.AreEqual(new[] { "a.cfg", "b.cfg", "-", "c.cfg" }, commandLine.ConfigFiles.ToArray());
    }

    [TestMethod]
    public void Parse_NoConfigFile_ListsNoConfigFiles() =>
        Assert.IsEmpty(AssertServes(Files(), Url).ConfigFiles);

    [TestMethod]
    public void Parse_FilesNestedFiveDeep_AreRead()
    {
        var source = Files(("1.cfg", "config = 2.cfg\n"), ("2.cfg", "config = 3.cfg\n"), ("3.cfg", "config = 4.cfg\n"), ("4.cfg", "config = 5.cfg\n"), ("5.cfg", "url = http://127.0.0.1:0/\n"));

        var commandLine = AssertServes(source, "-K", "1.cfg");

        Assert.AreEqual(0, commandLine.ListenUrls.Single().Port);
    }

    // Decision 5 row 8: a sixth file open at once.
    [TestMethod]
    public void Parse_FilesNestedSixDeep_AreRefusedAtTheFifthFilesLine()
    {
        var source = Files(("1.cfg", "config = 2.cfg\n"), ("2.cfg", "config = 3.cfg\n"), ("3.cfg", "config = 4.cfg\n"), ("4.cfg", "config = 5.cfg\n"), ("5.cfg", "\nconfig = 6.cfg\n"), ("6.cfg", "url = http://127.0.0.1:0/\n"));

        AssertRefused(source, ["-K", "1.cfg"], SurlExitCode.FailedInit, "5.cfg:2: option --config: config files nest more than 5 deep", withTryLine: true);
    }

    // Decision 5 row 9: a cycle (M6, M7), named at once, case as the source compares.
    [TestMethod]
    public void Parse_FileIncludingItself_IsRefusedAsACycle() =>
        AssertRefused(
            Files(("s.cfg", "config = S.CFG\n")),
            ["-K", "s.cfg", Url],
            SurlExitCode.FailedInit,
            "s.cfg:1: option --config: S.CFG is already being read",
            withTryLine: true);

    [TestMethod]
    public void Parse_TwoFilesIncludingEachOther_AreRefusedAsACycle() =>
        AssertRefused(
            Files(("a.cfg", "config = b.cfg\n"), ("b.cfg", "\n\nconfig = a.cfg\n")),
            ["-K", "a.cfg", Url],
            SurlExitCode.FailedInit,
            "b.cfg:3: option --config: a.cfg is already being read",
            withTryLine: true);

    [TestMethod]
    public void Parse_TheSameFileTwiceInARow_IsReadTwice()
    {
        var commandLine = AssertServes(Files(("a.cfg", "url = http://127.0.0.1:0/\n")), "-K", "a.cfg", "-K", "a.cfg");

        Assert.HasCount(2, commandLine.ListenUrls);
    }

    // Syntax row 13: -K - reads standard input, once (M13).
    [TestMethod]
    public void Parse_StandardInput_IsRead()
    {
        var commandLine = AssertServes(Files(("-", "url = http://127.0.0.1:0/\n")), "-K", "-");

        Assert.AreEqual(0, commandLine.ListenUrls.Single().Port);
    }

    [TestMethod]
    public void Parse_StandardInputTwice_IsRefused() =>
        AssertRefused(
            Files(("-", "verbose\n")),
            ["-K", "-", "-K", "-", Url],
            SurlExitCode.FailedInit,
            "option --config: <stdin> is already being read",
            withTryLine: true);

    [TestMethod]
    public void Parse_RefusalInStandardInput_IsNamedStdin() =>
        AssertRefused(Files(("-", "nosuch\n")), ["-K", "-", Url], SurlExitCode.FailedInit, "<stdin>:1: option --nosuch: is unknown", withTryLine: true);

    // Decision 5 row 1: a file that cannot be read exits 37 with no try line (M11, M12).
    [TestMethod]
    public void Parse_MissingFile_IsCouldNotReadFile() =>
        AssertRefused(Files(), ["-K", "missing.cfg", Url], SurlExitCode.CouldNotReadFile, "(37) Could not read config file missing.cfg", withTryLine: false);

    [TestMethod]
    public void Parse_MissingNestedFile_IsCouldNotReadFileAtTheIncludingLine() =>
        AssertRefused(
            Files(("a.cfg", "\nconfig = nothere.cfg\n")),
            ["-K", "a.cfg", Url],
            SurlExitCode.CouldNotReadFile,
            "a.cfg:2: (37) Could not read config file nothere.cfg",
            withTryLine: false);

    [TestMethod]
    public void Parse_WithoutAConfigFileSource_EveryConfigFileCannotBeRead() =>
        Assert.AreEqual(
            new CommandLineFailure(SurlExitCode.CouldNotReadFile, "(37) Could not read config file a.cfg"),
            CommandLineParser.Parse(["-K", "a.cfg", Url]).Failure);

    [TestMethod]
    public void Parse_RefusalInANestedFile_IsLocatedOnlyByTheNestedFile() =>
        AssertRefused(
            Files(("a.cfg", "config = b.cfg\n"), ("b.cfg", "\nnosuch\n")),
            ["-K", "a.cfg", Url],
            SurlExitCode.FailedInit,
            "b.cfg:2: option --nosuch: is unknown",
            withTryLine: true);

    // Decision 5 row 2 and decision 6: a file past 16 MiB; the source is asked for one byte more.
    [TestMethod]
    public void Parse_FileLargerThan16MiB_IsRefused()
    {
        var source = new FakeConfigFileSource().WithBytes("a.cfg", new byte[ConfigFileText.MaxFileBytes + 1]);

        AssertRefused(source, ["-K", "a.cfg", Url], SurlExitCode.FailedInit, "a.cfg: the file is larger than 16777216 bytes", withTryLine: true);
        Assert.AreEqual(16777217, source.LastMaxByteCount);
    }

    [TestMethod]
    public void Parse_NestedFileLargerThan16MiB_IsNamedByItselfOnly()
    {
        var source = Files(("a.cfg", "config = b.cfg\n")).WithBytes("b.cfg", new byte[ConfigFileText.MaxFileBytes + 1]);

        AssertRefused(source, ["-K", "a.cfg", Url], SurlExitCode.FailedInit, "b.cfg: the file is larger than 16777216 bytes", withTryLine: true);
    }

    // Decision 5 row 3: a line longer than 10 MiB, its line end not counted.
    [TestMethod]
    public void Parse_LineLongerThan10MiB_IsRefused()
    {
        var line = Encoding.ASCII.GetBytes("# " + new string('x', ConfigFileText.MaxLineBytes - 1));
        var source = new FakeConfigFileSource().WithBytes("a.cfg", [.. "verbose\n"u8.ToArray(), .. line, (byte)'\n']);

        AssertRefused(source, ["-K", "a.cfg", Url], SurlExitCode.FailedInit, "a.cfg:2: the line is longer than 10485760 bytes", withTryLine: true);
    }

    [TestMethod]
    public void Parse_LineOf10MiBWithItsCrLf_IsRead()
    {
        var line = Encoding.ASCII.GetBytes("# " + new string('x', ConfigFileText.MaxLineBytes - 2));
        var source = new FakeConfigFileSource().WithBytes("a.cfg", [.. line, (byte)'\r', (byte)'\n']);

        AssertServes(source, "-K", "a.cfg", Url);
    }

    // Decision 5 row 4: a line that is not UTF-8.
    [TestMethod]
    public void Parse_LineNotUtf8_IsRefused()
    {
        var source = new FakeConfigFileSource().WithBytes("a.cfg", [.. "verbose\nlog-file = a"u8.ToArray(), 0xFF, (byte)'\n']);

        AssertRefused(source, ["-K", "a.cfg", Url], SurlExitCode.FailedInit, "a.cfg:2: not UTF-8", withTryLine: true);
    }

    // Decision 1: --help, --version and the rest behave in a file as on the command line.
    [TestMethod]
    public void Parse_HelpInAConfigFile_ShowsItsSubject()
    {
        var result = CommandLineParser.Parse(["-K", "a.cfg", "nosuch-url"], [], Files(("a.cfg", "help = tls\n")));

        Assert.AreEqual(CommandLineOutcome.ShowHelp, result.Outcome);
        Assert.AreEqual("tls", result.HelpSubject);
    }

    [TestMethod]
    public void Parse_HelpWithNoSubjectInAConfigFile_DoesNotTakeTheNextCommandLineWord()
    {
        var result = CommandLineParser.Parse(["-K", "a.cfg", "tls"], [], Files(("a.cfg", "help\n")));

        Assert.AreEqual(CommandLineOutcome.ShowHelp, result.Outcome);
        Assert.IsNull(result.HelpSubject);
    }

    [TestMethod]
    public void Parse_VersionInAConfigFile_ShowsTheVersion() =>
        Assert.AreEqual(CommandLineOutcome.ShowVersion, CommandLineParser.Parse(["-K", "a.cfg"], [], Files(("a.cfg", "version\n"))).Outcome);

    // Decision 5: checks made after the whole line count the files' options too.
    [TestMethod]
    public void Parse_UserInAFileAndOnTheCommandLine_IsGivenTwice() =>
        AssertRefused(
            Files(("a.cfg", "user = ann:1\n")),
            ["-u", "ann:2", "-K", "a.cfg", Url],
            SurlExitCode.FailedInit,
            "option --user: user ann is given twice",
            withTryLine: true);

    [TestMethod]
    public void Parse_ConfigOptionWithNoFile_RequiresParameter() =>
        AssertRefused(Files(), ["-K"], SurlExitCode.FailedInit, "option -K: requires parameter", withTryLine: true);

    [TestMethod]
    public void Parse_NoConfig_CannotBeReversed() =>
        AssertRefused(Files(), ["--no-config", Url], SurlExitCode.FailedInit, "option --no-config: the given option cannot be reversed with a --no- prefix", withTryLine: true);

    [TestMethod]
    public void Parse_NullConfigFileSource_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineParser.Parse([Url], [], null!));

    private static FakeConfigFileSource Files(params (string Path, string Text)[] files) => new(files);

    private static SurlCommandLine AssertServes(IConfigFileSource source, params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments, [], source);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        return result.CommandLine!;
    }

    private static void AssertRefused(IConfigFileSource source, string[] arguments, SurlExitCode exitCode, string message, bool withTryLine) =>
        Assert.AreEqual(new CommandLineFailure(exitCode, message, withTryLine), CommandLineParser.Parse(arguments, [], source).Failure);
}
