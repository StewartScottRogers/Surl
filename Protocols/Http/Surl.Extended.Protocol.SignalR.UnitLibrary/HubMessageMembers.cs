using System.Text.Json;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The members of one JSON hub-protocol record the server knows, each of the JSON kind HubProtocol.md
/// "JSON Encoding" gives it, before the record's <c>type</c> decides which it needs.
/// </summary>
internal sealed class HubMessageMembers
{
    /// <summary>
    /// Whether the record is one JSON object whose known members are each of their JSON kind, and
    /// whose IDs are no longer than <see cref="HubMessageJsonReader.MaxIdLength"/>.
    /// </summary>
    public bool IsWellFormed { get; private set; } = true;

    public int? Type { get; private set; }

    public string? InvocationId { get; private set; }

    public string? Target { get; private set; }

    public string? Error { get; private set; }

    public bool AllowReconnect { get; private set; }

    public IReadOnlyList<HubJsonValue>? Arguments { get; private set; }

    public IReadOnlyList<string>? StreamIds { get; private set; }

    public HubJsonValue? Item { get; private set; }

    public HubJsonValue? Result { get; private set; }

    /// <summary>
    /// Reads the members of <paramref name="record"/>.
    /// </summary>
    /// <param name="record">The record, without its <c>0x1E</c>.</param>
    /// <returns>The members read; <see cref="IsWellFormed"/> says whether they may be used.</returns>
    /// <exception cref="JsonException">The record is not well-formed JSON.</exception>
    public static HubMessageMembers Read(ReadOnlyMemory<byte> record)
    {
        var reader = new Utf8JsonReader(record.Span);
        var members = new HubMessageMembers();
        // A record with no JSON token throws here; Read returns false only at the end of the data.
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            members.IsWellFormed = false;

            return members;
        }

        reader.Read();
        while (members.IsWellFormed && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            reader.Read();
            members.ReadMember(name, ref reader, record);
            reader.Read();
        }

        if (members.IsWellFormed)
        {
            // Past the object's end only whitespace may follow; anything else throws.
            reader.Read();
        }

        return members;
    }

    private void ReadMember(string name, ref Utf8JsonReader reader, ReadOnlyMemory<byte> record)
    {
        switch (name)
        {
            case "type":
                Type = ReadInteger(ref reader);
                break;
            case "invocationId":
                InvocationId = ReadId(ref reader);
                break;
            case "target":
                Target = ReadString(ref reader);
                break;
            case "error":
                Error = ReadString(ref reader);
                break;
            case "allowReconnect":
                AllowReconnect = ReadBoolean(ref reader);
                break;
            default:
                ReadValueMember(name, ref reader, record);
                break;
        }
    }

    private void ReadValueMember(string name, ref Utf8JsonReader reader, ReadOnlyMemory<byte> record)
    {
        switch (name)
        {
            case "arguments":
                Arguments = ReadValues(ref reader, record);
                break;
            case "streamIds":
                StreamIds = ReadIds(ref reader);
                break;
            case "item":
                Item = ReadValue(ref reader, record);
                break;
            case "result":
                Result = ReadValue(ref reader, record);
                break;
            default:
                // headers, and every member the server does not know (ADR-0119, decision 3).
                reader.Skip();
                break;
        }
    }

    private int? ReadInteger(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value))
        {
            return value;
        }

        IsWellFormed = false;

        return null;
    }

    private string? ReadString(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        IsWellFormed = false;

        return null;
    }

    private string? ReadId(ref Utf8JsonReader reader)
    {
        var id = ReadString(ref reader);
        if (id is { Length: > HubMessageJsonReader.MaxIdLength })
        {
            IsWellFormed = false;

            return null;
        }

        return id;
    }

    private bool ReadBoolean(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
        {
            return reader.GetBoolean();
        }

        IsWellFormed = false;

        return false;
    }

    private List<string>? ReadIds(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            IsWellFormed = false;

            return null;
        }

        var ids = new List<string>();
        reader.Read();
        while (IsWellFormed && reader.TokenType != JsonTokenType.EndArray)
        {
            if (ReadId(ref reader) is { } id)
            {
                ids.Add(id);
                reader.Read();
            }
        }

        return ids;
    }

    private List<HubJsonValue>? ReadValues(ref Utf8JsonReader reader, ReadOnlyMemory<byte> record)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            IsWellFormed = false;

            return null;
        }

        var values = new List<HubJsonValue>();
        reader.Read();
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            values.Add(ReadValue(ref reader, record));
            reader.Read();
        }

        return values;
    }

    // The value at the reader, as the bytes it takes in the record.
    private static HubJsonValue ReadValue(ref Utf8JsonReader reader, ReadOnlyMemory<byte> record)
    {
        var start = (int)reader.TokenStartIndex;
        reader.Skip();

        return new HubJsonValue(record[start..(int)reader.BytesConsumed]);
    }
}
