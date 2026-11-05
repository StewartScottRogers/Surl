using System.Net;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The checks <c>start_server</c> makes before it starts anything (ADR-0103 decision 4): <c>-K</c>/<c>--config</c>
/// refused outright, every listen URL on loopback unless the host allows otherwise, and every local path an
/// option names inside the host's root. The words are parsed as <c>surl</c> parses them, so a listen URL or a path
/// is checked however it was spelled.
/// </summary>
/// <param name="settings">The host's settings.</param>
/// <param name="confinement">Where local paths must lie.</param>
internal sealed class StartServerSafetyCheck(SurlMcpHostSettings settings, PathConfinement confinement)
{
    /// <summary>The rule a config file breaks.</summary>
    internal const string ConfigFileRule =
        "-K/--config is not accepted: a config file could name paths this host never checks";

    /// <summary>
    /// Says which rule <paramref name="words"/> break, or returns <see langword="null"/> when they break none. Words
    /// <c>surl</c> itself would refuse are left to <c>Surl.Api</c>, which refuses them with <c>surl</c>'s exit code.
    /// </summary>
    /// <param name="words">The listen URLs and the other options, as <c>surl</c>'s words, in order.</param>
    /// <returns>The rule broken, or <see langword="null"/>.</returns>
    public string? FindRefusal(IReadOnlyList<string> words)
    {
        if (words.Any(IsConfigFileOption))
        {
            return ConfigFileRule;
        }

        // Every extended scheme is accepted here, so the words parse whenever Surl.Api's narrower parse would.
        var commandLine = CommandLineParser.Parse(words, ExtendedSchemeDefaultPorts.Schemes).CommandLine;
        return commandLine is null ? null : FindListenHostRefusal(commandLine) ?? FindPathRefusal(commandLine);
    }

    /// <summary>Whether <paramref name="host"/> is loopback: <c>localhost</c>, an address in <c>127.0.0.0/8</c>, or <c>::1</c>.</summary>
    /// <param name="host">The host as a listen URL gives it, without IPv6 brackets.</param>
    /// <returns><see langword="true"/> when it is loopback.</returns>
    internal static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));

    private static bool IsConfigFileOption(string word) =>
        word == "--config" || word.StartsWith("--config=", StringComparison.Ordinal) || word.StartsWith("-K", StringComparison.Ordinal);

    private string? FindListenHostRefusal(SurlCommandLine commandLine)
    {
        var nonLoopback = commandLine.ListenUrls.FirstOrDefault(listenUrl => !IsLocal(listenUrl));
        return nonLoopback is null || settings.AllowNonLoopback
            ? null
            : $"the listen host {nonLoopback.Host} is not loopback; only whoever starts surl-mcp can allow it, with --allow-non-loopback";
    }

    private string? FindPathRefusal(SurlCommandLine commandLine) =>
        CommandLinePathMembers.PathsOf(commandLine)
            .Select(named => confinement.FindRefusal(named.OptionName, named.Path))
            .FirstOrDefault(refusal => refusal is not null);

    /// <summary>Whether <paramref name="listenUrl"/> listens only locally: on a Unix domain socket, or on a loopback host.</summary>
    /// <param name="listenUrl">The parsed listen URL.</param>
    /// <returns><see langword="true"/> when it is local.</returns>
    internal static bool IsLocal(ListenUrl listenUrl) => listenUrl.UnixSocket is not null || IsLoopback(listenUrl.Host);
}
