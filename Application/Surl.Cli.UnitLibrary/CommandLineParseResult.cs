namespace Surl.Cli;

/// <summary>
/// What <see cref="CommandLineParser.Parse(IReadOnlyList{string}, IReadOnlyCollection{string})"/> made of a whole command line: serve a
/// <see cref="SurlCommandLine"/>, show the help, show the version, or a refusal.
/// </summary>
public sealed class CommandLineParseResult
{
    private CommandLineParseResult(
        CommandLineOutcome outcome,
        SurlCommandLine? commandLine,
        CommandLineFailure? failure,
        string? helpSubject = null,
        string? aiHelpTopic = null)
    {
        Outcome = outcome;
        CommandLine = commandLine;
        Failure = failure;
        HelpSubject = helpSubject;
        AiHelpTopic = aiHelpTopic;
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

    /// <summary>
    /// The topic when <see cref="Outcome"/> is <see cref="CommandLineOutcome.ShowAiHelp"/>:
    /// <see langword="null"/> when none was given or it was empty, otherwise the argument
    /// exactly as written. <see langword="null"/> for every other outcome.
    /// </summary>
    public string? AiHelpTopic { get; }

    /// <summary>A result that tells <c>surl</c> to show the AI help for <paramref name="topic"/> (ADR-0046 decision 1).</summary>
    /// <param name="topic">The topic as written after <c>--aihelp</c>, or <see langword="null"/> when none followed.</param>
    /// <returns>The AI help result; an empty topic is carried as <see langword="null"/>.</returns>
    public static CommandLineParseResult ShowAiHelp(string? topic) =>
        new(CommandLineOutcome.ShowAiHelp, null, null, aiHelpTopic: string.IsNullOrEmpty(topic) ? null : topic);

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

    /// <summary>
    /// <see langword="true"/> when the refusal is a failure found once the command line was read and <c>-s</c>
    /// (without <c>-S</c>) is in force, so <c>surl</c> writes nothing and only the exit code says what failed.
    /// </summary>
    public bool IsSilenced { get; private init; }

    /// <summary>
    /// <see langword="true"/> for an unsupported-scheme refusal the parser holds until the whole line is read, so a
    /// later <c>-s</c> can silence it; never a result <see cref="CommandLineParser.Parse(IReadOnlyList{string})"/> returns.
    /// </summary>
    public bool IsDeferred { get; private init; }

    /// <summary>An unsupported-scheme refusal the parser holds until the whole line is read (BL-1372).</summary>
    /// <param name="failure">The exit code and message.</param>
    /// <returns>The deferred result.</returns>
    internal static CommandLineParseResult Deferred(CommandLineFailure failure) =>
        new(CommandLineOutcome.Refused, null, failure) { IsDeferred = true };

    /// <summary>A refusal that, like a transfer failure in upstream curl, <c>-s</c> silences (BL-1372).</summary>
    /// <param name="failure">The exit code and message.</param>
    /// <param name="isSilenced">Whether the log level is <see cref="Surl.Output.LogLevel.None"/> once the line is read.</param>
    /// <returns>The refused result.</returns>
    public static CommandLineParseResult RefusedAfterTheLine(CommandLineFailure failure, bool isSilenced) =>
        new(CommandLineOutcome.Refused, null, failure) { IsSilenced = isSilenced };
}
