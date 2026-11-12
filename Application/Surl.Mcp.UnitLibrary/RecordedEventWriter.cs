using System.Globalization;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// Writes an event as <c>get_events</c> and <c>wait_for_event</c> show it (ADR-0124 decisions 7 and 8): its
/// <c>sequence</c>, <c>at</c> and <c>kind</c>, then the record's members in snake_case, enum values in snake_case,
/// and a member that is <see langword="null"/> left out. A <see cref="LoginJudgedEvent"/> carries no secret, so none
/// is written.
/// </summary>
internal static class RecordedEventWriter
{
    /// <summary>The kind of <paramref name="surlEvent"/>: its record's type name without <c>Event</c>, in snake_case (<c>exchange_started</c>).</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>The kind's name.</returns>
    public static string KindOf(SurlEvent surlEvent) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(surlEvent.GetType().Name[..^"Event".Length]);

    /// <summary>Writes <paramref name="surlEvent"/> as an object.</summary>
    /// <param name="writer">The writer, where a value may go.</param>
    /// <param name="surlEvent">An event the instance kept: any kind but exchange bytes.</param>
    public static void WriteEvent(Utf8JsonWriter writer, SurlEvent surlEvent)
    {
        writer.WriteStartObject();
        writer.WriteNumber("sequence", surlEvent.Sequence);
        writer.WriteString("at", surlEvent.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("kind", KindOf(surlEvent));
        WriteExchangeId(writer, ExchangeIdOf(surlEvent));
        WriteMembers(writer, surlEvent);
        writer.WriteEndObject();
    }

    private static long? ExchangeIdOf(SurlEvent surlEvent) => surlEvent switch
    {
        ExchangeStartedEvent started => started.ExchangeId,
        ExchangeEndedEvent ended => ended.ExchangeId,
        LoginJudgedEvent login => login.ExchangeId,
        StoreChangedEvent change => change.ExchangeId,
        FaultAppliedEvent fault => fault.ExchangeId,
        ConsumerGroupChangedEvent group => group.ExchangeId,
        _ => null,
    };

    private static void WriteExchangeId(Utf8JsonWriter writer, long? exchangeId)
    {
        if (exchangeId is { } id)
        {
            writer.WriteNumber("exchange_id", id);
        }
    }

    private static void WriteMembers(Utf8JsonWriter writer, SurlEvent surlEvent)
    {
        switch (surlEvent)
        {
            case ExchangeStartedEvent started:
                writer.WriteString("listen_url", started.ListenUrl);
                writer.WriteString("scheme", started.Scheme);
                writer.WriteString("client", started.Client.ToString());
                break;
            case ExchangeEndedEvent ended:
                writer.WriteNumber("received_byte_count", ended.ReceivedByteCount);
                writer.WriteNumber("sent_byte_count", ended.SentByteCount);
                break;
            case LoginJudgedEvent login:
                WriteLogin(writer, login);
                break;
            case StoreChangedEvent change:
                WriteStoreChange(writer, change);
                break;
            default:
                WriteOtherMembers(writer, surlEvent);
                break;
        }
    }

    private static void WriteOtherMembers(Utf8JsonWriter writer, SurlEvent surlEvent)
    {
        if (surlEvent is FaultAppliedEvent fault)
        {
            writer.WriteString("rule_id", fault.RuleId);
            writer.WriteString("fault_kind", SnakeCase(fault.Kind));
            writer.WriteString("stage", SnakeCase(fault.Stage));
            writer.WriteString("target", fault.Target);
        }
        else if (surlEvent is HookFailedEvent hook)
        {
            writer.WriteString("hook_id", hook.HookId);
            writer.WriteString("failure", SnakeCase(hook.Failure));
            writer.WriteString("message", hook.Message);
        }
        else if (surlEvent is ConsumerGroupChangedEvent group)
        {
            WriteConsumerGroupChange(writer, group);
        }
    }

    private static void WriteConsumerGroupChange(Utf8JsonWriter writer, ConsumerGroupChangedEvent group)
    {
        writer.WriteString("scheme", group.Scheme);
        writer.WriteString("group", group.Group);
        writer.WriteString("change", SnakeCase(group.Change));
        if (group.MemberId is not null)
        {
            writer.WriteString("member_id", group.MemberId);
        }

        writer.WriteNumber("generation_id", group.GenerationId);
        if (group.Account is not null)
        {
            writer.WriteString("account", group.Account);
        }
    }

    private static void WriteLogin(Utf8JsonWriter writer, LoginJudgedEvent login)
    {
        writer.WriteString("scheme", login.Scheme);
        writer.WriteString("account", login.Account);
        writer.WriteString("mechanism", login.Mechanism);
        writer.WriteBoolean("is_accepted", login.IsAccepted);
        writer.WriteBoolean("is_refused_by_fault", login.IsRefusedByFault);
    }

    private static void WriteStoreChange(Utf8JsonWriter writer, StoreChangedEvent change)
    {
        writer.WriteString("store", SnakeCase(change.Store));
        writer.WriteString("change", SnakeCase(change.Change));
        writer.WriteString("name", change.Name);
        if (change.PreviousName is not null)
        {
            writer.WriteString("previous_name", change.PreviousName);
        }

        if (change.ByteCount is { } byteCount)
        {
            writer.WriteNumber("byte_count", byteCount);
        }
    }

    private static string SnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
