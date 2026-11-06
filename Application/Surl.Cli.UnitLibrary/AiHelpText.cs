namespace Surl.Cli;

/// <summary>
/// What <c>surl --aihelp [topic]</c> writes (ADR-0046 decisions 3, 4, 5 and 8): the overview,
/// one topic page, <c>all</c>, or the unknown-topic answer, in one Markdown structure built from
/// <see cref="CommandLineOptions"/>, <see cref="AiHelpTopics"/> and
/// <see cref="ExitCodeGuidanceTable"/>.
/// </summary>
public static class AiHelpText
{
    /// <summary>The one line a section with nothing in it holds.</summary>
    public const string NothingForThisTopicLine = "Nothing for this topic.";

    /// <summary>The line the unknown-topic answer gives before the topic table.</summary>
    public const string UnknownTopicLine = "Unknown topic provided, here is a list of all topics:";

    private const string AllTopic = "all";
    private const string LimitsCategory = "limits";
    private const string SecurityCategory = "security";
    private const string TestingCategory = "testing";
    private const string ExitCodesTopic = "exit-codes";

    /// <summary>What <c>surl</c> writes for the <c>--aihelp</c> topic <paramref name="topic"/>.</summary>
    /// <param name="topic">
    /// The topic as written after <c>--aihelp</c>; <see langword="null"/> or empty for the overview.
    /// </param>
    /// <returns>The text for stdout; the text for stderr is always empty.</returns>
    public static HelpAnswer Answer(string? topic)
    {
        if (string.IsNullOrEmpty(topic))
        {
            return ToOutput(OverviewLines());
        }

        if (topic.Equals(AllTopic, StringComparison.OrdinalIgnoreCase))
        {
            return ToOutput(AllLines());
        }

        return AiHelpTopics.TryFind(topic, out var found)
            ? ToOutput(TopicPageLines(found))
            : ToOutput(UnknownTopicLines());
    }

    /// <summary>The overview, then every topic page, each followed by one empty line before the next.</summary>
    private static IEnumerable<string> AllLines() =>
        AiHelpTopics.All.Aggregate(OverviewLines(), (lines, topic) => lines.Append(string.Empty).Concat(TopicPageLines(topic)));

    private static IEnumerable<string> OverviewLines() =>
        new[] { "# surl --aihelp: Overview and topic list" }
            .Concat(Section("About", Paragraphs(AiHelpProse.OverviewAbout)))
            .Concat(Section("Command line", Paragraphs(AiHelpProse.OverviewCommandLine)))
            .Concat(Section("Conventions", Paragraphs(AiHelpProse.OverviewConventions)))
            .Concat(Section(
                "Topics",
                [
                    .. TopicTableLines(),
                    string.Empty,
                    "Run `surl --aihelp <topic>` for one topic, or `surl --aihelp all` for this overview and every topic.",
                ]))
            .Concat(Section("Examples", ExampleLines(AiHelpExamples.OverviewTopic)));

    private static IEnumerable<string> UnknownTopicLines() =>
        new[] { "# surl --aihelp: unknown topic", string.Empty, UnknownTopicLine, string.Empty }.Concat(TopicTableLines());

    private static IEnumerable<string> TopicPageLines(AiHelpTopic topic) =>
        new[] { $"# surl --aihelp {topic.Name}: {topic.Description}" }
            .Concat(Section("About", Paragraphs(AiHelpProse.TopicAbout[topic.Name])))
            .Concat(Section("Schemes", SchemeLines(topic)))
            .Concat(Section("Options", OptionSectionLines(topic)))
            .Concat(Section("Exit codes", ExitCodeLines(topic)))
            .Concat(Section("Examples", ExampleLines(topic.Name)));

    /// <summary>An empty line, the <c>##</c> heading, an empty line and the section's lines.</summary>
    private static IEnumerable<string> Section(string heading, IEnumerable<string> lines) =>
        new[] { string.Empty, "## " + heading, string.Empty }.Concat(lines);

    /// <summary>Paragraphs separated by one empty line; list items (<c>- </c>) follow each other with none.</summary>
    private static IEnumerable<string> Paragraphs(IReadOnlyList<string> paragraphs) =>
        paragraphs.SelectMany((paragraph, index) =>
            index == 0 || (IsListItem(paragraph) && IsListItem(paragraphs[index - 1]))
                ? new[] { paragraph }
                : [string.Empty, paragraph]);

    private static bool IsListItem(string paragraph) => paragraph.StartsWith("- ", StringComparison.Ordinal);

    /// <summary>ADR-0046 decision 4's lines for every example of <paramref name="topic"/>, or the nothing line.</summary>
    private static IEnumerable<string> ExampleLines(string topic)
    {
        var examples = AiHelpExamples.All.Where(example => example.Topic == topic).ToArray();

        return examples.Length == 0
            ? [NothingForThisTopicLine]
            : examples.SelectMany((example, index) => index == 0 ? ExampleLines(example) : new[] { string.Empty }.Concat(ExampleLines(example)));
    }

    private static IEnumerable<string> ExampleLines(AiHelpExample example)
    {
        var lines = new List<string> { "### " + example.Title, string.Empty };
        if (PreconditionText(example.Precondition) is { } precondition)
        {
            lines.AddRange(["Given: " + precondition + ".", string.Empty]);
        }

        lines.AddRange(Fenced([string.Concat(example.Arguments.Prepend("surl").Select((word, index) => index == 0 ? word : " " + word))]));
        lines.AddRange(StreamLines("stdout", example.Output));
        lines.AddRange(StreamLines("stderr", example.Error));
        lines.AddRange([string.Empty, ExitCodeLine(example)]);
        if (example.CurlCommandLines.Count > 0)
        {
            lines.AddRange([string.Empty, "Reach it with upstream curl:", string.Empty, .. Fenced(example.CurlCommandLines)]);
        }

        return lines;
    }

