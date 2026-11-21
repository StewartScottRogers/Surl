using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Surl.Control;

/// <summary>One event as the control endpoint writes it: sequence, time and kind, then the record's members.</summary>
internal sealed record ControlEventJson
{
    public required long Sequence { get; init; }

    public required string At { get; init; }

    public required string Kind { get; init; }

    public long? ExchangeId { get; init; }

    public string? ListenUrl { get; init; }

    public string? Scheme { get; init; }

    public string? Client { get; init; }

    public string? Direction { get; init; }

    public string? Layer { get; init; }

    public long? StreamId { get; init; }

    public string? Encoding { get; init; }

    public string? Data { get; init; }

    public long? ByteCount { get; init; }

    public bool? IsCut { get; init; }

    public long? ReceivedByteCount { get; init; }

    public long? SentByteCount { get; init; }

    public string? Account { get; init; }

    public string? Mechanism { get; init; }

    public bool? IsAccepted { get; init; }

    public bool? IsRefusedByFault { get; init; }

    public string? Store { get; init; }

    public string? Change { get; init; }

    public string? Name { get; init; }

    public string? PreviousName { get; init; }

    public IReadOnlyList<string>? DeliveredFor { get; init; }

    public string? RuleId { get; init; }

    public string? FaultKind { get; init; }

    public string? Stage { get; init; }

    public string? Target { get; init; }

    public string? HookId { get; init; }

    public string? Failure { get; init; }

    public string? Message { get; init; }

    public string? Group { get; init; }

    public string? MemberId { get; init; }

    public int? GenerationId { get; init; }
}

/// <summary>The answer to <c>GET /v1/events</c>.</summary>
internal sealed record ControlEventsJson(IReadOnlyList<ControlEventJson> Events, bool HasMore, long DroppedEventCount);

/// <summary>The answer to <c>GET /v1/events/wait</c>: the event, or <c>timed_out</c>.</summary>
internal sealed record ControlEventWaitJson(ControlEventJson? Event, bool? TimedOut);

/// <summary>One chunk of an exchange: the bytes that crossed the connection one way, as text or base64.</summary>
internal sealed record ControlExchangeChunkJson(string Direction, string Encoding, string Data, string Layer, long? StreamId);

/// <summary>One exchange, in ADR-0103's <c>get_exchanges</c> shape with its listen URL and scheme.</summary>
internal sealed record ControlExchangeJson(
    long ExchangeId,
    string ListenUrl,
    string Scheme,
    string Client,
    string StartedAt,
    IReadOnlyList<ControlExchangeChunkJson> Chunks,
    long RecordedBytes,
    long ShownBytes,
    bool IsCut,
    bool IsTruncated);

/// <summary>The answer to <c>GET /v1/exchanges</c>.</summary>
internal sealed record ControlExchangesJson(IReadOnlyList<ControlExchangeJson> Exchanges, long DroppedExchangeCount, bool HasMore);

/// <summary>The answer to <c>POST /v1/faults</c>.</summary>
internal sealed record ControlFaultIdJson(string FaultId);

/// <summary>One rule in the answer to <c>GET /v1/faults</c>; <c>remaining</c> is written even when null.</summary>
internal sealed record ControlFaultJson(
    string FaultId,
    string Item,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Remaining);

/// <summary>The answer to <c>GET /v1/faults</c>.</summary>
internal sealed record ControlFaultsJson(IReadOnlyList<ControlFaultJson> Faults);

/// <summary>The answer to <c>POST /v1/snapshots</c>.</summary>
internal sealed record ControlSnapshotIdJson(string SnapshotId);

/// <summary>The answer to <c>GET /v1/zones</c>: the apexes of the DNS zones held.</summary>
internal sealed record ControlZonesJson(IReadOnlyList<string> Zones);

/// <summary>One mailbox in the answer to <c>GET /v1/mail/{owner}</c>.</summary>
internal sealed record ControlMailboxJson(string Name, int MessageCount, int UnseenCount, uint UidValidity);

/// <summary>The answer to <c>GET /v1/mail/{owner}</c>.</summary>
internal sealed record ControlMailboxesJson(IReadOnlyList<ControlMailboxJson> Mailboxes);

/// <summary>One message in a listing, or read as JSON with its <c>text</c> or <c>base64</c>.</summary>
internal sealed record ControlMailMessageJson(uint Uid, long Size, IReadOnlyList<string> Flags, string InternalDate, string? Text = null, string? Base64 = null);

