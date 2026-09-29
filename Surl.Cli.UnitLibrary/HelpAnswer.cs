namespace Surl.Cli;

/// <summary>
/// What <c>surl</c> writes for one help subject (ADR-0034 decision 3): each text is written
/// as it is, and is <see cref="string.Empty"/> when nothing goes to that stream.
/// </summary>
/// <param name="Output">The text for stdout.</param>
/// <param name="Error">The text for stderr.</param>
public sealed record HelpAnswer(string Output, string Error);
