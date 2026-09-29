namespace Surl.Cli;

/// <summary>What a parsed command line tells <c>surl</c> to do.</summary>
public enum CommandLineOutcome
{
    /// <summary>Serve the listen URLs with the parsed options.</summary>
    Serve,

    /// <summary>
    /// Write <see cref="HelpText.Answer"/>'s texts for <see cref="CommandLineParseResult.HelpSubject"/>
    /// to stdout and stderr and exit with <c>SurlExitCode.Ok</c>.
    /// </summary>
    ShowHelp,

    /// <summary>
    /// Write <see cref="AiHelpText.Answer"/>'s texts for <see cref="CommandLineParseResult.AiHelpTopic"/>
    /// to stdout and stderr and exit with <c>SurlExitCode.Ok</c> (ADR-0046 decision 1).
    /// </summary>
    ShowAiHelp,

    /// <summary>Write <see cref="ManualText.Text"/> to stdout and exit with <c>SurlExitCode.Ok</c> (ADR-0034 decision 6).</summary>
    ShowManual,

    /// <summary>Write <see cref="VersionText.Compose"/>'s text to stdout and exit with <c>SurlExitCode.Ok</c>.</summary>
    ShowVersion,

    /// <summary>Write the failure to stderr and exit with its exit code.</summary>
    Refused,
}
