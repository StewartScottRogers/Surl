namespace Surl.Conformance;

/// <summary>
/// How upstream's runtests runs curl for a test case (<c>singletest_prepare</c>,
/// <c>singletest_setenv</c> and <c>singletest_run</c> in <c>tests/runner.pm</c> at tag
/// <c>curl-8_21_0</c>): curl's arguments, its standard input, the files created before the run
/// and the environment variables set for it.
/// </summary>
/// <remarks>
/// The arguments are <c>-q</c> unless the command's <c>option</c> says <c>no-q</c>;
/// <c>--output %LOGDIR/curl&lt;N&gt;.out</c> unless the option says <c>no-output</c>, or the case
/// verifies stdout and the option does not say <c>force-output</c>; <c>--include</c> unless the
/// option says <c>no-include</c>; <c>--trace %LOGDIR/trace&lt;N&gt;</c> when the option says
/// <c>binary-trace</c>, else <c>--trace-ascii</c> to the same file; <c>--trace-time</c>; then the
/// case's <c>&lt;command&gt;</c> lines joined with spaces, newlines removed, split as the shell
/// splits them (<see cref="UpstreamTestCaseShellWords"/>), or <c>-</c> when the case has no
/// command.
/// </remarks>
/// <param name="Arguments">curl's arguments, each one argument, unquoted.</param>
/// <param name="StandardInput">The bytes of <c>&lt;stdin&gt;</c>, empty when the case has none.</param>
/// <param name="Files">The files <c>&lt;file&gt;</c> to <c>&lt;file4&gt;</c> create, in that order.</param>
/// <param name="Environment">
/// The variables <c>&lt;setenv&gt;</c> sets, by name; a line naming a variable with no
/// <c>=</c>, which runtests removes, sets it empty.
/// </param>
/// <param name="OutputFile">The file <c>--output</c> names, whether or not it is passed: the file the reply's <c>&lt;data&gt;</c> is checked against.</param>
/// <param name="StandardOutputFile">
/// <c>%LOGDIR/stdout&lt;N&gt;</c>, where runtests redirects curl's stdout; a case's verify
/// section may name it as a <c>&lt;file&gt;</c>, so write curl's stdout there.
/// </param>
/// <param name="StandardErrorFile"><c>%LOGDIR/stderr&lt;N&gt;</c>, likewise for curl's stderr.</param>
public sealed record UpstreamTestCaseCommandLine(
    IReadOnlyList<string> Arguments,
    byte[] StandardInput,
    IReadOnlyList<UpstreamTestCaseClientFile> Files,
    IReadOnlyDictionary<string, string> Environment,
    string OutputFile,
    string StandardOutputFile,
    string StandardErrorFile)
{
    private static readonly string[] FileParts = ["file", "file1", "file2", "file3", "file4"];

    /// <summary>
    /// Builds what runtests runs for case <paramref name="number"/>.
    /// </summary>
    /// <param name="number">The case's number.</param>
    /// <param name="testCase">The case, parsed with the run's variables (<c>%LOGDIR</c> among them).</param>
    /// <param name="logDirectory">The run's <c>%LOGDIR</c>, with <c>/</c> separators.</param>
    /// <returns>The command line.</returns>
    /// <exception cref="FormatException">The command has an unclosed quote, or a file part has no <c>name</c>.</exception>
    public static UpstreamTestCaseCommandLine Build(int number, UpstreamTestCase testCase, string logDirectory)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(logDirectory);

        var outputFile = $"{logDirectory}/curl{number}.out";

        return new UpstreamTestCaseCommandLine(
            BuildArguments(number, testCase, logDirectory, outputFile),
            ReadStandardInput(testCase),
            ReadFiles(testCase),
            ReadEnvironment(testCase),
            outputFile,
            $"{logDirectory}/stdout{number}",
            $"{logDirectory}/stderr{number}");
    }

    private static List<string> BuildArguments(int number, UpstreamTestCase testCase, string logDirectory, string outputFile)
    {
        var command = testCase.FindPart("client", "command");
        var option = command?.Attributes.GetValueOrDefault("option") ?? string.Empty;
        var arguments = new List<string>();
        AddIf(arguments, !option.Contains("no-q", StringComparison.Ordinal), "-q");
        AddIf(arguments, WritesOutputFile(testCase, option), "--output", outputFile);
        AddIf(arguments, !option.Contains("no-include", StringComparison.Ordinal), "--include");
        arguments.Add(option.Contains("binary-trace", StringComparison.Ordinal) ? "--trace" : "--trace-ascii");
        arguments.Add($"{logDirectory}/trace{number}");
        arguments.Add("--trace-time");
        arguments.AddRange(command is null ? ["-"] : UpstreamTestCaseShellWords.Split(JoinCommandLines(command)));

        return arguments;
    }

    private static byte[] ReadStandardInput(UpstreamTestCase testCase) =>
        testCase.FindPart("client", "stdin")?.ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule.CutThenForceCrlf) ?? [];

    private static void AddIf(List<string> arguments, bool condition, params string[] added)
    {
        if (condition)
        {
            arguments.AddRange(added);
        }
    }

    private static bool WritesOutputFile(UpstreamTestCase testCase, string option) =>
        !option.Contains("no-output", StringComparison.Ordinal)
        && (testCase.FindPart("verify", "stdout") is null || option.Contains("force-output", StringComparison.Ordinal));

    private static string JoinCommandLines(UpstreamTestCasePart command) =>
        string.Join(' ', command.Lines).Replace("\n", string.Empty, StringComparison.Ordinal);

    private static List<UpstreamTestCaseClientFile> ReadFiles(UpstreamTestCase testCase)
    {
        var parts = testCase.FindSection("client")?.Parts ?? [];

        // getpart returns the text of every part with the name, so two <file> parts (test 329) make one
        // file under the first part's name; the first part also decides whether the file is made at all.
        return FileParts
            .Select(name => parts.Where(part => part.Name == name).ToList())
            .Where(sameName => sameName.Count > 0 && (sameName[0].Lines.Count > 0 || sameName[0].IsSet("name")))
            .Select(ToClientFile)
            .ToList();
    }

    private static UpstreamTestCaseClientFile ToClientFile(List<UpstreamTestCasePart> sameName) =>
        sameName[0].IsSet("name")
            ? new UpstreamTestCaseClientFile(sameName[0].Attributes["name"], sameName.SelectMany(part => part.ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule.CutThenForceCrlf)).ToArray())
            : throw new FormatException($"<{sameName[0].Name}> has no name attribute.");

    private static Dictionary<string, string> ReadEnvironment(UpstreamTestCase testCase)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        // getpart reads every <setenv> part, so test 433 gets COLUMNS from its second one beside the first.
        var lines = (testCase.FindSection("client")?.Parts ?? []).Where(part => part.Name == "setenv").SelectMany(part => part.Lines);

        foreach (var line in lines)
        {
            AddSetting(environment, line.TrimEnd('\n'));
        }

        return environment;
    }

    private static void AddSetting(Dictionary<string, string> environment, string line)
    {
        if (line.Length == 0)
        {
            return;
        }

        var equals = line.IndexOf('=', StringComparison.Ordinal);
        if (equals < 0)
        {
            environment[line] = string.Empty;
        }
        else
        {
            environment[line[..equals]] = line[(equals + 1)..];
        }
    }
}
