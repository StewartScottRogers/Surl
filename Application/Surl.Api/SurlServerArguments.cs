using Surl.Cli;
using Surl.Composition;
using Surl.Output;

namespace Surl.Api;

/// <summary>
/// Turns <see cref="SurlServerOptions"/> into <c>surl</c>'s own words and parses them as
/// <c>surl</c> does (ADR-0099, decision 4): each listen URL, then each property that differs from
/// its default as the option it names, then <see cref="SurlServerOptions.AdditionalArguments"/>.
/// </summary>
internal static class SurlServerArguments
{
    /// <summary>
    /// Throws for options no start can be made from: no listen URL, a null word, a data directory
    /// together with in-memory files, or an in-memory file path that is not a plain relative path.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The options are misused.</exception>
    public static void ThrowIfMisused(SurlServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ListenUrls.Count == 0)
        {
            throw new ArgumentException("ListenUrls names no listen URL.", nameof(options));
        }

        if (options.ListenUrls.Contains(null!) || options.AdditionalArguments.Contains(null!))
        {
            throw new ArgumentException("ListenUrls and AdditionalArguments may not hold null.", nameof(options));
        }

        if (options.DataDirectory is not null && options.Files.Count > 0)
        {
            throw new ArgumentException("DataDirectory and Files cannot both be given.", nameof(options));
        }

        InMemoryFiles.ThrowIfMisused(options.Files);
    }

    /// <summary>Composes the arguments <c>surl</c> would be given for <paramref name="options"/>.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The arguments, in order.</returns>
    public static IReadOnlyList<string> Compose(SurlServerOptions options)
    {
        List<string> arguments = [.. options.ListenUrls];
        if (options.DataDirectory is { } dataDirectory)
        {
            arguments.AddRange(["--directory", dataDirectory]);
        }

        AddFlag(arguments, options.AllowUploads, "--allow-uploads");
        AddFlag(arguments, options.ListDirectories, "--list-directories");
        AddFlag(arguments, options.FollowSymlinks, "--follow-symlinks");
        AddFlag(arguments, options.ServeDotFiles, "--serve-dot-files");
        if (options.LogLevel != LogLevel.Info)
        {
            arguments.AddRange(["--log-level", options.LogLevel.ToString().ToLowerInvariant()]);
        }

        arguments.AddRange(options.AdditionalArguments);
        return arguments;
    }

    /// <summary>
    /// Parses <paramref name="options"/>' arguments as <c>surl</c> parses them, with
    /// <paramref name="extendedSchemes"/>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="extendedSchemes">The extended schemes served, none while extensions are off.</param>
    /// <returns>The command line to compose.</returns>
    /// <exception cref="SurlServerStartException"><c>surl</c> would refuse the command line.</exception>
    /// <exception cref="ArgumentException">The arguments ask for help, the version, a log file or a trace file.</exception>
    public static SurlCommandLine Parse(SurlServerOptions options, IReadOnlyCollection<string> extendedSchemes)
    {
        var parsed = CommandLineParser.Parse(Compose(options), extendedSchemes);
        return parsed.Outcome switch
        {
            CommandLineOutcome.Serve => RefuseHostOwnedFiles(parsed.CommandLine!),
            CommandLineOutcome.Refused => throw new SurlServerStartException(ToStartFailure(parsed.Failure!)),
            _ => throw new ArgumentException(
                "--help, --aihelp, --manual and --version start no server.", nameof(options)),
        };
    }

    /// <summary>The refusal as <c>surl</c> writes it: the message, then the try-help line when it has one.</summary>
    /// <param name="failure">The refusal.</param>
    /// <returns>The exit code and lines.</returns>
    internal static SurlStartFailure ToStartFailure(CommandLineFailure failure) =>
        new(failure.ExitCode, failure.FollowedByTryHelpLine ? [failure.Message, CommandLineFailure.TryHelpLine] : [failure.Message]);

    private static SurlCommandLine RefuseHostOwnedFiles(SurlCommandLine commandLine) =>
        commandLine.LogFile is null && commandLine.TraceFile is null
            ? commandLine
            : throw new ArgumentException(
                "--log-file, --trace and --trace-ascii belong to a host; write the log to SurlServerOptions.Log.", "options");

    private static void AddFlag(List<string> arguments, bool isOn, string option)
    {
        if (isOn)
        {
            arguments.Add(option);
        }
    }
}
