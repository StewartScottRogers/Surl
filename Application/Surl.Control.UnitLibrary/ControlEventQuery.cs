using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// Which events <c>GET /v1/events</c> and <c>GET /v1/events/wait</c> ask for: those after a sequence number, of a
/// kind, and of a scheme, listen URL or account. An absent attribute matches anything.
/// </summary>
/// <param name="After">Only events whose sequence number is larger.</param>
/// <param name="Kind">The event kind, such as <c>store_changed</c>, or <see langword="null"/>.</param>
/// <param name="Scheme">
/// The scheme, matched against an exchange start, a judged login, or the exchange another event belongs to.
/// </param>
/// <param name="ListenUrl">The listen URL, matched against an exchange start or the exchange an event belongs to.</param>
/// <param name="Account">The account, matched against a judged login or an account-store change.</param>
internal sealed record ControlEventQuery(long After, string? Kind, string? Scheme, string? ListenUrl, string? Account)
{
    /// <summary>Whether <paramref name="surlEvent"/> is one this query asks for.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <param name="exchanges">The held exchange starts, by exchange ID.</param>
    /// <returns><see langword="true"/> when every given attribute matches.</returns>
    public bool Matches(SurlEvent surlEvent, IReadOnlyDictionary<long, ExchangeStartedEvent> exchanges) =>
        surlEvent.Sequence > After
        && MatchesKind(surlEvent)
        && MatchesScheme(surlEvent, exchanges)
        && MatchesListenUrl(surlEvent, exchanges)
        && MatchesAccount(surlEvent);

    private bool MatchesKind(SurlEvent surlEvent) => Kind is null || Kind == ControlEventKinds.NameOf(surlEvent);

    private bool MatchesScheme(SurlEvent surlEvent, IReadOnlyDictionary<long, ExchangeStartedEvent> exchanges) =>
        Scheme is null || string.Equals(Scheme, SchemeOf(surlEvent, exchanges), StringComparison.OrdinalIgnoreCase);

    private bool MatchesListenUrl(SurlEvent surlEvent, IReadOnlyDictionary<long, ExchangeStartedEvent> exchanges) =>
        ListenUrl is null || ListenUrl == ExchangeOf(surlEvent, exchanges)?.ListenUrl;

    private bool MatchesAccount(SurlEvent surlEvent) => Account is null || Account == ControlEventKinds.AccountOf(surlEvent);

    private static string? SchemeOf(SurlEvent surlEvent, IReadOnlyDictionary<long, ExchangeStartedEvent> exchanges) =>
        surlEvent is LoginJudgedEvent login ? login.Scheme : ExchangeOf(surlEvent, exchanges)?.Scheme;

    private static ExchangeStartedEvent? ExchangeOf(SurlEvent surlEvent, IReadOnlyDictionary<long, ExchangeStartedEvent> exchanges) =>
        ControlEventKinds.ExchangeIdOf(surlEvent) is { } exchangeId ? exchanges.GetValueOrDefault(exchangeId) : null;
}
