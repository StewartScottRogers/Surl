using System.Reflection;
using System.Runtime.InteropServices;

namespace Surl.Mcp;

/// <summary>What <c>surl-mcp --help</c> and <c>surl-mcp --version</c> write to standard output, and the version <c>initialize</c> names.</summary>
internal static class SurlMcpHelpText
{
    /// <summary>The <c>--help</c> answer.</summary>
    public static readonly string Help = string.Join(
        Environment.NewLine,
        "Usage: surl-mcp [options]",
        "Serves Surl's MCP tools, which start, list and stop Surl protocol servers and show what their clients",
        "sent, and show and wait for their events (exchanges, logins, store changes, faults), as an MCP server on",
        "standard input and output. Standard output carries only MCP messages; every log",
        "line goes to standard error.",
        " --allow-non-loopback  Let a listen URL name a host other than loopback",
        " --help                Show this help and exit",
        " --log-level <level>   Log each server at none, error, info, verbose or trace (default info)",
        $" --max-servers <n>     Run at most <n> servers at once, {SurlMcpCommandLine.MinimumMaxServers} to {SurlMcpCommandLine.MaximumMaxServers} (default {SurlMcpHostSettings.DefaultMaxServers})",
        $" --root <dir>          Confine local paths to <dir> (else {SurlMcpCommandLine.RootEnvironmentVariable}); with neither, no option may name a local path",
        " --version             Show the version and exit",
        string.Empty);

    /// <summary>
    /// The <c>surl-mcp</c> assembly's informational version, without the <c>+</c> and the build metadata after it:
    /// the <c>serverInfo</c> version (ADR-0103 decision 6).
    /// </summary>
    public static readonly string Version =
        typeof(SurlMcpHelpText).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    /// <summary>The <c>--version</c> answer: <c>surl-mcp &lt;version&gt; (&lt;runtime identifier&gt;)</c>.</summary>
    public static readonly string VersionLine = $"surl-mcp {Version} ({RuntimeInformation.RuntimeIdentifier}){Environment.NewLine}";
}