/// <summary>The answer to <c>GET /v1/mail/{owner}/{mailbox}</c>.</summary>
internal sealed record ControlMailMessagesJson(IReadOnlyList<ControlMailMessageJson> Messages, bool HasMore);

/// <summary>One child in a folder listing.</summary>
internal sealed record ControlFolderEntryJson(string Name, string Kind, long? ByteCount);

/// <summary>The answer to <c>GET /v1/files/{path}/</c>.</summary>
internal sealed record ControlFolderJson(IReadOnlyList<ControlFolderEntryJson> Entries, bool HasMore);

/// <summary>One header of a browsed queue message.</summary>
internal sealed record ControlHeaderJson(string Name, string Value);

/// <summary>One browsed queue message.</summary>
internal sealed record ControlQueuedMessageJson(int Position, string? Text, string? Base64, IReadOnlyList<ControlHeaderJson> Headers);

/// <summary>The answer to <c>GET /v1/queues/{name}/messages</c>.</summary>
internal sealed record ControlQueueJson(IReadOnlyList<ControlQueuedMessageJson> Messages, int ReadyCount, int InFlightCount, bool HasMore);

/// <summary>The answer to <c>GET /v1/topics/{name}/retained</c>.</summary>
internal sealed record ControlRetainedJson(string Topic, int Qos, string? Text, string? Base64);

/// <summary>One record of a topic log.</summary>
internal sealed record ControlLogRecordJson(long Offset, string Timestamp, string? Key, string? Text, string? Base64);

/// <summary>The answer to <c>GET /v1/topics/{name}/log</c>.</summary>
internal sealed record ControlLogJson(IReadOnlyList<ControlLogRecordJson> Records, long NextOffset, bool HasMore);

/// <summary>One key in a listing, or read with its <c>text</c> or <c>base64</c> value.</summary>
internal sealed record ControlKeyJson(string? Key, string? KeyBase64, string Type, long ByteCount, string? ExpiresAt, string? Text = null, string? Base64 = null);

/// <summary>The answer to <c>GET /v1/keys</c>.</summary>
internal sealed record ControlKeysJson(IReadOnlyList<ControlKeyJson> Keys, bool HasMore);

/// <summary>Every error answer.</summary>
internal sealed record ControlErrorJson(string Error, string Message);

/// <summary>The body of <c>POST /v1/messages</c>.</summary>
internal sealed record ControlMessageRequestJson
{
    public string? Topic { get; init; }

    public string? Queue { get; init; }

    public string? PayloadText { get; init; }

    public string? PayloadBase64 { get; init; }

    public bool Retain { get; init; }
}

/// <summary>The body of <c>POST /v1/accounts</c>.</summary>
internal sealed record ControlAccountRequestJson
{
    public string? User { get; init; }

    public string? Password { get; init; }
}

/// <summary>The body of <c>PUT /v1/accounts/{user}</c> and its proxy and TLS-SRP twins.</summary>
internal sealed record ControlPasswordRequestJson
{
    public string? Password { get; init; }
}

/// <summary>
/// The control endpoint's JSON, by source generation so nothing is read by reflection: snake_case names, null
/// members left out, and a request member the endpoint does not know refused.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ControlEventsJson))]
[JsonSerializable(typeof(ControlEventWaitJson))]
[JsonSerializable(typeof(ControlExchangesJson))]
[JsonSerializable(typeof(ControlFaultIdJson))]
[JsonSerializable(typeof(ControlFaultsJson))]
[JsonSerializable(typeof(ControlSnapshotIdJson))]
[JsonSerializable(typeof(ControlErrorJson))]
[JsonSerializable(typeof(ControlMessageRequestJson))]
[JsonSerializable(typeof(ControlAccountRequestJson))]
[JsonSerializable(typeof(ControlPasswordRequestJson))]
[JsonSerializable(typeof(ControlMailboxesJson))]
[JsonSerializable(typeof(ControlMailMessagesJson))]
[JsonSerializable(typeof(ControlMailMessageJson))]
[JsonSerializable(typeof(ControlFolderJson))]
[JsonSerializable(typeof(ControlQueueJson))]
[JsonSerializable(typeof(ControlRetainedJson))]
[JsonSerializable(typeof(ControlLogJson))]
[JsonSerializable(typeof(ControlKeysJson))]
[JsonSerializable(typeof(ControlKeyJson))]
[JsonSerializable(typeof(ControlZonesJson))]
[ExcludeFromCodeCoverage(Justification = "Its body is written by the System.Text.Json source generator, not by Surl; the shapes it serializes are pinned byte for byte by the endpoint's tests.")]
internal sealed partial class ControlJsonContext : JsonSerializerContext;
