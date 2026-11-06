using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// The names the control endpoint gives event kinds: each record's type name without <c>Event</c>, in snake_case
/// (<c>exchange_started</c>, <c>store_changed</c>), and what each event says of its exchange, scheme, listen URL
/// and account for a query to match.
/// </summary>
internal static class ControlEventKinds
{
    /// <summary>Every kind a query may name.</summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        NameOf(typeof(ExchangeStartedEvent)),
        NameOf(typeof(ExchangeBytesEvent)),
        NameOf(typeof(ExchangeEndedEvent)),
        NameOf(typeof(LoginJudgedEvent)),
        NameOf(typeof(StoreChangedEvent)),
        NameOf(typeof(FaultAppliedEvent)),
        NameOf(typeof(HookFailedEvent)),
    };

    /// <summary>The kind of <paramref name="surlEvent"/>.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>Its kind's name.</returns>
    public static string NameOf(SurlEvent surlEvent) => NameOf(surlEvent.GetType());

    /// <summary>Snake case of an enum member, as the endpoint writes each enum value.</summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns><c>Received</c> as <c>received</c>, <c>AccountAdded</c> as <c>account_added</c>.</returns>
    public static string SnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>The exchange an event belongs to, or <see langword="null"/> when it belongs to none.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>Its exchange ID.</returns>
    public static long? ExchangeIdOf(SurlEvent surlEvent) => surlEvent switch
    {
        ExchangeStartedEvent started => started.ExchangeId,
        ExchangeBytesEvent bytes => bytes.ExchangeId,
        ExchangeEndedEvent ended => ended.ExchangeId,
        _ => ExchangeIdOfAnotherKind(surlEvent),
    };

    private static long? ExchangeIdOfAnotherKind(SurlEvent surlEvent) => surlEvent switch
    {
        LoginJudgedEvent login => login.ExchangeId,
        StoreChangedEvent change => change.ExchangeId,
        FaultAppliedEvent fault => fault.ExchangeId,
        _ => null,
    };

    /// <summary>
    /// The account an event names: a judged login's account, or the account an account-store change touched.
    /// </summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>The account name, or <see langword="null"/>.</returns>
    public static string? AccountOf(SurlEvent surlEvent) => surlEvent switch
    {
        LoginJudgedEvent login => login.Account,
        StoreChangedEvent { Store: StoreKind.Accounts } change => change.Name,
        _ => null,
    };

    private static string NameOf(Type eventType) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(eventType.Name[..^"Event".Length]);
}
