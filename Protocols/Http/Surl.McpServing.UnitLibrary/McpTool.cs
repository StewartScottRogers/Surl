using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// One MCP tool, added to an <see cref="McpServerRegistry" /> with one explicit
/// <see cref="McpServerRegistry.AddTool" /> call (ADR-0103 decision 1): no scanning, no attributes, no reflection.
/// </summary>
public sealed class McpTool
{
    /// <summary>Initializes a new instance of the <see cref="McpTool" /> class.</summary>
    /// <param name="name">The tool's programmatic name, unique within its registry.</param>
    /// <param name="title">The tool's display name, or <see langword="null" /> to list none.</param>
    /// <param name="description">What the tool does, for the model reading <c>tools/list</c>.</param>
    /// <param name="inputSchema">
    /// The tool's input schema as hand-written JSON text: an object whose <c>type</c> is <c>"object"</c>, using only
    /// the JSON Schema keywords the core checks. <see cref="McpServerRegistry.AddTool" /> refuses any other.
    /// </param>
    /// <param name="handler">
    /// Runs the tool on arguments that already passed the input schema, with the request's cancellation token.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="name" /> is empty.</exception>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="title" /> is <see langword="null" />.</exception>
    public McpTool(
        string name,
        string? title,
        string description,
        string inputSchema,
        Func<JsonElement, CancellationToken, Task<McpToolResult>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(inputSchema);
        ArgumentNullException.ThrowIfNull(handler);
        Name = name;
        Title = title;
        Description = description;
        InputSchema = inputSchema;
        Handler = handler;
    }

    /// <summary>Gets the tool's programmatic name.</summary>
    public string Name { get; }

    /// <summary>Gets the tool's display name, or <see langword="null" /> when it has none.</summary>
    public string? Title { get; }

    /// <summary>Gets what the tool does.</summary>
    public string Description { get; }

    /// <summary>Gets the tool's input schema as the JSON text it was given.</summary>
    public string InputSchema { get; }

    /// <summary>Gets the handler that runs the tool.</summary>
    public Func<JsonElement, CancellationToken, Task<McpToolResult>> Handler { get; }
}
