using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// What an MCP server serves: its tools, each added with one explicit <see cref="AddTool" /> call
/// (ADR-0103 decision 1). The <c>initialize</c> result advertises the <c>tools</c> capability only once a tool is
/// added, and <c>tools/list</c> lists them in the order they were added. Add every tool before a dispatcher
/// answers its first message: the registry is read, never locked, while sessions run.
/// </summary>
public sealed class McpServerRegistry
{
    private readonly List<McpRegisteredTool> _tools = [];

    private readonly Dictionary<string, McpRegisteredTool> _toolsByName = new(StringComparer.Ordinal);

    /// <summary>Gets the tools added, in the order they were added.</summary>
    public IReadOnlyList<McpTool> Tools => [.. _tools.Select(registered => registered.Tool)];

    /// <summary>Gets the tools added with their read input schemas, in the order they were added.</summary>
    internal IReadOnlyList<McpRegisteredTool> RegisteredTools => _tools;

    /// <summary>Adds a tool.</summary>
    /// <param name="tool">The tool.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tool" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// A tool of the same name was added already (the first stays), or the input schema is not JSON, is not an object
    /// whose <c>type</c> is <c>"object"</c>, or uses a keyword or value the core does not check.
    /// </exception>
    public void AddTool(McpTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (_toolsByName.ContainsKey(tool.Name))
        {
            throw new ArgumentException($"A tool named {tool.Name} is already registered.", nameof(tool));
        }

        McpToolInputSchema inputSchema;
        JsonElement inputSchemaJson;
        try
        {
            inputSchema = McpToolInputSchema.ReadToolSchema(tool.InputSchema, out inputSchemaJson);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException($"The tool {tool.Name} has an input schema the core cannot check: {exception.Message}", nameof(tool), exception);
        }

        McpRegisteredTool registered = new(tool, inputSchemaJson, inputSchema);
        _tools.Add(registered);
        _toolsByName.Add(tool.Name, registered);
    }

    /// <summary>Finds a tool by name.</summary>
    /// <param name="name">The tool's name.</param>
    /// <param name="tool">The tool, when found.</param>
    /// <returns>Whether a tool of that name was added.</returns>
    internal bool TryGetTool(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out McpRegisteredTool? tool) =>
        _toolsByName.TryGetValue(name, out tool);
}
