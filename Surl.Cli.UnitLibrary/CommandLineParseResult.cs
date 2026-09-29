namespace Surl.Cli;

/// <summary>
/// What <see cref="CommandLineParser.Parse"/> made of a whole command line: serve a
/// <see cref="SurlCommandLine"/>, show the help, show the version, or a refusal.
/// </summary>
public sealed class CommandLineParseResult
{
    private CommandLineParseResult(
        CommandLineOutcome outcome, SurlCommandLine? commandLine, CommandLineFailure? failure, string? helpSubject = null)
    {
        Outcome = outcome;
        CommandLine = commandLine;
        Failure = failure;
        HelpSubject = helpSubject;
    }

    /// <summary>The result that tells <c>surl</c> to show the version.</summary>
    public static CommandLineParseResult ShowVersion { get; } = new(CommandLineOutcome.ShowVersion, null, null);

    /// <summary>The result that tells <c>surl</c> to show the manual.</summary>
    public static CommandLineParseResult ShowManual { get; } = new(CommandLineOutcome.ShowManual, null, null);

    /// <summary>What the command line tells <c>surl</c> to do.</summary>
    public CommandLineOutcome Outcome { get; }

    /// <summary>
    /// The command line to serve when <see cref="Outcome"/> is <see cref="CommandLineOutcome.Serve"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public SurlCommandLine? CommandLine { get; }

    /// <summary>
    /// Why the command line was refused when <see cref="Outcome"/> is
    /// <see cref="CommandLineOutcome.Refused"/>; otherwise <see langword="null"/>.
    /// </summary>
    public CommandLineFailure? Failure { get; }

    /// <summary>
    /// The help subject when <see cref="Outcome"/> is <see cref="CommandLineOutcome.ShowHelp"/>:
    /// <see langword="null"/> when none was given or it was empty, otherwise the argument
    /// exactly as written. <see langword="null"/> for every other outcome.
    /// </summary>
    public string? HelpSubject { get; }

    /// <summary>A result that tells <c>surl</c> to show the help for <paramref name="subject"/> (ADR-0034 decision 4).</summary>
    /// <param name="subject">The subject as written after <c>-h</c>/<c>--help</c>, or <see langword="null"/> when none followed.</param>
    /// <returns>The help result; an empty subject is carried as <see langword="null"/>.</returns>
    public static CommandLineParseResult ShowHelp(string? subject) =>
        new(CommandLineOutcome.ShowHelp, null, null, string.IsNullOrEmpty(subject) ? null : subject);

    /// <summary>A result that tells <c>surl</c> to serve <paramref name="commandLine"/>.</summary>
    /// <param name="commandLine">The parsed command line, with at least one listen URL.</param>
    /// <returns>The serve result.</returns>
    public static CommandLineParseResult Serve(SurlCommandLine commandLine) =>
        new(CommandLineOutcome.Serve, commandLine, null);

    /// <summary>A result carrying the failure that refused the command line.</summary>
    /// <param name="failure">The exit code and message.</param>
    /// <returns>The refused result.</returns>
    public static CommandLineParseResult Refused(CommandLineFailure failure) =>
        new(CommandLineOutcome.Refused, null, failure);
}
