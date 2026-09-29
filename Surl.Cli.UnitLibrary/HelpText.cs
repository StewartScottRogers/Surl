namespace Surl.Cli;

/// <summary>
/// What <c>surl --help [subject]</c> writes (ADR-0034 decision 3): the short list, every
/// option, the category list, one category's options, or one option's page, each built from
/// <see cref="CommandLineOptions"/> and <see cref="HelpCategories"/>.
/// </summary>
public static class HelpText
{
    /// <summary>The line <c>--help</c> writes for a subject that names no category and no option.</summary>
    public const string UnknownCategoryLine = "Unknown category provided, here is a list of all categories:";

    /// <summary>The stderr line for a subject that starts with <c>-</c> and names no option.</summary>
    public const string IncorrectOptionNameLine = "surl: Incorrect option name to show help for, see surl -h";

    private const string AllSubject = "all";
    private const string CategorySubject = "category";
    private const string LongOptionPrefix = "--";
    private const string NegationPrefix = "no-";

    /// <summary>What <c>surl</c> writes for the help subject <paramref name="subject"/>.</summary>
    /// <param name="subject">
    /// The subject as written after <c>-h</c>/<c>--help</c>; <see langword="null"/> or empty
    /// for none.
    /// </param>
    /// <returns>The text for stdout and the text for stderr.</returns>
    public static HelpAnswer Answer(string? subject)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return ToOutput(ShortListLines());
        }

        if (subject[0] == '-')
        {
            return AnswerOption(subject);
        }

        if (subject.Equals(AllSubject, StringComparison.OrdinalIgnoreCase))
        {
            return ToOutput(OptionLines(CommandLineOptions.All));
        }

        if (subject.Equals(CategorySubject, StringComparison.OrdinalIgnoreCase))
        {
            return ToOutput(CategoryListLines());
        }

        return HelpCategories.TryFind(subject, out var category)
            ? ToOutput(CategoryPageLines(category))
            : ToOutput([UnknownCategoryLine, string.Empty, .. CategoryListLines()]);
    }

    private static HelpAnswer AnswerOption(string subject)
    {
        var option = FindOption(subject);
        return option is null
            ? new HelpAnswer(string.Empty, IncorrectOptionNameLine + Environment.NewLine)
            : ToOutput(OptionPageLines(option));
    }

    /// <summary>
    /// The option <paramref name="subject"/> names: <c>--name</c> in full, <c>--no-name</c> of
    /// a negatable option, or <c>-x</c>; null for anything else.
    /// </summary>
    private static CommandLineOption? FindOption(string subject) =>
        subject.StartsWith(LongOptionPrefix, StringComparison.Ordinal)
            ? FindLongOption(subject[LongOptionPrefix.Length..])
            : FindShortOption(subject);

    /// <summary>The option <c>-x</c> names: exactly one short-name character after <c>-</c>.</summary>
    private static CommandLineOption? FindShortOption(string subject) =>
        subject.Length == 2 && CommandLineOptions.TryFindShort(subject[1], out var option) ? option : null;

    /// <summary>The option <c>--name</c> names in full, or the negatable option <c>--no-name</c> names.</summary>
    private static CommandLineOption? FindLongOption(string name)
    {
        if (CommandLineOptions.TryFindLong(name, out var option))
        {
            return option;
        }

        return name.StartsWith(NegationPrefix, StringComparison.Ordinal)
            && CommandLineOptions.TryFindLong(name[NegationPrefix.Length..], out var negated)
            && negated.Negatable
                ? negated
                : null;
    }

    private static IEnumerable<string> ShortListLines()
    {
        string[] usage = ["Usage: surl [options...] <url>..."];
        string[] pointerStart =
        [
            string.Empty,
            "This is not the full help; this menu is split into categories.",
            "Use \"--help category\" to get an overview of all categories, which are:",
        ];
        string[] pointerEnd =
        [
            "Use \"--help all\" to list all options",
            "Use \"--help [option]\" to view documentation for a given option",
        ];

        return usage
            .Concat(OptionLines(CommandLineOptions.All.Where(option => option.Help.IsInShortList)))
            .Concat(pointerStart)
            .Concat(HelpLayout.WrapNameList([.. HelpCategories.All.Select(category => category.Name)]))
            .Concat(pointerEnd);
    }

    private static IEnumerable<string> CategoryListLines()
    {
        var width = HelpCategories.All.Max(category => category.Name.Length);
        return HelpCategories.All.Select(category => " " + category.Name.PadRight(width) + "  " + category.Description);
    }

    private static IEnumerable<string> CategoryPageLines(HelpCategory category)
    {
        var options = CommandLineOptions.All.Where(option => option.Help.Categories.Contains(category.Name)).ToArray();
        string[] heading = [$"{category.Name}: {category.Description}"];
        return options.Length == 0 ? heading : heading.Concat(OptionLines(options));
    }

    private static IEnumerable<string> OptionPageLines(CommandLineOption option)
    {
        var help = option.Help;
        var summary = help.Default is null ? $"{help.Description}." : $"{help.Description}. Default: {help.Default}.";
        var categories = $"Categories: {string.Join(", ", help.Categories.Order(StringComparer.Ordinal))}.";

        return new[] { "    " + LeftSide(option).TrimStart() }
            .Concat(HelpLayout.WrapParagraph(summary))
            .Append(string.Empty)
            .Concat(HelpLayout.WrapParagraph(categories))
            .Append(string.Empty);
    }

    /// <summary>The option lines of <paramref name="options"/>, in ordinal order of the long name.</summary>
    private static IReadOnlyList<string> OptionLines(IEnumerable<CommandLineOption> options) =>
        HelpLayout.FormatOptionLines(
            [.. options.OrderBy(option => option.LongName, StringComparer.Ordinal).Select(option => (LeftSide(option), option.Help.Description))]);

    /// <summary><c>-x, --name &lt;arg&gt;</c>, or four spaces and <c>--name &lt;arg&gt;</c> without a short name.</summary>
    private static string LeftSide(CommandLineOption option)
    {
        var shortPart = option.ShortName is null ? "    " : $"-{option.ShortName}, ";
        var argumentPart = option.Help.ArgumentName is null ? string.Empty : " " + option.Help.ArgumentName;
        return shortPart + LongOptionPrefix + option.LongName + argumentPart;
    }

    private static HelpAnswer ToOutput(IEnumerable<string> lines) =>
        new(string.Concat(lines.Select(line => line + Environment.NewLine)), string.Empty);
}
