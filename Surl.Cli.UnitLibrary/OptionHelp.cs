namespace Surl.Cli;

/// <summary>What the help says about one option (ADR-0034 decision 2).</summary>
/// <param name="ArgumentName">The argument's name in the help (<c>&lt;seconds&gt;</c>), or <see langword="null"/> when the option takes none.</param>
/// <param name="Description">The one-line description, at most 34 characters, with no final full stop.</param>
/// <param name="Categories">The names of the <see cref="HelpCategories"/> the option is listed in.</param>
/// <param name="IsInShortList">Whether <c>surl --help</c> lists the option.</param>
/// <param name="Default">What <c>--help &lt;option&gt;</c> gives as the default, or <see langword="null"/> when it gives none.</param>
/// <param name="Explanation">
/// For a loosening option, the paragraph <c>--help testing</c> and <c>--help &lt;option&gt;</c>
/// write about what it loosens and why it is not the default (ADR-0034 decision 5); otherwise
/// <see langword="null"/>.
/// </param>
internal sealed record OptionHelp(
    string? ArgumentName,
    string Description,
    IReadOnlyList<string> Categories,
    bool IsInShortList,
    string? Default,
    string? Explanation = null);
