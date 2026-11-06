using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// Answers <c>tools/list</c> and <c>tools/call</c> (ADR-0103 decision 1, MCP 2025-06-18 "Server Features > Tools"):
/// the list paged with <c>cursor</c> and <c>nextCursor</c>, and a call's arguments checked against the tool's
/// input schema before its handler runs.
/// </summary>
internal static class McpToolMethods
{
    /// <summary>The method that lists the tools.</summary>
    internal const string ListMethod = "tools/list";

    /// <summary>The method that calls a tool.</summary>
    internal const string CallMethod = "tools/call";

    private static readonly JsonElement EmptyArguments = ParseEmptyObject();

    /// <summary>Answers <c>tools/list</c>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="registry">The tools served.</param>
    /// <param name="pageSize">The most tools one page lists.</param>
    /// <param name="version">The protocol revision negotiated; <c>title</c> is written only for <c>2025-06-18</c>.</param>
    /// <returns>The response.</returns>
    internal static byte[] AnswerList(JsonRpcRequest request, McpServerRegistry registry, int pageSize, string version)
    {
        IReadOnlyList<McpRegisteredTool> tools = registry.RegisteredTools;
        if (!McpListCursor.TryReadOffset(request.Params, tools.Count, out int offset))
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.InvalidParams("cursor"));
        }

        int end = (int)Math.Min((long)offset + pageSize, tools.Count);
        return JsonRpcResponseWriter.WriteSuccess(request.Id, writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("tools");
            for (int index = offset; index < end; index++)
            {
                WriteTool(writer, tools[index], version);
            }

            writer.WriteEndArray();
            if (end < tools.Count)
            {
                writer.WriteString("nextCursor", McpListCursor.Encode(end));
            }

            writer.WriteEndObject();
        });
    }

    /// <summary>Answers <c>tools/call</c>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="registry">The tools served.</param>
    /// <param name="version">The protocol revision negotiated; <c>structuredContent</c> is written only for <c>2025-06-18</c>.</param>
    /// <param name="cancellationToken">The request's cancellation token, given to the handler.</param>
    /// <returns>The response, or <see langword="null" /> when the handler was cancelled through its token.</returns>
    internal static async Task<byte[]?> AnswerCallAsync(
        JsonRpcRequest request, McpServerRegistry registry, string version, CancellationToken cancellationToken)
    {
        if (ReadCall(request.Params, registry, out McpRegisteredTool? tool, out JsonElement arguments) is { } invalidParams)
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.InvalidParams(invalidParams));
        }

        McpToolResult result;
        try
        {
            result = await tool!.Tool.Handler(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (McpToolFailureException failure)
        {
            result = McpToolResult.FromFailure(failure.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
#pragma warning disable CA1031 // A handler's own failure must become -32603, never end the session (ADR-0103 decision 1).
        catch (Exception)
#pragma warning restore CA1031
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.InternalError);
        }

        return JsonRpcResponseWriter.WriteSuccess(request.Id, writer => WriteResult(writer, result, version));
    }

    // Returns what is wrong with the call's params, or null with the tool and its checked arguments.
    private static string? ReadCall(JsonElement? parameters, McpServerRegistry registry, out McpRegisteredTool? tool, out JsonElement arguments)
    {
        tool = null;
        arguments = EmptyArguments;
        if (!TryReadToolName(parameters, out JsonElement value, out string name))
        {
            return "name";
        }

        if (!registry.TryGetTool(name, out tool))
        {
            return $"unknown tool {name}";
        }

        if (value.TryGetProperty("arguments", out JsonElement given) && given.ValueKind != JsonValueKind.Null)
        {
            arguments = given;
        }

        return tool.InputSchema.Check(arguments, "arguments");
    }

    private static bool TryReadToolName(JsonElement? parameters, out JsonElement value, out string name)
    {
        value = default;
        name = string.Empty;
        if (parameters is not { ValueKind: JsonValueKind.Object } given
            || !given.TryGetProperty("name", out JsonElement nameElement)
            || nameElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = given;
        name = nameElement.GetString()!;
        return true;
    }

    private static void WriteTool(Utf8JsonWriter writer, McpRegisteredTool registered, string version)
    {
        McpTool tool = registered.Tool;
        writer.WriteStartObject();
        writer.WriteString("name", tool.Name);
        if (tool.Title is { } title && version == McpProtocolVersions.Latest)
        {
            writer.WriteString("title", title);
        }

        writer.WriteString("description", tool.Description);
        writer.WritePropertyName("inputSchema");
        registered.InputSchemaJson.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static void WriteResult(Utf8JsonWriter writer, McpToolResult result, string version)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("content");
        foreach (string text in result.TextContent)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", text);
            writer.WriteEndObject();
        }

        foreach (McpEmbeddedResource resource in result.EmbeddedResources)
        {
            WriteEmbeddedResource(writer, resource);
        }

        writer.WriteEndArray();
        if (result.StructuredContent is { } structuredContent && version == McpProtocolVersions.Latest)
        {
            writer.WritePropertyName("structuredContent");
            structuredContent.WriteTo(writer);
        }

        writer.WriteBoolean("isError", result.IsError);
        writer.WriteEndObject();
    }

    private static void WriteEmbeddedResource(Utf8JsonWriter writer, McpEmbeddedResource resource)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "resource");
        writer.WriteStartObject("resource");
        writer.WriteString("uri", resource.Uri);
        writer.WriteString("mimeType", resource.MimeType);
        writer.WriteBase64String("blob", resource.Blob.Span);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static JsonElement ParseEmptyObject()
    {
        using JsonDocument document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
