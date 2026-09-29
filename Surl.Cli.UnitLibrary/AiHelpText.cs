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
            .Concat(Section("About", [NothingForThisTopicLine]))
            .Concat(Section("Command line", [NothingForThisTopicLine]))
            .Concat(Section("Conventions", [NothingForThisTopicLine]))
            .Concat(Section(
                "Topics",
                [
                    .. TopicTableLines(),
                    string.Empty,
                    "Run `surl --aihelp <topic>` for one topic, or `surl --aihelp all` for this overview and every topic.",
                ]))
            .Concat(Section("Examples", [NothingForThisTopicLine]));

    private static IEnumerable<string> UnknownTopicLines() =>
        new[] { "# surl --aihelp: unknown topic", string.Empty, UnknownTopicLine, string.Empty }.Concat(TopicTableLines());

    private static IEnumerable<string> TopicPageLines(AiHelpTopic topic) =>
        new[] { $"# surl --aihelp {topic.Name}: {topic.Description}" }
            .Concat(Section("About", [NothingForThisTopicLine]))
            .Concat(Section("Schemes", SchemeLines(topic)))
            .Concat(Section("Options", OptionSectionLines(topic)))
            .Concat(Section("Exit codes", ExitCodeLines(topic)))
            .Concat(Section("Examples", [NothingForThisTopicLine]));

    /// <summary>An empty line, the <c>##</c> heading, an empty line and the section's lines.</summary>
    private static IEnumerable<string> Section(string heading, IEnumerable<string> lines) =>
        new[] { string.Empty, "## " + heading, string.Empty }.Concat(lines);

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
