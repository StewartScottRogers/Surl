using System.Buffers;
using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// Writes JSON-RPC 2.0 responses as UTF-8 JSON on one line with no line terminator; the transport adds it.
/// <see cref="Utf8JsonWriter" /> escapes every control character in a string, so a response never holds a raw newline.
/// </summary>
public static class JsonRpcResponseWriter
{
    /// <summary>Writes a success response.</summary>
    /// <param name="id">The id of the request answered.</param>
    /// <param name="result">The method's result.</param>
    /// <returns>The response's UTF-8 JSON.</returns>
    public static byte[] WriteSuccess(JsonRpcRequestId id, JsonElement result) =>
        Write(id, writer =>
        {
            writer.WritePropertyName("result");
            result.WriteTo(writer);
        });

    /// <summary>Writes an error response.</summary>
    /// <param name="id">The id of the request answered, or <see langword="null" /> to write JSON null.</param>
    /// <param name="error">The error.</param>
    /// <returns>The response's UTF-8 JSON.</returns>
    public static byte[] WriteError(JsonRpcRequestId? id, JsonRpcError error) =>
        Write(id, writer =>
        {
            writer.WriteStartObject("error");
            writer.WriteNumber("code", error.Code);
            writer.WriteString("message", error.Message);
            writer.WriteEndObject();
        });

    private static byte[] Write(JsonRpcRequestId? id, Action<Utf8JsonWriter> writeOutcome)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            if (id is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                id.Value.WriteTo(writer);
            }

            writeOutcome(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
