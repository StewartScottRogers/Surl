using System.Buffers;
using System.Text.Json;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>Builds a tool result's <c>structuredContent</c> object with <see cref="Utf8JsonWriter" />.</summary>
internal static class McpStructuredContent
{
    /// <summary>Writes one JSON object.</summary>
    /// <param name="writeMembers">Writes the object's members.</param>
    /// <returns>The object.</returns>
    internal static JsonElement Write(Action<Utf8JsonWriter> writeMembers)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
