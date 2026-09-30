using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>ADR-0046 decisions 3, 4, 5, 8 and 9: what <c>surl --aihelp [topic]</c> writes.</summary>
[TestClass]
public sealed partial class AiHelpTextTests
{
    private static readonly string NewLine = Environment.NewLine;

    // ADR-0046 decision 3's sixteen topics and smtp (BL-207), in ordinal order.
    private static readonly string[] AdrTopicNames =
    [
        "auth", "content", "dict", "exit-codes", "gopher", "http", "limits", "listen-urls",
        "logging", "mqtt", "security", "smtp", "ssh", "surl", "telnet", "testing", "tftp", "tls",
    ];

    // ADR-0046 decision 4's section headings, in order, on every topic page.
    private static readonly string[] TopicPageSections = ["## About", "## Schemes", "## Options", "## Exit codes", "## Examples"];

    private static readonly string[] TopicTableLines =
    [
        "| Topic | Covers |",
        "| --- | --- |",
        "| `auth` | Accounts and authentication methods |",
        "| `content` | Served files and the data directory |",
        "| `dict` | DICT protocol |",
        "| `exit-codes` | Exit codes and what to do next |",
        "| `gopher` | GOPHER and GOPHERS protocol |",
        "| `http` | HTTP and HTTPS protocol |",
        "| `limits` | Connection, time and size limits |",
        "| `listen-urls` | Listen URLs, ports and the Listening on line |",
        "| `logging` | Log levels, tracing and the log file |",
        "| `mqtt` | MQTT and MQTTS protocol |",
        "| `security` | Options that widen what a peer may do |",
        "| `smtp` | SMTP and SMTPS protocol |",
        "| `ssh` | SSH protocol |",
        "| `surl` | The command line tool itself |",
        "| `telnet` | TELNET protocol |",
        "| `testing` | Loosening options for tests (warned) |",
        "| `tftp` | TFTP protocol |",
        "| `tls` | TLS certificates and versions |",
    ];

    [TestMethod]
    public void Topics_AreTheAdrsSixteenInOrdinalOrder()
    {
        CollectionAssert.AreEqual(AdrTopicNames, AiHelpTopics.All.Select(topic => topic.Name).ToArray());
    }

