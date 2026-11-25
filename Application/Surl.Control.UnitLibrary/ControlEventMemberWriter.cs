using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// The <see cref="ISurlEventMemberWriter"/> that builds an event's control JSON (ADR-0171): each member an event
/// writes, named by its property name, lands on the <see cref="ControlEventJson"/> member of the same meaning.
/// A member the JSON has no place for throws, so a new event member cannot go unwritten without a test failing.
/// </summary>
internal sealed class ControlEventMemberWriter(ControlEventJson head) : ISurlEventMemberWriter
{
    private static readonly Dictionary<string, Func<ControlEventJson, string?, ControlEventJson>> TextMembers = new(StringComparer.Ordinal)
    {
        ["ListenUrl"] = (json, value) => json with { ListenUrl = value },
        ["Scheme"] = (json, value) => json with { Scheme = value },
        ["Client"] = (json, value) => json with { Client = value },
        ["Account"] = (json, value) => json with { Account = value },
        ["Mechanism"] = (json, value) => json with { Mechanism = value },
        ["Name"] = (json, value) => json with { Name = value },
        ["PreviousName"] = (json, value) => json with { PreviousName = value },
        ["RuleId"] = (json, value) => json with { RuleId = value },
        ["Target"] = (json, value) => json with { Target = value },
        ["HookId"] = (json, value) => json with { HookId = value },
        ["Message"] = (json, value) => json with { Message = value },
        ["Group"] = (json, value) => json with { Group = value },
        ["MemberId"] = (json, value) => json with { MemberId = value },
        ["Value"] = (json, value) => json with { Value = value },
    };

    private static readonly Dictionary<string, Func<ControlEventJson, IReadOnlyList<string>?, ControlEventJson>> TextListMembers = new(StringComparer.Ordinal)
    {
        ["DeliveredFor"] = (json, value) => json with { DeliveredFor = value },
    };

    private static readonly Dictionary<string, Func<ControlEventJson, long?, ControlEventJson>> NumberMembers = new(StringComparer.Ordinal)
    {
        ["ExchangeId"] = (json, value) => json with { ExchangeId = value },
        ["StreamId"] = (json, value) => json with { StreamId = value },
        ["ReceivedByteCount"] = (json, value) => json with { ReceivedByteCount = value },
        ["SentByteCount"] = (json, value) => json with { SentByteCount = value },
        ["ByteCount"] = (json, value) => json with { ByteCount = value },
        ["GenerationId"] = (json, value) => json with { GenerationId = (int?)value },
    };

    private static readonly Dictionary<string, Func<ControlEventJson, bool, ControlEventJson>> FlagMembers = new(StringComparer.Ordinal)
    {
        ["IsAccepted"] = (json, value) => json with { IsAccepted = value },
        ["IsRefusedByFault"] = (json, value) => json with { IsRefusedByFault = value },
    };

    private static readonly Dictionary<string, Func<ControlEventJson, string, ControlEventJson>> ChoiceMembers = new(StringComparer.Ordinal)
    {
        ["Direction"] = (json, value) => json with { Direction = value },
        ["Layer"] = (json, value) => json with { Layer = value },
        ["Store"] = (json, value) => json with { Store = value },
        ["Change"] = (json, value) => json with { Change = value },
        ["Kind"] = (json, value) => json with { FaultKind = value },
        ["Stage"] = (json, value) => json with { Stage = value },
        ["Failure"] = (json, value) => json with { Failure = value },
    };

    private ControlEventJson written = head;

    /// <summary>The JSON with every member written so far.</summary>
    public ControlEventJson Written => written;

    /// <inheritdoc />
    public void Text(string name, string? value) => written = Find(TextMembers, name)(written, value);

    /// <inheritdoc />
    public void TextList(string name, IReadOnlyList<string>? values) =>
        written = Find(TextListMembers, name)(written, values);

    /// <inheritdoc />
    public void Number(string name, long? value) => written = Find(NumberMembers, name)(written, value);

    /// <inheritdoc />
    public void Flag(string name, bool value) => written = Find(FlagMembers, name)(written, value);

    /// <inheritdoc />
    public void Choice(string name, Enum value) =>
        written = Find(ChoiceMembers, name)(written, ControlEventKinds.SnakeCaseOf(value));

    /// <inheritdoc />
    public void Bytes(string name, ReadOnlyMemory<byte> value)
    {
        _ = Find(BytesMembers, name);
        var shown = ControlShownBytes.Show(value.Span, ControlEventWriter.MaxShownBytesPerEvent);
        written = written with
        {
            Encoding = shown.Encoding,
            Data = shown.Data,
            ByteCount = value.Length,
            IsCut = shown.ShownLength < value.Length,
        };
    }

    private static readonly Dictionary<string, bool> BytesMembers = new(StringComparer.Ordinal) { ["Bytes"] = true };

    private static TWriter Find<TWriter>(Dictionary<string, TWriter> members, string name) =>
        members.TryGetValue(name, out var writer)
            ? writer
            : throw new InvalidOperationException($"The control JSON has no place for event member {name}.");
}
