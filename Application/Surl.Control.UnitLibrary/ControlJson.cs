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

    public string? RuleId { get; init; }

    public string? FaultKind { get; init; }

    public string? Stage { get; init; }

    public string? Target { get; init; }

    public string? HookId { get; init; }

    public string? Failure { get; init; }

    public string? Message { get; init; }
}

/// <summary>The answer to <c>GET /v1/events</c>.</summary>
internal sealed record ControlEventsJson(IReadOnlyList<ControlEventJson> Events, bool HasMore, long DroppedEventCount);

/// <summary>The answer to <c>GET /v1/events/wait</c>: the event, or <c>timed_out</c>.</summary>
internal sealed record ControlEventWaitJson(ControlEventJson? Event, bool? TimedOut);

/// <summary>One chunk of an exchange: the bytes that crossed the connection one way, as text or base64.</summary>
internal sealed record ControlExchangeChunkJson(string Direction, string Encoding, string Data);

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
[ExcludeFromCodeCoverage(Justification = "Its body is written by the System.Text.Json source generator, not by Surl; the shapes it serializes are pinned byte for byte by the endpoint's tests.")]
internal sealed partial class ControlJsonContext : JsonSerializerContext;
