using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Which exchanges and events <see cref="SurlServer.FindExchanges"/>, <see cref="SurlServer.FindEvents"/>,
/// <see cref="SurlServer.WaitForExchangeAsync"/> and <see cref="SurlServer.WaitForEventAsync"/> ask for
/// (ADR-0124 decision 1, ADR-0143). Every property left at its default matches anything; every property
/// given must match.
/// </summary>
/// <example>
/// <code>
/// // The mail for bob delivered over SMTP within 5 seconds.
/// var delivered = await server.WaitForEventAsync(
///     new SurlEventQuery { Scheme = "smtp", Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
///     TimeSpan.FromSeconds(5));
/// </code>
/// </example>
public sealed record SurlEventQuery
{
    /// <summary>
    /// The scheme, case-insensitive (<c>smtp</c>): an exchange's listen URL's scheme; for an event, a judged
    /// login's own scheme or else the scheme of the exchange it belongs to.
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// The listen URL as <see cref="SurlServer.BoundUrls"/> lists it, bound port included: an exchange's, or
    /// that of the exchange an event belongs to.
    /// </summary>
    public Uri? ListenUrl { get; init; }

    /// <summary>
    /// The account: an exchange in which a login for it was judged, accepted or refused; an event that names
    /// it (a judged login, an account-store change) or that belongs to such an exchange. Case-sensitive.
    /// </summary>
    public string? Account { get; init; }

    /// <summary>The event's kind; for an exchange, one of its events is of this kind.</summary>
    public SurlEventKind? Kind { get; init; }

    /// <summary>The store a <see cref="StoreChangedEvent"/> changed; for an exchange, it changed this store.</summary>
    public StoreKind? Store { get; init; }

    /// <summary>The earliest time, inclusive: an event's <see cref="SurlEvent.At"/>, an exchange's start.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>The latest time, exclusive: an event's <see cref="SurlEvent.At"/>, an exchange's start.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>
    /// Only what came after this sequence number: an event's <see cref="SurlEvent.Sequence"/>, the sequence
    /// number of an exchange's <see cref="ExchangeStartedEvent"/>. 0, the default, matches everything.
    /// </summary>
    public long AfterSequence { get; init; }
}
