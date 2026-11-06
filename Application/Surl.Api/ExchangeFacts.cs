using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// What a <see cref="SurlEventQuery"/> asks of one recorded exchange: where and when it started, and the
/// accounts judged, event kinds seen and stores changed in it so far.
/// </summary>
/// <param name="started">The event that opened the exchange.</param>
internal sealed class ExchangeFacts(ExchangeStartedEvent started)
{
    private readonly HashSet<string> accounts = new(StringComparer.Ordinal);
    private readonly HashSet<SurlEventKind> kinds = [SurlEventKind.ExchangeStarted];
    private readonly HashSet<StoreKind> stores = [];

    /// <summary>The exchange's ID.</summary>
    public long ExchangeId => started.ExchangeId;

    /// <summary>The listen URL that accepted it.</summary>
    public Uri ListenUrl { get; } = new(started.ListenUrl);

    /// <summary>The listen URL's scheme.</summary>
    public string Scheme => started.Scheme;

    /// <summary>The event that opened the exchange.</summary>
    public ExchangeStartedEvent Started => started;

    /// <summary>Whether a login for <paramref name="account"/> was judged in the exchange.</summary>
    /// <param name="account">The account.</param>
    /// <returns><see langword="true"/> when one was.</returns>
    public bool HasJudgedLoginFor(string account) => accounts.Contains(account);

    /// <summary>Notes what <paramref name="surlEvent"/>, an event of this exchange, says about it.</summary>
    /// <param name="surlEvent">The event.</param>
    public void Note(SurlEvent surlEvent)
    {
        if (SurlEventFacts.KindOf(surlEvent) is { } kind)
        {
            kinds.Add(kind);
        }

        switch (surlEvent)
        {
            case LoginJudgedEvent login:
                accounts.Add(login.Account);
                break;
            case StoreChangedEvent change:
                stores.Add(change.Store);
                break;
        }
    }

    /// <summary>Whether the exchange is one <paramref name="query"/> asks for.</summary>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when every given property matches.</returns>
    public bool Matches(SurlEventQuery query) =>
        started.Sequence > query.AfterSequence
        && SurlEventFacts.IsWithin(started.At, query)
        && IsOfSchemeListenUrlAndAccount(query)
        && HasKindAndStore(query);

    private bool IsOfSchemeListenUrlAndAccount(SurlEventQuery query) =>
        SurlEventFacts.IsScheme(Scheme, query)
        && (query.ListenUrl is null || query.ListenUrl == ListenUrl)
        && (query.Account is null || HasJudgedLoginFor(query.Account));

    private bool HasKindAndStore(SurlEventQuery query) =>
        (query.Kind is not { } kind || kinds.Contains(kind))
        && (query.Store is not { } store || stores.Contains(store));
}
