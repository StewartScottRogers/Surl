using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// What a <see cref="SurlEventQuery"/> reads off one kept event: its kind, its exchange, its scheme, listen URL
/// and account, and whether it matches.
/// </summary>
internal static class SurlEventFacts
{
    /// <summary>The kind of <paramref name="surlEvent"/>.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>Its kind, or <see langword="null"/> for exchange bytes and any event kind not kept.</returns>
    public static SurlEventKind? KindOf(SurlEvent surlEvent) => surlEvent switch
    {
        ExchangeStartedEvent => SurlEventKind.ExchangeStarted,
        ExchangeEndedEvent => SurlEventKind.ExchangeEnded,
        LoginJudgedEvent => SurlEventKind.LoginJudged,
        _ => KindOfAnEventNotOfAnExchange(surlEvent),
    };

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

    /// <summary>Whether <paramref name="at"/> lies in the query's window, <c>Since</c> inclusive, <c>Until</c> exclusive.</summary>
    /// <param name="at">A time.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool IsWithin(DateTimeOffset at, SurlEventQuery query) =>
        (query.Since is not { } since || at >= since) && (query.Until is not { } until || at < until);

    /// <summary>Whether <paramref name="scheme"/> is the query's, case-insensitive; any scheme when it names none.</summary>
    /// <param name="scheme">A scheme, or <see langword="null"/> when unknown.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public static bool IsScheme(string? scheme, SurlEventQuery query) =>
        query.Scheme is null || string.Equals(query.Scheme, scheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="surlEvent"/> is one <paramref name="query"/> asks for.</summary>
    /// <param name="surlEvent">A kept event.</param>
    /// <param name="exchange">The recorded exchange it belongs to, or <see langword="null"/> when none is held.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when every given property matches.</returns>
    public static bool Matches(SurlEvent surlEvent, ExchangeFacts? exchange, SurlEventQuery query) =>
        surlEvent.Sequence > query.AfterSequence
        && IsWithin(surlEvent.At, query)
        && IsOfKindAndStore(surlEvent, query)
        && IsOfSchemeAndListenUrl(surlEvent, exchange, query)
        && (query.Account is null || NamesAccount(surlEvent, exchange, query.Account));

    private static bool IsOfKindAndStore(SurlEvent surlEvent, SurlEventQuery query) =>
        (query.Kind is null || KindOf(surlEvent) == query.Kind)
        && (query.Store is null || (surlEvent is StoreChangedEvent change && change.Store == query.Store));

    private static bool IsOfSchemeAndListenUrl(SurlEvent surlEvent, ExchangeFacts? exchange, SurlEventQuery query) =>
        IsScheme(OwnSchemeOf(surlEvent) ?? exchange?.Scheme, query)
        && (query.ListenUrl is null || query.ListenUrl == exchange?.ListenUrl);

    private static string? OwnSchemeOf(SurlEvent surlEvent) => surlEvent switch
    {
        LoginJudgedEvent login => login.Scheme,
        ConsumerGroupChangedEvent group => group.Scheme,
        _ => null,
    };

    private static SurlEventKind? KindOfAnEventNotOfAnExchange(SurlEvent surlEvent) => surlEvent switch
    {
        StoreChangedEvent => SurlEventKind.StoreChanged,
        FaultAppliedEvent => SurlEventKind.FaultApplied,
        HookFailedEvent => SurlEventKind.HookFailed,
        ConsumerGroupChangedEvent => SurlEventKind.ConsumerGroupChanged,
        _ => null,
    };

    private static long? ExchangeIdOfAnotherKind(SurlEvent surlEvent) => surlEvent switch
    {
        LoginJudgedEvent login => login.ExchangeId,
        StoreChangedEvent change => change.ExchangeId,
        FaultAppliedEvent fault => fault.ExchangeId,
        ConsumerGroupChangedEvent group => group.ExchangeId,
        _ => null,
    };

    private static bool NamesAccount(SurlEvent surlEvent, ExchangeFacts? exchange, string account) =>
        surlEvent switch
        {
            LoginJudgedEvent login => login.Account == account,
            ConsumerGroupChangedEvent group => group.Account == account,
            StoreChangedEvent { Store: StoreKind.Accounts } change when change.Name == account => true,
            _ => exchange?.HasJudgedLoginFor(account) == true,
        };
}
