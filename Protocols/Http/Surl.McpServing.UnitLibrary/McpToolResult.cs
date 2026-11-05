using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// What a tool's handler returns, written as the <c>tools/call</c> result (ADR-0103 decision 1): text
/// <c>content</c> blocks, an optional <c>structuredContent</c> object (written only when <c>2025-06-18</c> was
/// negotiated), and <c>isError</c>, always written.
/// </summary>
public sealed class McpToolResult
{
    /// <summary>Initializes a new instance of the <see cref="McpToolResult" /> class.</summary>
    /// <param name="textContent">The text of each <c>content</c> block, in order.</param>
    /// <param name="structuredContent">The <c>structuredContent</c> object, or <see langword="null" /> for none.</param>
    /// <param name="isError">Whether the tool failed; the failure is told in <paramref name="textContent" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="textContent" /> or one of its entries is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="structuredContent" /> is not a JSON object.</exception>
    public McpToolResult(IReadOnlyList<string> textContent, JsonElement? structuredContent = null, bool isError = false)
    {
        ArgumentNullException.ThrowIfNull(textContent);
        if (textContent.Any(text => text is null))
        {
            throw new ArgumentNullException(nameof(textContent), "A content block's text is null.");
        }

        if (structuredContent is { ValueKind: not JsonValueKind.Object })
        {
            throw new ArgumentException("structuredContent must be a JSON object.", nameof(structuredContent));
        }

        TextContent = [.. textContent];
        StructuredContent = structuredContent?.Clone();
        IsError = isError;
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolResult" /> class.</summary>
    /// <param name="textContent">The text of each text <c>content</c> block, in order.</param>
    /// <param name="embeddedResources">The embedded resource blocks, written after the text blocks, in order.</param>
    /// <param name="structuredContent">The <c>structuredContent</c> object, or <see langword="null" /> for none.</param>
    /// <param name="isError">Whether the tool failed.</param>
    /// <exception cref="ArgumentNullException">A list or one of its entries is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="structuredContent" /> is not a JSON object.</exception>
    public McpToolResult(
        IReadOnlyList<string> textContent,
        IReadOnlyList<McpEmbeddedResource> embeddedResources,
        JsonElement? structuredContent = null,
        bool isError = false)
        : this(textContent, structuredContent, isError)
    {
        ArgumentNullException.ThrowIfNull(embeddedResources);
        if (embeddedResources.Any(resource => resource is null))
        {
            throw new ArgumentNullException(nameof(embeddedResources), "An embedded resource is null.");
        }

        EmbeddedResources = [.. embeddedResources];
    }

    /// <summary>Gets the embedded resource blocks, written after the text blocks, in order.</summary>
    public IReadOnlyList<McpEmbeddedResource> EmbeddedResources { get; } = [];

    /// <summary>Gets the text of each <c>content</c> block, in order.</summary>
    public IReadOnlyList<string> TextContent { get; }

    /// <summary>Gets the <c>structuredContent</c> object, or <see langword="null" /> for none.</summary>
    public JsonElement? StructuredContent { get; }

    /// <summary>Gets a value indicating whether the tool failed.</summary>
    public bool IsError { get; }

    /// <summary>Creates a successful result with one text block.</summary>
    /// <param name="text">The block's text.</param>
    /// <returns>A result with <c>isError</c> false.</returns>
    public static McpToolResult FromText(string text) => new([text]);

    /// <summary>Creates a failed result with one text block telling the failure.</summary>
    /// <param name="message">What went wrong.</param>
    /// <returns>A result with <c>isError</c> true.</returns>
    public static McpToolResult FromFailure(string message) => new([message], isError: true);
}
