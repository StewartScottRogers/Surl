using System.Text.Json;
using System.Text.Unicode;

namespace Surl.McpServing;

/// <summary>
/// Reads one message from the client as JSON-RPC 2.0, by ADR-0103 decision 1's rules. Never throws on
/// malformed input: what is not a valid message becomes a <see cref="JsonRpcInvalidMessage" />.
/// </summary>
public static class JsonRpcMessageParser
{
    /// <summary>Reads one message.</summary>
    /// <param name="message">The message's UTF-8 bytes, without its line terminator.</param>
    /// <returns>
    /// A <see cref="JsonRpcRequest" />, a <see cref="JsonRpcNotification" />, a <see cref="JsonRpcClientResponse" />,
    /// or a <see cref="JsonRpcInvalidMessage" />: <see cref="JsonRpcErrorCodes.ParseError" /> for input that is
    /// not UTF-8, not JSON or nested deeper than 64 levels; <see cref="JsonRpcErrorCodes.InvalidRequest" /> for
    /// JSON that is not an object (a batch included), an <c>id</c> that is not a string or number, or a bad
    /// <c>jsonrpc</c>, <c>method</c> or <c>params</c>, echoing the id when one was read.
    /// </returns>
    public static JsonRpcIncoming Parse(ReadOnlyMemory<byte> message)
    {
        // JsonDocument checks a string's UTF-8 only when the string is read, so the whole message is checked first.
        if (!Utf8.IsValid(message.Span))
        {
            return new JsonRpcInvalidMessage(JsonRpcError.ParseError, null);
        }

        JsonDocument document;
        try
        {
            // The default options allow a depth of 64 and refuse comments and trailing commas.
            document = JsonDocument.Parse(message);
        }
        catch (JsonException)
        {
            return new JsonRpcInvalidMessage(JsonRpcError.ParseError, null);
        }

        using (document)
        {
            return ReadMessage(document.RootElement);
        }
    }

    private static JsonRpcIncoming ReadMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new JsonRpcInvalidMessage(JsonRpcError.InvalidRequest, null);
        }

        if (IsClientResponse(root))
        {
            return JsonRpcClientResponse.Instance;
        }

        if (!root.TryGetProperty("id", out JsonElement idElement))
        {
            return ReadCall(root, null);
        }

        return idElement.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? ReadCall(root, new JsonRpcRequestId(idElement.Clone()))
            : new JsonRpcInvalidMessage(JsonRpcError.InvalidRequest, null);
    }

    private static bool IsClientResponse(JsonElement root) =>
        !root.TryGetProperty("method", out _)
        && (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _));

    private static JsonRpcIncoming ReadCall(JsonElement root, JsonRpcRequestId? id)
    {
        if (!HasVersion(root) || !TryReadMethod(root, out string method) || !TryReadParams(root, out JsonElement? parameters))
        {
            return new JsonRpcInvalidMessage(JsonRpcError.InvalidRequest, id);
        }

        return id is null ? new JsonRpcNotification(method, parameters) : new JsonRpcRequest(id, method, parameters);
    }

    private static bool HasVersion(JsonElement root) =>
        root.TryGetProperty("jsonrpc", out JsonElement version)
        && version.ValueKind == JsonValueKind.String
        && version.ValueEquals("2.0");

    private static bool TryReadMethod(JsonElement root, out string method)
    {
        method = string.Empty;
        if (!root.TryGetProperty("method", out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        method = element.GetString()!;
        return true;
    }

    private static bool TryReadParams(JsonElement root, out JsonElement? parameters)
    {
        parameters = null;
        if (!root.TryGetProperty("params", out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return false;
        }

        parameters = element.Clone();
        return true;
    }
}