    private static string? PreconditionText(AiHelpExamplePrecondition precondition) => precondition switch
    {
        AiHelpExamplePrecondition.DataDirectoryExists => "`<path>` is an existing directory no other surl holds",
        AiHelpExamplePrecondition.DataDirectoryHeldByAnotherSurl => "another surl is serving `<path>` with `--directory`",
        AiHelpExamplePrecondition.KeytabAndUserFileExist => "`http.keytab` is a keytab holding an AES key for `HTTP/<host>@<REALM>`, and `users.txt` a `--user-file`",
        _ => null,
    };

    /// <summary>An empty line, then <c>stdout:</c> and the fenced lines, or <c>stdout: nothing.</c>.</summary>
    private static IEnumerable<string> StreamLines(string stream, IReadOnlyList<string> lines) =>
        lines.Count == 0
            ? [string.Empty, stream + ": nothing."]
            : new[] { string.Empty, stream + ":", string.Empty }.Concat(Fenced(lines));

    private static string ExitCodeLine(AiHelpExample example)
    {
        var number = ((int)example.ExitCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var stopped = example.ServesUntilStopped ? ", once stopped with Ctrl+C or SIGTERM" : string.Empty;
        return $"Exit code: {number} ({Code(Enum.GetName(example.ExitCode)!)}){stopped}.";
    }

    private static IEnumerable<string> Fenced(IEnumerable<string> lines) => new[] { "```" }.Concat(lines).Append("```");

    private static IEnumerable<string> TopicTableLines() =>
        TableLines(["Topic", "Covers"], AiHelpTopics.All.Select(topic => new[] { Code(topic.Name), topic.Description }));

    private static IEnumerable<string> SchemeLines(AiHelpTopic topic) =>
        topic.Schemes.Count == 0
            ? [NothingForThisTopicLine]
            : TableLines(["Scheme", "Default port"], topic.Schemes.Select(scheme => new[] { Code(scheme), DefaultPort(scheme) }));

    private static string DefaultPort(string scheme)
    {
        SchemeDefaultPorts.TryGetDefaultPort(scheme, out var port);
        return port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>ADR-0046 decision 5's option table, then each listed option's explanation.</summary>
    private static IEnumerable<string> OptionSectionLines(AiHelpTopic topic)
    {
        var options = CommandLineOptions.All
            .Where(option => option.Help.Categories.Contains(topic.Name))
            .OrderBy(option => option.LongName, StringComparer.Ordinal)
            .ToArray();

        if (options.Length == 0)
        {
            return [NothingForThisTopicLine];
        }

        string[] header = ["Option", "Argument type", "Default", "Allowed values", "Loosens security", "Categories", "Description"];
        var explanations = options
            .Where(option => option.Help.Explanation is not null)
            .SelectMany(option => new[] { string.Empty, $"{Code("--" + option.LongName)}: {option.Help.Explanation}" });
        return TableLines(header, options.Select(OptionRow)).Concat(explanations);
    }

    private static string[] OptionRow(CommandLineOption option)
    {
        var help = option.Help;
        return
        [
            Code(HelpText.LeftSide(option).TrimStart()),
            option.ArgumentType.Name,
            help.Default ?? "not applicable",
            help.Categories.Contains(LimitsCategory)
                ? option.ArgumentType.AllowedValues + "; 0 means no limit"
                : option.ArgumentType.AllowedValues,
            LoosensSecurity(help.Categories),
            string.Join(", ", help.Categories.Order(StringComparer.Ordinal)),
            help.Description,
        ];
    }

    private static string LoosensSecurity(IReadOnlyList<string> categories)
    {
        if (categories.Contains(TestingCategory))
        {
            return "yes, for tests only";
        }

        return categories.Contains(SecurityCategory) ? "yes, widens what a peer may do" : "no";
    }

    /// <summary>The exit-code rows whose topics hold this topic, or every row on <c>exit-codes</c>.</summary>
    private static IEnumerable<string> ExitCodeLines(AiHelpTopic topic)
    {
        var rows = ExitCodeGuidanceTable.All
            .Where(row => topic.Name == ExitCodesTopic || row.Topics.Contains(topic.Name))
            .ToArray();

        return rows.Length == 0
            ? [NothingForThisTopicLine]
            : TableLines(
                ["Code", "Name", "Meaning", "What to do next"],
                rows.Select(row => new[]
                {
                    ((int)row.Code).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Code(Enum.GetName(row.Code)!),
                    row.Meaning,
                    row.NextStep,
                }));
    }

    /// <summary>A Markdown table: the header row, the <c>| --- |</c> row and one row per entry.</summary>
    private static IEnumerable<string> TableLines(string[] header, IEnumerable<string[]> rows) =>
        new[] { Row(header), Row([.. header.Select(_ => "---")]) }.Concat(rows.Select(Row));

    /// <summary>One table row, each cell's <c>|</c> written <c>\|</c> so no cell splits the row.</summary>
    internal static string Row(string[] cells) =>
        "| " + string.Join(" | ", cells.Select(cell => cell.Replace("|", "\\|", StringComparison.Ordinal))) + " |";

    private static string Code(string text) => "`" + text + "`";

    private static HelpAnswer ToOutput(IEnumerable<string> lines) =>
        new(string.Concat(lines.Select(line => line + Environment.NewLine)), string.Empty);
}