    [TestMethod]
    public void Answer_EveryHelpCategory_IsATopic()
    {
        foreach (var category in HelpCategories.All)
        {
            Assert.IsTrue(AiHelpTopics.TryFind(category.Name, out var topic), category.Name);
            Assert.AreEqual(category.Description, topic.Description, category.Name);
            CollectionAssert.AreEqual(category.Schemes.ToArray(), topic.Schemes.ToArray(), category.Name);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Answer_NoTopic_IsTheOverview(string? topic)
    {
        string[] expected =
        [
            "# surl --aihelp: Overview and topic list",
            "",
            "## About",
            "",
            "surl is the server-side mate of curl: for each request upstream curl makes, surl is the server that answers it. "
                + "Where `curl [options] <url>` names what to fetch, `surl [options] <url>` names what to listen on.",
            "",
            "`surl --version` names the schemes this build serves. Each protocol has a topic below with its schemes, "
                + "their default ports, its options and an upstream curl command line that reaches it.",
            "",
            "surl serves until it is stopped with Ctrl+C or SIGTERM, and then exits 0.",
            "",
            "## Command line",
            "",
            "A command line is `surl [options] <url>...`, with at least one listen URL (see the listen-urls topic).",
            "",
            "Options and listen URLs may come in any order and are read left to right; the first error ends the reading, "
                + "and nothing is served. After `--` every argument is a listen URL.",
            "",
            "A long option's argument is the next argument (`--max-time 30`) or follows an equals sign (`--max-time=30`).",
            "",
            "Short options may be bundled: `-vs` is `-v -s`. An option that takes an argument ends the bundle and takes the rest "
                + "of it as its argument, so `-vm30` is `-v -m 30`; with nothing left, it takes the next argument.",
            "",
            "A flag whose Allowed values say `--no-<name> turns it off` is turned off with `--no-<name>`, such as `--no-verbose`.",
            "",
            "An option or argument the command line refuses writes `surl: option --<name>: <reason>` to stderr, then "
                + "`surl: try 'surl --help' or 'surl --manual' for more information`, and exits 2.",
            "",
            "## Conventions",
            "",
            "Every topic page has the same five sections, in this order: About, Schemes, Options, Exit codes and Examples. "
                + "A section with nothing in it holds the one line `Nothing for this topic.`.",
            "",
            "In the option table, Option is how the option is written; Argument type and Allowed values say what its argument "
                + "may be; Default is its value when it is not given (`not applicable` when it has none); Loosens security says "
                + "`yes, for tests only` for an option that loosens a secure default and writes a warning, "
                + "`yes, widens what a peer may do` for one that exposes more, and `no` otherwise; Categories names every topic "
                + "that lists it; Description is its `surl --help` line.",
            "",
            "In the exit-code table, Code is the process exit code, Name its name in surl's source, Meaning when surl returns it, "
                + "and What to do next the step that fixes it.",
            "",
            "In an example, `<port>` stands for the port surl bound and `<path>` for a directory; substitute them. "
                + "An example that serves keeps serving until it is stopped with Ctrl+C or SIGTERM, and then exits 0.",
            "",
            "`surl --help`, `surl --manual`, `surl --version` and these pages are written to stdout at every log level, `-s` included.",
            "",
            "## Topics",
            "",
            .. TopicTableLines,
            "",
            "Run `surl --aihelp <topic>` for one topic, or `surl --aihelp all` for this overview and every topic.",
            "",
            "## Examples",
            "",
            "### Serve HTTP on an ephemeral port",
            "",
            "```",
            "surl http://127.0.0.1:0/",
            "```",
            "",
            "stdout:",
            "",
            "```",
            "Listening on http://127.0.0.1:<port>/",
            "```",
            "",
            "stderr: nothing.",
            "",
            "Exit code: 0 (`Ok`), once stopped with Ctrl+C or SIGTERM.",
            "",
            "Reach it with upstream curl:",
            "",
            "```",
            "curl http://127.0.0.1:<port>/",
            "```",
        ];

        var answer = AiHelpText.Answer(topic);

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    public void Answer_EveryTopic_IsInTheOverviewAndInAll()
    {
        var overview = AiHelpText.Answer(null).Output;
        var all = AiHelpText.Answer("all").Output;

        foreach (var topic in AiHelpTopics.All)
        {
            StringAssert.Contains(overview, $"| `{topic.Name}` | {topic.Description} |" + NewLine, topic.Name);
            StringAssert.Contains(all, AiHelpText.Answer(topic.Name).Output, topic.Name);
        }
    }

    [TestMethod]
    public void Answer_All_IsTheOverviewThenEveryTopicPageEachAfterAnEmptyLine()
    {
        var expected = AiHelpTopics.All.Aggregate(
            AiHelpText.Answer(null).Output,
            (text, topic) => text + NewLine + AiHelpText.Answer(topic.Name).Output);

        var answer = AiHelpText.Answer("all");

        Assert.AreEqual(expected, answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    public void Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName()
    {
        var all = AiHelpText.Answer("all").Output;

        foreach (var option in CommandLineOptions.All)
        {
            var row = $"| `{HelpText.LeftSide(option).TrimStart()}` | {option.ArgumentType.Name} | ";
            StringAssert.Contains(all, row, option.LongName);

            foreach (var category in option.Help.Categories)
            {
                StringAssert.Contains(AiHelpText.Answer(category).Output, row, $"{option.LongName} in {category}");
            }
        }
    }

    [TestMethod]
    public void Answer_Topic_ListsOnlyTheOptionsOfItsCategory()
    {
        foreach (var topic in AiHelpTopics.All)
        {
            var optionRows = OptionRows(AiHelpText.Answer(topic.Name).Output);
            var expected = CommandLineOptions.All.Count(option => option.Help.Categories.Contains(topic.Name));

            Assert.HasCount(expected, optionRows, topic.Name);
        }
    }

    [TestMethod]
    public void Answer_ExitCodes_ListsEverySurlExitCodeMember()
    {
        var page = AiHelpText.Answer("exit-codes").Output;

        foreach (var code in Enum.GetValues<SurlExitCode>())
        {
            StringAssert.Contains(page, $"| {(int)code} | `{code}` | ", code.ToString());
        }
    }

    [TestMethod]
    public void Answer_EveryTopicPage_HasTheSameSectionsInOrder()
    {
        foreach (var topic in AiHelpTopics.All)
        {
            var headings = Lines(AiHelpText.Answer(topic.Name)).Where(line => line.StartsWith("# ", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal)).ToArray();

            Assert.AreEqual($"# surl --aihelp {topic.Name}: {topic.Description}", headings[0], topic.Name);
            CollectionAssert.AreEqual(TopicPageSections, headings[1..], topic.Name);
        }
    }

    [TestMethod]
    public void Answer_EveryAnswer_HasNoEscapeTabOrTrailingSpaceAndWholeTableRows()
    {
        string?[] topics = [null, "all", "nosuch", .. AdrTopicNames];

        foreach (var topic in topics)
        {
            var answer = AiHelpText.Answer(topic);
            Assert.DoesNotContain("\u001b", answer.Output, topic);
            Assert.DoesNotContain("\t", answer.Output, topic);
            Assert.EndsWith(NewLine, answer.Output, topic);

            var lines = Lines(answer);
            Assert.IsFalse(lines.Any(line => line.EndsWith(' ')), topic);
            AssertEveryTableRowHasTheCellsOfItsHeader(lines, topic);
        }
    }

    [TestMethod]
    [DataRow("nosuch")]
    [DataRow("category")]
    [DataRow("http://127.0.0.1:1/")]
    [DataRow("--user")]
    [DataRow("-h")]
    [DataRow("--")]
    [DataRow("allx")]
    public void Answer_UnknownOrOptionLikeTopic_IsTheUnknownTopicAnswer(string topic)
    {
        string[] expected =
        [
            "# surl --aihelp: unknown topic",
            "",
            "Unknown topic provided, here is a list of all topics:",
            "",
            .. TopicTableLines,
        ];

        var answer = AiHelpText.Answer(topic);

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    [DataRow("MQTT", "mqtt")]
    [DataRow("Exit-Codes", "exit-codes")]
    [DataRow("All", "all")]
    [DataRow("ALL", "all")]
    public void Answer_Topic_MatchesInAnyCase(string given, string topic)
    {
        Assert.AreEqual(AiHelpText.Answer(topic), AiHelpText.Answer(given));
    }

    [TestMethod]
    public void Answer_Testing_ListsEachExplanationAfterTheTable()
    {
        var page = AiHelpText.Answer("testing").Output;

        foreach (var option in CommandLineOptions.All.Where(option => option.Help.Explanation is not null))
        {
            StringAssert.Contains(page, NewLine + NewLine + $"`--{option.LongName}`: {option.Help.Explanation}" + NewLine, option.LongName);
        }
    }

    [TestMethod]
    [DataRow("allow-anonymous", "yes, for tests only")]
    [DataRow("self-signed", "yes, for tests only")]
    [DataRow("allow-uploads", "yes, widens what a peer may do")]
    [DataRow("tlsv1.0", "yes, widens what a peer may do")]
    [DataRow("verbose", "no")]
    [DataRow("max-time", "no")]
    public void Answer_Option_SaysWhetherItLoosensSecurity(string longName, string loosens)
    {
        Assert.IsTrue(CommandLineOptions.TryFindLong(longName, out var option));

        var rows = OptionRows(AiHelpText.Answer("all").Output)
            .Where(line => line.StartsWith($"| `{HelpText.LeftSide(option).TrimStart()}` |", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        Assert.HasCount(1, rows);
        var row = rows[0];

        Assert.AreEqual(loosens, Cells(row)[4].Trim());
    }

    [TestMethod]
    public void Answer_Surl_WritesNotApplicableForAnOptionWithNoDefault()
    {
        StringAssert.Contains(
            AiHelpText.Answer("surl").Output,
            "| `-V, --version` | none | not applicable | none | no | surl | Show version number and quit |" + NewLine);
    }

    [TestMethod]
    public void Row_CellWithAPipe_IsEscapedSoTheRowKeepsItsCells()
    {
        var row = AiHelpText.Row(["`none|error|info|verbose|trace`", "a"]);

        Assert.AreEqual("| `none\\|error\\|info\\|verbose\\|trace` | a |", row);
        Assert.HasCount(2, Cells(row));
    }

    [TestMethod]
    public void Answer_Telnet_IsPinned()
    {
        string[] expected =
        [
            "# surl --aihelp telnet: TELNET protocol",
            "",
            "## About",
            "",
            "The TELNET server (RFC 854, RFC 855) negotiates options with the client, reports what the client tells it about "
                + "its terminal and environment, then echoes each line back until the client sends `quit`.",
            "",
            "`curl telnet://127.0.0.1:<port>/` carries the session: what curl reads from stdin is sent, and what the server "
                + "answers is written to stdout. `curl -t <option=value>` sets curl's side of the negotiation.",
            "",
            "## Schemes",
            "",
            "| Scheme | Default port |",
            "| --- | --- |",
            "| `telnet` | 23 |",
            "",
            "## Options",
            "",
            "| Option | Argument type | Default | Allowed values | Loosens security | Categories | Description |",
            "| --- | --- | --- | --- | --- | --- | --- |",
            "| `--max-line <bytes>` | bytes | 8192 | digits with an optional decimal point and more digits, then at most one suffix "
                + "k, m, g, t or p in either case, each 1024 times the one before; at most 9223372036854775807 bytes; 0 means no limit "
                + "| no | dict, gopher, limits, smtp, telnet | Longest command line accepted |",
            "",
            "## Exit codes",
            "",
            "Nothing for this topic.",
            "",
            "## Examples",
            "",
            "### Serve TELNET",
            "",
            "```",
            "surl telnet://127.0.0.1:0/",
            "```",
            "",
            "stdout:",
            "",
            "```",
            "Listening on telnet://127.0.0.1:<port>/",
            "```",
            "",
            "stderr: nothing.",
            "",
            "Exit code: 0 (`Ok`), once stopped with Ctrl+C or SIGTERM.",
            "",
            "Reach it with upstream curl:",
            "",
            "```",
            "curl telnet://127.0.0.1:<port>/",
            "```",
        ];

        var answer = AiHelpText.Answer("telnet");

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    public void Answer_ExitCodes_IsPinned()
    {
        string[] expected =
        [
            "# surl --aihelp exit-codes: Exit codes and what to do next",
            "",
            "## About",
            "",
            "Every exit code surl returns, with what it means and what to do next. The Exit codes section of every other topic lists "
                + "the codes tied to that topic; this page lists them all.",
            "",
            "A failure writes a `surl:` line naming what failed to stderr: `surl: (N) <message>` for a failure found once the "
                + "command line is read, or `surl: option --<name>: <reason>` and the `try` line for a refused command line.",
            "",
            "With `-s` and without `-S`, a failure found once the command line is read writes nothing, and only the exit code "
                + "says what failed; a refused command line is always written.",
            "",
            "## Schemes",
            "",
            "Nothing for this topic.",
            "",
            "## Options",
            "",
            "Nothing for this topic.",
            "",
            "## Exit codes",
            "",
            "| Code | Name | Meaning | What to do next |",
            "| --- | --- | --- | --- |",
            "| 0 | `Ok` | Help, the manual or the version was written, or surl was stopped by Ctrl+C or SIGTERM "
                + "| Nothing: surl succeeded, or stopped cleanly when asked |",
            "| 1 | `UnsupportedProtocol` | A listen URL names a scheme this build does not serve "
                + "| Run surl --version to list the schemes this build serves, and use one of them |",
            "| 2 | `FailedInit` | The command line cannot be used: an option or its argument refused, "
                + "an option not available in this build, no listen URL, a malformed --user-file or --authorized-keys file, a --hostkey file surl cannot use, "
                + "an scp or sftp listen URL with no host key, or a --cacert file that does not exist "
                + "| Read the surl: line on stderr, which names what was refused, and fix it; "
                + "the option tables give each option's allowed values |",
            "| 3 | `MalformedUrl` | A listen URL is malformed "
                + "| Write the listen URL as scheme://host[:port][/], with no user name, path, query or fragment |",
            "| 6 | `CouldNotResolveHost` | A listen URL names a host that resolves to nothing "
                + "| Use an IP address such as 127.0.0.1, or a host name that resolves |",
            "| 23 | `CouldNotWriteFile` | The .surl folder or its lock file cannot be created, a log or trace file cannot be opened, "
                + "or the trace file is the --log-file file "
                + "| Make the data directory writable by the user surl runs as, or give a log or trace file that can be opened "
                + "and is not the --log-file file |",
            "| 37 | `CouldNotReadFile` | The data directory cannot be opened, or the --user-file, an --authorized-keys or --hostkey file, the MQTT retained-message file or the mail store "
                + "cannot be read | Check the path exists and the user surl runs as can read it; surl creates none of them |",
            "| 45 | `BindFailed` | A listener cannot bind its address and port "
                + "| Use another port, or port 0 and read the bound port from the Listening on line, and an address this machine has |",
            "| 58 | `CertificateProblem` | A secure listen URL has no certificate, or the --cert or --key file cannot be used "
                + "| Give --cert and --key files that load, with --pass when the key needs one, or --self-signed in a test |",
            "| 77 | `CaCertificateBadFile` | The --cacert file cannot be read | Give a --cacert file that holds certificates |",
            "| 124 | `DataDirectoryInUse` | Another surl holds the data directory "
                + "| Stop the other surl, give another --directory, or serve in memory without --directory |",
            "| 125 | `InternalError` | surl failed while serving "
                + "| Report it with the command line and the stderr line: it is a defect in surl |",
            "",
            "## Examples",
            "",
            "Nothing for this topic.",
        ];

        var answer = AiHelpText.Answer("exit-codes");

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    public void Answer_EveryTopicAndTheOverview_HasItsAboutText()
    {
        string?[] answers = [null, .. AdrTopicNames];

        foreach (var topic in answers)
        {
            var lines = Lines(AiHelpText.Answer(topic));
            var about = Array.IndexOf(lines, "## About");

            Assert.AreNotEqual(AiHelpText.NothingForThisTopicLine, lines[about + 2], topic);
        }

        CollectionAssert.AreEquivalent(AdrTopicNames, AiHelpProse.TopicAbout.Keys.ToArray());
    }

    [TestMethod]
    public void Answer_EveryTopicButExitCodesAndSecurity_HasAnExample()
    {
        foreach (var topic in AdrTopicNames.Except(["exit-codes", "security"]).Append(AiHelpExamples.OverviewTopic))
        {
            Assert.IsTrue(AiHelpExamples.All.Any(example => example.Topic == topic), topic);
        }

        foreach (var example in AiHelpExamples.All)
        {
            Assert.IsTrue(example.Topic == AiHelpExamples.OverviewTopic || AiHelpTopics.TryFind(example.Topic, out _), example.Title);
        }

        StringAssert.Contains(AiHelpText.Answer("security").Output, "## Examples" + NewLine + NewLine + AiHelpText.NothingForThisTopicLine + NewLine);
    }

    [TestMethod]
    public void Answer_EveryExample_IsOnItsTopicsPage()
    {
        foreach (var example in AiHelpExamples.All)
        {
            var page = AiHelpText.Answer(example.Topic == AiHelpExamples.OverviewTopic ? null : example.Topic).Output;
            var command = string.Join(' ', example.Arguments.Prepend("surl"));

            StringAssert.Contains(page, $"### {example.Title}{NewLine}", example.Title);
            StringAssert.Contains(page, $"```{NewLine}{command}{NewLine}```{NewLine}", example.Title);
            StringAssert.Contains(page, $"Exit code: {(int)example.ExitCode} (`{example.ExitCode}`)", example.Title);
            foreach (var line in example.Output.Concat(example.Error).Concat(example.CurlCommandLines))
            {
                StringAssert.Contains(page, NewLine + line + NewLine, example.Title);
            }
        }
    }

    [TestMethod]
    public void Answer_Content_WritesEachPreconditionAndEachEmptyStream()
    {
        var page = AiHelpText.Answer("content").Output;

        StringAssert.Contains(page, NewLine + "Given: `<path>` is an existing directory no other surl holds." + NewLine);
        StringAssert.Contains(page, NewLine + "Given: another surl is serving `<path>` with `--directory`." + NewLine);
        StringAssert.Contains(page, NewLine + "stdout: nothing." + NewLine);
        StringAssert.Contains(page, NewLine + "stderr: nothing." + NewLine);
        StringAssert.Contains(page, NewLine + "Exit code: 124 (`DataDirectoryInUse`)." + NewLine);
    }

    [TestMethod]
    public void Answer_EveryPage_NamesOnlyOptionsThatExist()
    {
        // surl's SSH host-key note and --throwaway-hostkey warning name the curl options that pin
        // the key (ADR-0051, decisions 8 and 11).
        string[] curlOptionsSurlNames = ["hostpubsha256", "hostpubmd5"];

        foreach (var name in NamedLongOptions().Except(curlOptionsSurlNames))
        {
            var exists = CommandLineOptions.TryFindLong(name, out _)
                || (name.StartsWith("no-", StringComparison.Ordinal) && CommandLineOptions.TryFindLong(name[3..], out var negated) && negated.Negatable);

            Assert.IsTrue(exists, "--" + name);
        }
    }

    [TestMethod]
    public void Prose_EverySurlLineQuoted_IsOneTheCodeWrites()
    {
        // Written by Surl.Cli: each template is matched against what the parser writes.
        var writtenByTheParser = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["surl: (2) no URL specified"] = "surl: " + CommandLineParser.Parse([]).Failure!.Message,
            ["surl: try 'surl --help' or 'surl --manual' for more information"] = "surl: " + CommandLineFailure.TryHelpLine,
            ["surl: option --<name>: <reason>"] = "surl: " + CommandLineParser.Parse(["--max-time", "abc"]).Failure!.Message,
            ["surl: (3) URL rejected: <reason>"] = "surl: " + CommandLineParser.Parse(["http://user@127.0.0.1/"]).Failure!.Message,
            ["surl: (1) Protocol \"<scheme>\" not supported"] = "surl: " + CommandLineParser.Parse(["nosuch://127.0.0.1/"]).Failure!.Message,
        };

        // Written by Surl.Console, checked against its source (BL-140's Notes); the examples that
        // show them are run by Surl.Console.UnitTests (ADR-0046 decision 9, BL-142).
        string[] writtenBySurlConsole =
        [
            "surl: (N) <message>",
            "surl: (N)",
            "surl: warning:",
            "surl: (124) Directory <path> is in use by another surl process",
            "surl: (37) Could not open directory <path>",
            "surl: (45) Could not bind <scheme>://<address>:<port>/: <reason>",
            "surl: (6) Could not resolve host: <host>",
            "surl: (58) <url> needs a certificate: give --cert <file>, or --self-signed for a throwaway one",
            "surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)",
            "surl: (2) --auth gssapi is not available in this build",
            "surl: (2) --hostcert is not available in this build",
            "surl: (2) --allow-weak-ssh-algorithms is not available in this build",
            "surl: (37) Could not read authorized keys <file>",
            "surl: (2) <url> needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one",
            "surl: warning: --throwaway-hostkey: serving a throwaway SSH host key (--hostpubsha256 <base64>); clients must pin it or skip the check (curl -k)",
        ];

        foreach (var (template, written) in writtenByTheParser)
        {
            Assert.MatchesRegex("^" + Regex.Replace(Regex.Escape(template), "<[a-z]+>", ".+") + "$", written, template);
        }

        var quoted = ProseParagraphs()
            .SelectMany(paragraph => QuotedSurlLine().Matches(paragraph).Select(match => match.Groups[1].Value))
            .Distinct()
            .ToArray();
        CollectionAssert.AreEquivalent(writtenByTheParser.Keys.Concat(writtenBySurlConsole).ToArray(), quoted);
    }

    /// <summary>
    /// Every <c>--name</c> in the prose and in the examples' <c>surl</c> arguments and output,
    /// leaving out the upstream curl command lines, whose options are curl's.
    /// </summary>
    private static IEnumerable<string> NamedLongOptions()
    {
        var texts = ProseParagraphs()
            .Select(paragraph => CurlCodeSpan().Replace(paragraph, string.Empty))
            .Concat(AiHelpExamples.All.SelectMany(example => example.Arguments.Concat(example.Output).Concat(example.Error)));

        return texts.SelectMany(text => LongOptionName().Matches(text).Select(match => match.Groups[1].Value)).Distinct();
    }

    private static IEnumerable<string> ProseParagraphs() =>
        AiHelpProse.OverviewAbout
            .Concat(AiHelpProse.OverviewCommandLine)
            .Concat(AiHelpProse.OverviewConventions)
            .Concat(AiHelpProse.TopicAbout.Values.SelectMany(paragraphs => paragraphs));

    /// <summary>Asserts every row of each table in <paramref name="lines"/> has as many cells as the table's header.</summary>
    private static void AssertEveryTableRowHasTheCellsOfItsHeader(string[] lines, string? topic)
    {
        var headerCells = 0;
        var previousWasRow = false;
        foreach (var line in lines)
        {
            var isRow = line.StartsWith("| ", StringComparison.Ordinal);
            if (isRow && !previousWasRow)
            {
                headerCells = Cells(line).Length;
            }

            if (isRow)
            {
                Assert.AreEqual(headerCells, Cells(line).Length, $"{topic}: {line}");
                Assert.EndsWith(" |", line, $"{topic}: {line}");
            }

            previousWasRow = isRow;
        }
    }

    /// <summary>The cells of a table row, split at every <c>|</c> not written <c>\|</c>.</summary>
    private static string[] Cells(string row) => UnescapedPipe().Split(row)[1..^1];

    /// <summary>The option table's rows, header and <c>| --- |</c> row left out.</summary>
    private static string[] OptionRows(string page) =>
        [.. page.Split(NewLine).Where(line => line.StartsWith("| `-", StringComparison.Ordinal))];

    private static string[] Lines(HelpAnswer answer) => answer.Output.Split(NewLine)[..^1];

    private static string Text(IEnumerable<string> lines) => string.Concat(lines.Select(line => line + NewLine));

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex UnescapedPipe();

    // --name, not --<placeholder> nor the --no- of --no-<name>.
    [GeneratedRegex(@"(?<![\w-])--([a-z0-9][a-z0-9.-]*[a-z0-9])(?![\w<-])")]
    private static partial Regex LongOptionName();

    [GeneratedRegex("`curl [^`]*`")]
    private static partial Regex CurlCodeSpan();

    [GeneratedRegex("`(surl: [^`]*)`")]
    private static partial Regex QuotedSurlLine();
}
