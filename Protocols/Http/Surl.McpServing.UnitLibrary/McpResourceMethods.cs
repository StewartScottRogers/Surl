using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// Answers <c>resources/list</c> and <c>resources/read</c> (ADR-0103 decision 1, MCP 2025-06-18 "Server Features >
/// Resources"): the list paged with <c>cursor</c> and <c>nextCursor</c> as <c>tools/list</c> is, and a read answered
/// with one <c>text</c> entry in <c>contents</c>. Resource templates are not served.
/// </summary>
internal static class McpResourceMethods
{
    /// <summary>The method that lists the resources.</summary>
    internal const string ListMethod = "resources/list";

    /// <summary>The method that reads one resource.</summary>
    internal const string ReadMethod = "resources/read";

    /// <summary>Answers <c>resources/list</c>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="registry">The resources served.</param>
    /// <param name="pageSize">The most resources one page lists.</param>
    /// <param name="version">The protocol revision negotiated; <c>title</c> is written only for <c>2025-06-18</c>.</param>
    /// <returns>The response.</returns>
    internal static byte[] AnswerList(JsonRpcRequest request, McpServerRegistry registry, int pageSize, string version)
    {
        IReadOnlyList<McpRegisteredResource> resources = registry.RegisteredResources;
        if (!McpListCursor.TryReadOffset(request.Params, resources.Count, out int offset))
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.InvalidParams("cursor"));
        }

        int end = (int)Math.Min((long)offset + pageSize, resources.Count);
        return JsonRpcResponseWriter.WriteSuccess(request.Id, writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("resources");
            for (int index = offset; index < end; index++)
            {
                WriteResource(writer, resources[index].Resource, version);
            }

            writer.WriteEndArray();
            if (end < resources.Count)
            {
                writer.WriteString("nextCursor", McpListCursor.Encode(end));
            }

            writer.WriteEndObject();
        });
    }

    /// <summary>Answers <c>resources/read</c>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="registry">The resources served.</param>
    /// <param name="version">The protocol revision negotiated; <c>title</c> is written only for <c>2025-06-18</c>.</param>
    /// <param name="cancellationToken">The request's cancellation token, given to the provider.</param>
    /// <returns>The response, or <see langword="null" /> when the read was cancelled through its token.</returns>
    internal static async Task<byte[]?> AnswerReadAsync(
        JsonRpcRequest request, McpServerRegistry registry, string version, CancellationToken cancellationToken)
    {
        if (!TryReadUri(request.Params, out string uri))
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.InvalidParams("uri"));
        }

        if (!registry.TryGetResource(uri, out McpRegisteredResource? registered))
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.ResourceNotFound(uri));
        }

        string? text;
        try
        {
            text = await registered.Provider.ReadTextAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
#pragma warning disable CA1031 // A provider's own failure must become -32603, never end the session (ADR-0103 decision 1).
        catch (Exception)
#pragma warning restore CA1031
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.InternalError);
        }

        if (text is null)
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.ResourceNotFound(uri));
        }

        return JsonRpcResponseWriter.WriteSuccess(request.Id, writer => WriteContents(writer, registered.Resource, text, version));
    }

    private static bool TryReadUri(JsonElement? parameters, out string uri)
    {
        uri = string.Empty;
        if (parameters is not { ValueKind: JsonValueKind.Object } given
            || !given.TryGetProperty("uri", out JsonElement uriElement)
            || uriElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        uri = uriElement.GetString()!;
        return true;
    }

    private static void WriteResource(Utf8JsonWriter writer, McpResource resource, string version)
    {
        writer.WriteStartObject();
        WriteIdentity(writer, resource, version);
        if (resource.Description is { } description)
        {
            writer.WriteString("description", description);
        }

        WriteMimeType(writer, resource);
        writer.WriteEndObject();
    }

    // The "Reading Resources" example writes uri, name, title, mimeType and text, in that order.
    private static void WriteContents(Utf8JsonWriter writer, McpResource resource, string text, string version)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("contents");
        writer.WriteStartObject();
        WriteIdentity(writer, resource, version);
        WriteMimeType(writer, resource);
        writer.WriteString("text", text);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    // title is written only when 2025-06-18 was negotiated: the two older revisions do not define it (ADR-0103 decision 1).
    private static void WriteIdentity(Utf8JsonWriter writer, McpResource resource, string version)
    {
        writer.WriteString("uri", resource.Uri);
        writer.WriteString("name", resource.Name);
        if (resource.Title is { } title && version == McpProtocolVersions.Latest)
        {
            writer.WriteString("title", title);
        }
    }

    private static void WriteMimeType(Utf8JsonWriter writer, McpResource resource)
    {
        if (resource.MimeType is { } mimeType)
        {
            writer.WriteString("mimeType", mimeType);
        }
    }
}
