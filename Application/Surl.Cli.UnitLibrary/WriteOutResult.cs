namespace Surl.Cli;

/// <summary>What <see cref="WriteOutFormat.Expand"/> made of a format for one exchange.</summary>
/// <param name="Text">The text to print to stdout.</param>
/// <param name="UnknownVariables">The names of every <c>%{name}</c> that is no variable, in order; each printed nothing.</param>
public sealed record WriteOutResult(string Text, IReadOnlyList<string> UnknownVariables);
