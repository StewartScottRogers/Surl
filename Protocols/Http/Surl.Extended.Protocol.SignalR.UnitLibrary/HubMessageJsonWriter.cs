using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Writes a <see cref="HubMessage"/> as one record of the JSON hub protocol: a JSON object with
/// no whitespace, then <c>0x1E</c> (HubProtocol.md "JSON Encoding"; ADR-0119, decision 3).
/// </summary>
/// <remarks>
/// Members are written in the order HubProtocol.md's examples give them: <c>type</c>,
/// <c>invocationId</c>, <c>target</c>, <c>arguments</c>, <c>streamIds</c>, <c>item</c>,
/// <c>result</c>, <c>error</c>, <c>allowReconnect</c>; a member a message does not have is left
/// out. Values read from a client are written back as the bytes it sent.
/// </remarks>
public static class HubMessageJsonWriter
{
    /// <summary>
    /// The byte that ends every record: ASCII record separator.
    /// </summary>
    public const byte RecordSeparator = 0x1E;

    // Only what JSON requires is escaped, so a text such as "didn't" is written as it reads.
    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Writes <paramref name="message"/> and its record separator.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The record's bytes.</returns>
    public static byte[] Write(HubMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return WriteRecord(writer => WriteMembers(writer, message));
    }

    /// <summary>
    /// Writes one record holding the JSON object <paramref name="writeMembers"/> fills.
    /// </summary>
    /// <param name="writeMembers">Writes the object's members.</param>
    /// <returns>The record's bytes, its record separator included.</returns>
    internal static byte[] WriteRecord(Action<Utf8JsonWriter> writeMembers)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Options))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }

        buffer.Write([RecordSeparator]);

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteMembers(Utf8JsonWriter writer, HubMessage message)
    {
        writer.WriteNumber("type", (int)message.Type);
        switch (message)
        {
            case HubInvocation invocation:
                WriteInvocation(writer, invocation);
                break;
            case HubStreamItem streamItem:
                writer.WriteString("invocationId", streamItem.InvocationId);
                WriteValue(writer, "item", streamItem.Item);
                break;
            case HubCompletion completion:
                WriteCompletion(writer, completion);
                break;
            case HubCancelInvocation cancel:
                writer.WriteString("invocationId", cancel.InvocationId);
                break;
            case HubClose close:
                WriteClose(writer, close);
                break;
        }
    }

    private static void WriteInvocation(Utf8JsonWriter writer, HubInvocation invocation)
    {
        WriteOptionalString(writer, "invocationId", invocation.InvocationId);
        writer.WriteString("target", invocation.Target);
        writer.WriteStartArray("arguments");
        foreach (var argument in invocation.Arguments)
        {
            writer.WriteRawValue(argument.Utf8Json.Span);
        }

        writer.WriteEndArray();
        if (invocation.StreamIds.Count > 0)
        {
            writer.WriteStartArray("streamIds");
            foreach (var streamId in invocation.StreamIds)
            {
                writer.WriteStringValue(streamId);
            }

            writer.WriteEndArray();
        }
    }

    private static void WriteCompletion(Utf8JsonWriter writer, HubCompletion completion)
    {
        writer.WriteString("invocationId", completion.InvocationId);
        if (completion.Result is { } result)
        {
            WriteValue(writer, "result", result);
        }

        WriteOptionalString(writer, "error", completion.Error);
    }

    private static void WriteClose(Utf8JsonWriter writer, HubClose close)
    {
        WriteOptionalString(writer, "error", close.Error);
        if (close.AllowReconnect)
        {
            writer.WriteBoolean("allowReconnect", true);
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, string name, HubJsonValue value)
    {
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.Utf8Json.Span);
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}
