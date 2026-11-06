namespace Surl.McpServing;

/// <summary>
/// One resource an <see cref="IMcpResourceProvider" /> lists, written as one entry of <c>resources/list</c>
/// (ADR-0103 decision 1, MCP 2025-06-18 "Server Features > Resources").
/// </summary>
public sealed class McpResource
{
    /// <summary>Initializes a new instance of the <see cref="McpResource" /> class.</summary>
    /// <param name="uri">The resource's URI, unique within its registry.</param>
    /// <param name="name">The resource's name.</param>
    /// <param name="title">The resource's display name, or <see langword="null" /> to list none.</param>
    /// <param name="description">What the resource holds, or <see langword="null" /> to list none.</param>
    /// <param name="mimeType">The resource's media type, or <see langword="null" /> to list none.</param>
    /// <exception cref="ArgumentException"><paramref name="uri" /> or <paramref name="name" /> is null or empty.</exception>
    public McpResource(string uri, string name, string? title, string? description, string? mimeType)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        ArgumentException.ThrowIfNullOrEmpty(name);
        Uri = uri;
        Name = name;
        Title = title;
        Description = description;
        MimeType = mimeType;
    }

    /// <summary>Gets the resource's URI.</summary>
    public string Uri { get; }

    /// <summary>Gets the resource's name.</summary>
    public string Name { get; }

    /// <summary>Gets the resource's display name, or <see langword="null" /> when it has none.</summary>
    public string? Title { get; }

    /// <summary>Gets what the resource holds, or <see langword="null" /> when it says nothing.</summary>
    public string? Description { get; }

    /// <summary>Gets the resource's media type, or <see langword="null" /> when it names none.</summary>
    public string? MimeType { get; }
}
