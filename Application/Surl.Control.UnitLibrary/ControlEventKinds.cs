using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// The names the control endpoint gives event kinds: each record's type name without <c>Event</c>, in snake_case
/// (<c>exchange_started</c>, <c>store_changed</c>), and what each event says of its exchange and account for a
/// query to match. The events say these themselves (ADR-0171); nothing here switches on an event's type.
/// </summary>
internal static class ControlEventKinds
{
    /// <summary>
    /// Every kind a query may name. Listed explicitly because the library is native AOT and cannot enumerate types;
    /// a test enumerates the exported <see cref="SurlEvent"/> records and fails when one is missing (ADR-0171).
    /// </summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        NameOfType(typeof(ExchangeStartedEvent)),
        NameOfType(typeof(ExchangeBytesEvent)),
        NameOfType(typeof(ExchangeEndedEvent)),
        NameOfType(typeof(LoginJudgedEvent)),
        NameOfType(typeof(StoreChangedEvent)),
        NameOfType(typeof(FaultAppliedEvent)),
        NameOfType(typeof(HookFailedEvent)),
        NameOfType(typeof(ConsumerGroupChangedEvent)),
        NameOfType(typeof(CookieReceivedEvent)),
    };

    /// <summary>The kind a record type's events have, as <see cref="SurlEvent.KindName"/> spells it.</summary>
    /// <param name="eventType">A <see cref="SurlEvent"/> record type.</param>
    /// <returns>Its kind's name.</returns>
    public static string NameOfType(Type eventType) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(eventType.Name[..^"Event".Length]);

    /// <summary>The kind of <paramref name="surlEvent"/>.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>Its kind's name.</returns>
    public static string NameOf(SurlEvent surlEvent) => surlEvent.KindName;

    /// <summary>Snake case of an enum member, as the endpoint writes each enum value.</summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns><c>Received</c> as <c>received</c>, <c>AccountAdded</c> as <c>account_added</c>.</returns>
    public static string SnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        SnakeCaseOf(value);

    /// <summary>Snake case of an enum member given as <see cref="Enum"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The same text <see cref="SnakeCase{TEnum}"/> gives.</returns>
    public static string SnakeCaseOf(Enum value) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>The exchange an event belongs to, or <see langword="null"/> when it belongs to none.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>Its exchange ID.</returns>
    public static long? ExchangeIdOf(SurlEvent surlEvent) => surlEvent.ExchangeId;

    /// <summary>The account an event names, or <see langword="null"/>.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>The account name, or <see langword="null"/>.</returns>
    public static string? AccountOf(SurlEvent surlEvent) => surlEvent.Account;
}
