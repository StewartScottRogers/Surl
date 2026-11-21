using System.Globalization;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// Writes an event as <c>get_events</c> and <c>wait_for_event</c> show it (ADR-0124 decisions 7 and 8, ADR-0171):
/// its <c>sequence</c>, <c>at</c> and <c>kind</c>, then the members the record writes through
/// <see cref="SurlEvent.WriteMembers"/> in snake_case, enum values in snake_case, bytes as hexadecimal bounded to
/// <see cref="MaxByteCount"/>, and a member that is <see langword="null"/> left out. A <see cref="LoginJudgedEvent"/>
/// carries no secret, so none is written.
/// </summary>
internal sealed class RecordedEventWriter : ISurlEventMemberWriter
{
    /// <summary>The most bytes of one bytes member that are written; the rest are left off.</summary>
    internal const int MaxByteCount = 256;

    private readonly Utf8JsonWriter _writer;
    private readonly string _kindPrefix;

    private RecordedEventWriter(Utf8JsonWriter writer, string kindName)
    {
        _writer = writer;
        _kindPrefix = kindName.Split('_')[0];
    }

    /// <summary>Writes <paramref name="surlEvent"/> as an object.</summary>
    /// <param name="writer">The writer, where a value may go.</param>
    /// <param name="surlEvent">An event the instance kept.</param>
    public static void WriteEvent(Utf8JsonWriter writer, SurlEvent surlEvent)
    {
        writer.WriteStartObject();
        writer.WriteNumber("sequence", surlEvent.Sequence);
        writer.WriteString("at", surlEvent.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("kind", surlEvent.KindName);
        surlEvent.WriteMembers(new RecordedEventWriter(writer, surlEvent.KindName));
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public void Text(string name, string? value)
    {
        if (value is not null)
        {
            _writer.WriteString(MemberName(name), value);
        }
    }

    /// <inheritdoc />
    public void TextList(string name, IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return;
        }

        _writer.WriteStartArray(MemberName(name));
        foreach (var value in values)
        {
            _writer.WriteStringValue(value);
        }

        _writer.WriteEndArray();
    }

    /// <inheritdoc />
    public void Number(string name, long? value)
    {
        if (value is { } number)
        {
            _writer.WriteNumber(MemberName(name), number);
        }
    }

    /// <inheritdoc />
    public void Flag(string name, bool value) => _writer.WriteBoolean(MemberName(name), value);

    /// <inheritdoc />
    public void Choice(string name, Enum value) => _writer.WriteString(MemberName(name), SnakeCase(value.ToString()));

    /// <inheritdoc />
    public void Bytes(string name, ReadOnlyMemory<byte> value) =>
        _writer.WriteString(MemberName(name), Convert.ToHexString(value.Span[..Math.Min(value.Length, MaxByteCount)]));

    internal static string SnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        SnakeCase(value.ToString());

    // A member named Kind would repeat the head's "kind", so it is written with the event's first word: fault_kind.
    private string MemberName(string name) =>
        SnakeCase(name) is "kind" ? _kindPrefix + "_kind" : SnakeCase(name);

    private static string SnakeCase(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
}
