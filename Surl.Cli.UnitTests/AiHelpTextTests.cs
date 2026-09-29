using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>ADR-0046 decisions 3, 4, 5, 8 and 9: what <c>surl --aihelp [topic]</c> writes.</summary>
[TestClass]
public sealed partial class AiHelpTextTests
{
    private static readonly string NewLine = Environment.NewLine;

    // ADR-0046 decision 3's sixteen topics, in ordinal order.
    private static readonly string[] AdrTopicNames =
    [
        "auth", "content", "dict", "exit-codes", "gopher", "http", "limits", "listen-urls",
        "logging", "mqtt", "security", "surl", "telnet", "testing", "tftp", "tls",
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
            "Nothing for this topic.",
            "",
            "## Command line",
            "",
            "Nothing for this topic.",
            "",
            "## Conventions",
            "",
            "Nothing for this topic.",
            "",
            "## Topics",
            "",
            .. TopicTableLines,
            "",
            "Run `surl --aihelp <topic>` for one topic, or `surl --aihelp all` for this overview and every topic.",
            "",
            "## Examples",
            "",
            "Nothing for this topic.",
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
            var headings = Lines(AiHelpText.Answer(topic.Name)).Where(line => line.StartsWith('#')).ToArray();

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
            "Nothing for this topic.",
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
                + "| no | dict, gopher, limits, telnet | Longest command line accepted |",
            "",
            "## Exit codes",
            "",
            "Nothing for this topic.",
            "",
            "## Examples",
            "",
            "Nothing for this topic.",
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
            "Nothing for this topic.",
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
            "| 2 | `FailedInit` | The command line cannot be used: an option or its argument refused, no listen URL, "
                + "a malformed --user-file, or a --cacert file that does not exist "
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
            "| 37 | `CouldNotReadFile` | The data directory cannot be opened, or the --user-file or the MQTT retained-message file "
                + "cannot be read | Check the path exists and the user surl runs as can read it; surl creates neither |",
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
}
