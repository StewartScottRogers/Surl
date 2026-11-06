using System.Text;
using System.Text.Json;
using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The <c>write_file</c> tool (ADR-0111 decision 2): creates or replaces one file from exactly one of <c>text</c>
/// and <c>base64</c>, through the content store's own upload rules. Registered only under <c>--allow-uploads</c>.
/// </summary>
public static class McpWriteFileTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "write_file";

    internal const string InputSchema =
        """{"type":"object","properties":{"path":{"type":"string","minLength":1,"description":"The file, '/'-separated and relative to the served directory."},"text":{"type":"string","description":"The file's contents as text, written as UTF-8. Give this or base64."},"base64":{"type":"string","description":"The file's contents as base64. Give this or text."}},"required":["path"],"additionalProperties":false}""";

    /// <summary>Creates the tool for one caller.</summary>
    /// <param name="store">The content store.</param>
    /// <param name="caller">Who the session's calls run as.</param>
    /// <returns>The tool.</returns>
    public static McpTool Create(ContentStore store, ContentCaller caller) =>
        new(Name, "Write a file", "Create or replace one file Surl serves.", InputSchema,
            (arguments, cancellationToken) => WriteAsync(store, caller, arguments, cancellationToken));

    private static async Task<McpToolResult> WriteAsync(ContentStore store, ContentCaller caller, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (ReadContents(arguments, out byte[] contents) is { } invalid)
        {
            return McpToolResult.FromFailure(invalid);
        }

        string path = McpToolPath.ReadRelativePath(arguments);
        ContentPathMapping mapping = McpToolPath.Map(store, caller, path);
        if (!mapping.IsMapped)
        {
            return McpToolResult.FromFailure(McpToolFailureText.PathRefused);
        }

        using MemoryStream source = new(contents, writable: false);
        ContentUploadResult written = await store.WriteUploadAsync(mapping, source, cancellationToken).ConfigureAwait(false);
        return written switch
        {
            ContentUploadResult.Written => Wrote(path, contents.Length),
            ContentUploadResult.TooLarge => McpToolResult.FromFailure(McpToolFailureText.LargerThanMaxFilesize),
            _ => McpToolResult.FromFailure(McpToolFailureText.WritingNotPermitted),
        };
    }

    // Returns what is wrong with text and base64, or null with the bytes to write.
    private static string? ReadContents(JsonElement arguments, out byte[] contents)
    {
        contents = [];
        bool hasText = arguments.TryGetProperty("text", out JsonElement text);
        bool hasBase64 = arguments.TryGetProperty("base64", out JsonElement base64);
        if (hasText == hasBase64)
        {
            return McpToolFailureText.ExactlyOneOfTextAndBase64;
        }

        if (hasText)
        {
            contents = Encoding.UTF8.GetBytes(text.GetString()!);
            return null;
        }

        try
        {
            contents = Convert.FromBase64String(base64.GetString()!);
            return null;
        }
        catch (FormatException)
        {
            return McpToolFailureText.Base64NotValid;
        }
    }

    private static McpToolResult Wrote(string path, int size) =>
        new(
            [$"wrote {size} bytes to {path}"],
            McpStructuredContent.Write(writer =>
            {
                writer.WriteString("path", path);
                writer.WriteNumber("size", size);
            }));
}
