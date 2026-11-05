namespace Surl.Cli;

/// <summary>
/// One option line of a config file (ADR-0101 decision 1): the command-line words it stands for,
/// or why it is refused.
/// </summary>
/// <param name="Number">The physical line number, counting every line from 1 (row 15).</param>
/// <param name="Words">
/// The option word (<c>--header</c> for an undashed <c>header</c>) and, when one was given, its value;
/// empty when the line is refused.
/// </param>
/// <param name="Failure">
/// The refusal after <c>&lt;file&gt;:&lt;line&gt;: </c> (decision 5 rows 3 to 6), or <see langword="null"/>.
/// </param>
internal sealed record ConfigFileLine(int Number, IReadOnlyList<string> Words, string? Failure);
