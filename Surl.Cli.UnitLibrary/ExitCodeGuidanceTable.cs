using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// ADR-0046 decision 6's exit-code guidance: one row per <see cref="SurlExitCode"/> member, in
/// numeric order, each meaning checked against where <c>CommandLineRunner</c>,
/// <c>CommandLineParser</c> and the serving engine return the code.
/// </summary>
internal static class ExitCodeGuidanceTable
{
    private static readonly ExitCodeGuidance[] Table =
    [
        new(
            SurlExitCode.Ok,
            "Help, the manual or the version was written, or surl was stopped by Ctrl+C or SIGTERM",
            "Nothing: surl succeeded, or stopped cleanly when asked",
            ["listen-urls", "surl"]),
        new(
            SurlExitCode.UnsupportedProtocol,
            "A listen URL names a scheme this build does not serve",
            "Run surl --version to list the schemes this build serves, and use one of them",
            ["listen-urls"]),
        new(
            SurlExitCode.FailedInit,
            "The command line cannot be used: an option or its argument refused, an option not available in this build, "
            + "no listen URL, a malformed --user-file, or a --cacert file that does not exist",
            "Read the surl: line on stderr, which names what was refused, and fix it; the option tables give each option's allowed values",
            ["auth", "limits", "surl", "tls"]),
        new(
            SurlExitCode.MalformedUrl,
            "A listen URL is malformed",
            "Write the listen URL as scheme://host[:port][/], with no user name, path, query or fragment",
            ["listen-urls"]),
        new(
            SurlExitCode.CouldNotResolveHost,
            "A listen URL names a host that resolves to nothing",
            "Use an IP address such as 127.0.0.1, or a host name that resolves",
            ["listen-urls"]),
        new(
            SurlExitCode.CouldNotWriteFile,
            "The .surl folder or its lock file cannot be created, a log or trace file cannot be opened, "
            + "or the trace file is the --log-file file",
            "Make the data directory writable by the user surl runs as, or give a log or trace file that can be opened "
            + "and is not the --log-file file",
            ["content", "logging"]),
        new(
            SurlExitCode.CouldNotReadFile,
            "The data directory cannot be opened, or the --user-file, the MQTT retained-message file or the mail store cannot be read",
            "Check the path exists and the user surl runs as can read it; surl creates neither",
            ["auth", "content", "mqtt", "smtp"]),
        new(
            SurlExitCode.BindFailed,
            "A listener cannot bind its address and port",
            "Use another port, or port 0 and read the bound port from the Listening on line, and an address this machine has",
            ["listen-urls"]),
        new(
            SurlExitCode.CertificateProblem,
            "A secure listen URL has no certificate, or the --cert or --key file cannot be used",
            "Give --cert and --key files that load, with --pass when the key needs one, or --self-signed in a test",
            ["tls"]),
        new(
            SurlExitCode.CaCertificateBadFile,
            "The --cacert file cannot be read",
            "Give a --cacert file that holds certificates",
            ["tls"]),
        new(
            SurlExitCode.DataDirectoryInUse,
            "Another surl holds the data directory",
            "Stop the other surl, give another --directory, or serve in memory without --directory",
            ["content"]),
        new(
            SurlExitCode.InternalError,
            "surl failed while serving",
            "Report it with the command line and the stderr line: it is a defect in surl",
            ["surl"]),
    ];

    /// <summary>Every row, one per <see cref="SurlExitCode"/> member, in numeric order.</summary>
    public static IReadOnlyList<ExitCodeGuidance> All => Table;
}
