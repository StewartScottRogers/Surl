using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The <c>read_file</c> tool (ADR-0111 decision 2): a well-formed UTF-8 file as one text block, any other file as
/// one embedded resource holding its bytes, and a file whose answer would pass the message limit refused.
/// </summary>
public static class McpReadFileTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_file";

    /// <summary>The media type of a file returned as an embedded resource.</summary>
    public const string BinaryMediaType = "application/octet-stream";

    internal const string InputSchema =
        """{"type":"object","properties":{"path":{"type":"string","minLength":1,"description":"The file, '/'-separated and relative to the served directory."}},"required":["path"]}""";

    /// <summary>Creates the tool for one caller.</summary>
    /// <param name="store">The content store.</param>
    /// <param name="caller">Who the session's calls run as.</param>
    /// <param name="maximumMessageBytes">The message limit, <c>--max-message</c>, which bounds the file's answer.</param>
    /// <returns>The tool.</returns>
    public static McpTool Create(ContentStore store, ContentCaller caller, int maximumMessageBytes) =>
        new(Name, "Read a file", "Read one file Surl serves.", InputSchema,
            (arguments, cancellationToken) => ReadAsync(store, caller, maximumMessageBytes, arguments, cancellationToken));

    private static async Task<McpToolResult> ReadAsync(
        ContentStore store, ContentCaller caller, int maximumMessageBytes, JsonElement arguments, CancellationToken cancellationToken)
    {
        string path = McpToolPath.ReadRelativePath(arguments);
        ContentPathMapping mapping = McpToolPath.Map(store, caller, path);
        if (!mapping.IsMapped)
        {
            return McpToolResult.FromFailure(McpToolFailureText.PathRefused);
        }

        if (store.GetFileStatus(mapping) is not { } status)
        {
            return McpToolResult.FromFailure(
                store.GetEntryKind(mapping) == ContentEntryKind.Directory ? McpToolFailureText.IsAFolder : McpToolFailureText.NoSuchFileOrFolder);
        }

        if (status.Length > maximumMessageBytes)
        {
            return McpToolResult.FromFailure(McpToolFailureText.LargerThanOneMessage);
        }

        using MemoryStream bytes = new();
        await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), bytes, cancellationToken).ConfigureAwait(false);
        return Answer(bytes.ToArray(), path, maximumMessageBytes);
    }

    private static McpToolResult Answer(byte[] bytes, string path, int maximumMessageBytes)
    {
        bool isText = Utf8.IsValid(bytes);
        string? text = isText ? Encoding.UTF8.GetString(bytes) : null;
        long answerBytes = isText ? JsonEncodedText.Encode(text!).EncodedUtf8Bytes.Length : (bytes.Length + 2L) / 3 * 4;
        if (answerBytes > maximumMessageBytes)
        {
            return McpToolResult.FromFailure(McpToolFailureText.LargerThanOneMessage);
        }

        return isText
            ? McpToolResult.FromText(text!)
            : new McpToolResult([], [new McpEmbeddedResource("file://" + McpToolPath.ToRequestPath(path), BinaryMediaType, bytes)]);
    }
}
