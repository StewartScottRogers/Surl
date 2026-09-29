namespace Surl.Cli;

/// <summary>What a parsed command line tells <c>surl</c> to do.</summary>
public enum CommandLineOutcome
{
    /// <summary>Serve the listen URLs with the parsed options.</summary>
    Serve,

    /// <summary>Write <see cref="HelpText.Text"/> to stdout and exit with <c>SurlExitCode.Ok</c>.</summary>
    ShowHelp,

    /// <summary>Write <see cref="VersionText.Compose"/>'s text to stdout and exit with <c>SurlExitCode.Ok</c>.</summary>
    ShowVersion,

    /// <summary>Write the failure to stderr and exit with its exit code.</summary>
    Refused,
}
