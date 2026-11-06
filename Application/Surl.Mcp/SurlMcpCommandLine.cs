using System.Globalization;
using Surl.Output;

namespace Surl.Mcp;

/// <summary>
/// Reads the <c>surl-mcp</c> command line (ADR-0103 decision 5): <c>--root &lt;dir&gt;</c>,
/// <c>--allow-non-loopback</c>, <c>--max-servers &lt;n&gt;</c>, <c>--log-level &lt;level&gt;</c>, <c>--help</c> and
/// <c>--version</c>. A later word overrides an earlier one; <c>--help</c> and <c>--version</c> answer at once, as the
/// first of them read. Any other word is refused.
/// </summary>
internal static class SurlMcpCommandLine
{
    /// <summary>The environment variable that names the root when <c>--root</c> is not given.</summary>
    public const string RootEnvironmentVariable = "SURL_MCP_ROOT";

    /// <summary>The fewest servers <c>--max-servers</c> accepts.</summary>
    public const int MinimumMaxServers = 1;

    /// <summary>The most servers <c>--max-servers</c> accepts.</summary>
    public const int MaximumMaxServers = 64;

    private static readonly Dictionary<string, LogLevel> LogLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = LogLevel.None,
        ["error"] = LogLevel.Error,
        ["info"] = LogLevel.Info,
        ["verbose"] = LogLevel.Verbose,
        ["trace"] = LogLevel.Trace,
    };

    /// <summary>Reads <paramref name="arguments"/>.</summary>
    /// <param name="arguments">The command-line words, without the program name.</param>
    /// <param name="rootFromEnvironment"><c>SURL_MCP_ROOT</c>'s value, or <see langword="null"/>; empty is unset.</param>
    /// <returns>What to do.</returns>
    public static SurlMcpCommandLineResult Parse(IReadOnlyList<string> arguments, string? rootFromEnvironment)
    {
        var settings = new SurlMcpHostSettings { Root = string.IsNullOrEmpty(rootFromEnvironment) ? null : rootFromEnvironment };
        for (var index = 0; index < arguments.Count; index++)
        {
            var word = arguments[index];
            var value = index + 1 < arguments.Count ? arguments[index + 1] : null;
            var step = ReadWord(word, value, settings);
            if (step.Outcome is not null)
            {
                return step.Outcome;
            }

            settings = step.Settings!;
            index += step.ValuesRead;
        }

        return SurlMcpCommandLineResult.Serve(settings);
    }

    private static WordStep ReadWord(string word, string? value, SurlMcpHostSettings settings) => word switch
    {
        "--help" => new(SurlMcpCommandLineResult.Help),
        "--version" => new(SurlMcpCommandLineResult.Version),
        "--allow-non-loopback" => new(settings with { AllowNonLoopback = true }, 0),
        "--root" => ReadValue(word, value, settings, ReadRoot),
        "--max-servers" => ReadValue(word, value, settings, ReadMaxServers),
        "--log-level" => ReadValue(word, value, settings, ReadLogLevel),
        _ => Refuse(word, "is unknown"),
    };

    private static WordStep ReadValue(
        string option, string? value, SurlMcpHostSettings settings, Func<string, SurlMcpHostSettings, WordStep> read) =>
        value is null ? Refuse(option, "needs an argument") : read(value, settings);

    private static WordStep ReadRoot(string value, SurlMcpHostSettings settings) =>
        value.Length == 0 ? Refuse("--root", "needs a directory, not the empty string") : new(settings with { Root = value }, 1);

    private static WordStep ReadMaxServers(string value, SurlMcpHostSettings settings) =>
        value.Length > 0 && value.All(char.IsAsciiDigit)
        && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var maxServers)
        && maxServers is >= MinimumMaxServers and <= MaximumMaxServers
            ? new(settings with { MaxServers = maxServers }, 1)
            : Refuse("--max-servers", $"expected a number from {MinimumMaxServers} to {MaximumMaxServers}, got '{value}'");

    private static WordStep ReadLogLevel(string value, SurlMcpHostSettings settings) =>
        LogLevels.TryGetValue(value, out var level)
            ? new(settings with { LogLevel = level }, 1)
            : Refuse("--log-level", $"expected none, error, info, verbose or trace, got '{value}'");

    private static WordStep Refuse(string option, string reason) =>
        new(SurlMcpCommandLineResult.Refuse($"surl-mcp: option {option}: {reason}"));

    /// <summary>One word read: either an answer, or the settings so far and how many value words it took.</summary>
    private sealed record WordStep(SurlMcpCommandLineResult? Outcome, SurlMcpHostSettings? Settings = null, int ValuesRead = 0)
    {
        public WordStep(SurlMcpHostSettings settings, int valuesRead)
            : this(null, settings, valuesRead)
        {
        }
    }
}
