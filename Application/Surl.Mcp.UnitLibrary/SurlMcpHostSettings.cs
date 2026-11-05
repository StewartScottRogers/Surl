using Surl.Output;

namespace Surl.Mcp;

/// <summary>
/// What whoever starts <c>surl-mcp</c> decides and an agent cannot change (ADR-0103 decisions 4 and 5): the root
/// local paths are confined to, whether a listen URL may name a host other than loopback, how many servers may
/// run at once, and the level each server's log is written at.
/// </summary>
public sealed record SurlMcpHostSettings
{
    /// <summary>The number of servers that may run at once unless <see cref="MaxServers"/> says otherwise.</summary>
    public const int DefaultMaxServers = 8;

    /// <summary>
    /// The directory every local path an option names must lie in (<c>--root</c>, else <c>SURL_MCP_ROOT</c>), or
    /// <see langword="null"/>, the default, for none, which refuses every option that names a local path.
    /// </summary>
    public string? Root { get; init; }

    /// <summary>Whether a listen URL may name a host other than loopback (<c>--allow-non-loopback</c>); off by default.</summary>
    public bool AllowNonLoopback { get; init; }

    /// <summary>The most servers that may run at once (<c>--max-servers</c>); <see cref="DefaultMaxServers"/> by default.</summary>
    public int MaxServers { get; init; } = DefaultMaxServers;

    /// <summary>The level each server's log is written at (<c>--log-level</c>); <c>surl</c>'s default, <see cref="LogLevel.Info"/>.</summary>
    public LogLevel LogLevel { get; init; } = LogLevel.Info;
}
