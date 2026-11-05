namespace Surl.McpServing;

/// <summary>
/// One binary embedded resource a tool returns: written as a <c>content</c> block
/// <c>{"type":"resource","resource":{"uri","mimeType","blob"}}</c>, the bytes as base64
/// (MCP 2025-06-18 "Server Features > Tools", "Embedded Resources").
/// </summary>
public sealed class McpEmbeddedResource
{
    private readonly byte[] _blob;

    /// <summary>Initializes a new instance of the <see cref="McpEmbeddedResource" /> class.</summary>
    /// <param name="uri">The resource's URI.</param>
    /// <param name="mimeType">The resource's media type.</param>
    /// <param name="blob">The resource's bytes; copied.</param>
    /// <exception cref="ArgumentException"><paramref name="uri" /> or <paramref name="mimeType" /> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="blob" /> is <see langword="null" />.</exception>
    public McpEmbeddedResource(string uri, string mimeType, byte[] blob)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        ArgumentException.ThrowIfNullOrEmpty(mimeType);
        ArgumentNullException.ThrowIfNull(blob);
        Uri = uri;
        MimeType = mimeType;
        _blob = [.. blob];
    }

    /// <summary>Gets the resource's URI.</summary>
    public string Uri { get; }

    /// <summary>Gets the resource's media type.</summary>
    public string MimeType { get; }

    /// <summary>Gets the resource's bytes.</summary>
    public ReadOnlyMemory<byte> Blob => _blob;
}
