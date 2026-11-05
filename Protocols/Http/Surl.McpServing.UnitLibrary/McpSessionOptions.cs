namespace Surl.McpServing;

/// <summary>What the server's host gives one MCP session: who the server is, what it serves, and the message limit.</summary>
public sealed class McpSessionOptions
{
    /// <summary>The default message limit: 4 MiB, the newline excluded (ADR-0103 decision 1).</summary>
    public const int DefaultMaximumMessageBytes = 4 * 1024 * 1024;

    /// <summary>Initializes a new instance of the <see cref="McpSessionOptions" /> class.</summary>
    /// <param name="serverInfo">The <c>serverInfo</c> the <c>initialize</c> result carries.</param>
    /// <param name="capabilities">The capabilities the <c>initialize</c> result advertises.</param>
    /// <param name="instructions">The <c>instructions</c> the <c>initialize</c> result carries, or <see langword="null" /> for none.</param>
    /// <param name="maximumMessageBytes">The longest message accepted, in bytes; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumMessageBytes" /> is less than 1.</exception>
    public McpSessionOptions(
        McpServerInfo serverInfo,
        McpServerCapabilities capabilities,
        string? instructions = null,
        int maximumMessageBytes = DefaultMaximumMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(serverInfo);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumMessageBytes, 1);
        ServerInfo = serverInfo;
        Capabilities = capabilities;
        Instructions = instructions;
        MaximumMessageBytes = maximumMessageBytes;
    }

    /// <summary>Gets the <c>serverInfo</c> the <c>initialize</c> result carries.</summary>
    public McpServerInfo ServerInfo { get; }

    /// <summary>Gets the capabilities the <c>initialize</c> result advertises.</summary>
    public McpServerCapabilities Capabilities { get; }

    /// <summary>Gets the <c>instructions</c> the <c>initialize</c> result carries, or <see langword="null" /> for none.</summary>
    public string? Instructions { get; }

    /// <summary>Gets the longest message accepted, in bytes, the newline excluded.</summary>
    public int MaximumMessageBytes { get; }
}
