namespace Surl.McpServing;

/// <summary>What the server's host gives one MCP session: who the server is, the message limit and the list page size.</summary>
public sealed class McpSessionOptions
{
    /// <summary>The default message limit: 4 MiB, the newline excluded (ADR-0103 decision 1).</summary>
    public const int DefaultMaximumMessageBytes = 4 * 1024 * 1024;

    /// <summary>The default number of entries one page of <c>tools/list</c> lists (ADR-0103 decision 1).</summary>
    public const int DefaultListPageSize = 100;

    /// <summary>Initializes a new instance of the <see cref="McpSessionOptions" /> class.</summary>
    /// <param name="serverInfo">The <c>serverInfo</c> the <c>initialize</c> result carries.</param>
    /// <param name="instructions">The <c>instructions</c> the <c>initialize</c> result carries, or <see langword="null" /> for none.</param>
    /// <param name="maximumMessageBytes">The longest message accepted, in bytes; at least 1.</param>
    /// <param name="listPageSize">The most entries one page of a list holds; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumMessageBytes" /> or <paramref name="listPageSize" /> is less than 1.</exception>
    public McpSessionOptions(
        McpServerInfo serverInfo,
        string? instructions = null,
        int maximumMessageBytes = DefaultMaximumMessageBytes,
        int listPageSize = DefaultListPageSize)
    {
        ArgumentNullException.ThrowIfNull(serverInfo);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumMessageBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(listPageSize, 1);
        ServerInfo = serverInfo;
        Instructions = instructions;
        MaximumMessageBytes = maximumMessageBytes;
        ListPageSize = listPageSize;
    }

    /// <summary>Gets the <c>serverInfo</c> the <c>initialize</c> result carries.</summary>
    public McpServerInfo ServerInfo { get; }

    /// <summary>Gets the <c>instructions</c> the <c>initialize</c> result carries, or <see langword="null" /> for none.</summary>
    public string? Instructions { get; }

    /// <summary>Gets the longest message accepted, in bytes, the newline excluded.</summary>
    public int MaximumMessageBytes { get; }

    /// <summary>Gets the most entries one page of a list holds.</summary>
    public int ListPageSize { get; }
}
