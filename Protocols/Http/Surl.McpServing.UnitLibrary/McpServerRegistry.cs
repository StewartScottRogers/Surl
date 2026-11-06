using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// What an MCP server serves: its tools, each added with one explicit <see cref="AddTool" /> call, and its
/// resources, each provider's added with one explicit <see cref="AddResources" /> call (ADR-0103 decision 1). The
/// <c>initialize</c> result advertises the <c>tools</c> capability only once a tool is added and the
/// <c>resources</c> capability only once a resource is added; <c>tools/list</c> and <c>resources/list</c> list them
/// in the order they were added. Add everything before a dispatcher answers its first message: the registry is
/// read, never locked, while sessions run.
/// </summary>
public sealed class McpServerRegistry
{
    private readonly List<McpRegisteredTool> _tools = [];

    private readonly Dictionary<string, McpRegisteredTool> _toolsByName = new(StringComparer.Ordinal);

    private readonly List<McpRegisteredResource> _resources = [];

    private readonly Dictionary<string, McpRegisteredResource> _resourcesByUri = new(StringComparer.Ordinal);

    /// <summary>Gets the tools added, in the order they were added.</summary>
    public IReadOnlyList<McpTool> Tools => [.. _tools.Select(registered => registered.Tool)];

    /// <summary>Gets the tools added with their read input schemas, in the order they were added.</summary>
    internal IReadOnlyList<McpRegisteredTool> RegisteredTools => _tools;

    /// <summary>Gets the resources added, providers in the order they were added and each provider's in its order.</summary>
    public IReadOnlyList<McpResource> Resources => [.. _resources.Select(registered => registered.Resource)];

    /// <summary>Gets the resources added with the providers that read them, in the order of <see cref="Resources" />.</summary>
    internal IReadOnlyList<McpRegisteredResource> RegisteredResources => _resources;

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

    /// <summary>
    /// Adds every resource a provider lists, reading its <see cref="IMcpResourceProvider.Resources" /> once, now.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="provider" /> is <see langword="null" />, or lists a <see langword="null" /> resource.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// One of the provider's URIs is already registered or is listed twice; none of the provider's resources is added.
    /// </exception>
    public void AddResources(IMcpResourceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        McpResource[] resources = [.. provider.Resources];
        HashSet<string> uris = new(StringComparer.Ordinal);
        foreach (McpResource resource in resources)
        {
            ArgumentNullException.ThrowIfNull(resource, nameof(provider));
            if (_resourcesByUri.ContainsKey(resource.Uri) || !uris.Add(resource.Uri))
            {
                throw new ArgumentException($"A resource with the URI {resource.Uri} is already registered.", nameof(provider));
            }
        }

        foreach (McpResource resource in resources)
        {
            McpRegisteredResource registered = new(resource, provider);
            _resources.Add(registered);
            _resourcesByUri.Add(resource.Uri, registered);
        }
    }

    /// <summary>Finds a resource by URI.</summary>
    /// <param name="uri">The resource's URI.</param>
    /// <param name="resource">The resource, when found.</param>
    /// <returns>Whether a resource with that URI was added.</returns>
    internal bool TryGetResource(string uri, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out McpRegisteredResource? resource) =>
        _resourcesByUri.TryGetValue(uri, out resource);

    /// <summary>Finds a tool by name.</summary>
    /// <param name="name">The tool's name.</param>
    /// <param name="tool">The tool, when found.</param>
    /// <returns>Whether a tool of that name was added.</returns>
    internal bool TryGetTool(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out McpRegisteredTool? tool) =>
        _toolsByName.TryGetValue(name, out tool);
}
