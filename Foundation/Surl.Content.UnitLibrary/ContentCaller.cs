using Surl.Protocol.Abstractions;

namespace Surl.Content;

/// <summary>
/// Who asks the content store for a path: the logged-in account, or an anonymous caller, and the
/// scheme of the server that asks (ADR-0102 decision 2.2).
/// </summary>
/// <remarks>
/// Anonymous callers are TFTP and Gopher (no login), an object-storage request with no signature,
/// and every login <c>--allow-anonymous</c> accepted unchecked. A share with
/// <see cref="ContentShare.Grants"/> reaches an anonymous caller only through
/// <see cref="ContentShare.EveryoneMember"/>.
/// </remarks>
public sealed class ContentCaller
{
    private ContentCaller(string? accountName, string? scheme, long? exchangeId = null, string? listenUrl = null)
    {
        AccountName = accountName;
        Scheme = scheme;
        ExchangeId = exchangeId;
        ListenUrl = listenUrl;
    }

    /// <summary>
    /// The exchange that makes the call, so a store change and a fault point name it; <see langword="null"/>
    /// when the caller was not built for one (BL-872).
    /// </summary>
    public long? ExchangeId { get; }

    /// <summary>
    /// The ID of the out-of-space fault rule that refused this caller's upload or session commit;
    /// <see langword="null"/> until a refusal, and always for a caller not built for an exchange
    /// (ADR-0129 amendment 1). The store sets it before it answers <see cref="ContentUploadResult.TooLarge"/>.
    /// </summary>
    public string? RefusedByFaultRuleId { get; private set; }

    /// <summary>Records the refusing rule; does nothing for a caller not built for an exchange.</summary>
    internal void RecordRefusal(string faultRuleId)
    {
        if (ExchangeId is not null)
        {
            RefusedByFaultRuleId = faultRuleId;
        }
    }

    /// <summary>The same caller, on the exchange <paramref name="exchangeId"/>.</summary>
    /// <param name="exchangeId">The exchange's ID.</param>
    /// <returns>A new caller; this one is unchanged.</returns>
    public ContentCaller WithExchange(long exchangeId) => new(AccountName, Scheme, exchangeId, ListenUrl);

    /// <summary>
    /// The listen URL, as the instance reports it, of the listener whose exchange makes the call, so an
    /// out-of-space rule scoped by <c>listener</c> matches (ADR-0168 decision 1); <see langword="null"/> when the
    /// caller was not built from an exchange.
    /// </summary>
    public string? ListenUrl { get; }

    /// <summary>
    /// A caller on <paramref name="exchange"/>: its ID, listen URL and scheme, logged in as
    /// <paramref name="accountName"/> or anonymous when that is <see langword="null"/> (ADR-0168 decision 1).
    /// </summary>
    /// <param name="exchange">The exchange that makes the call.</param>
    /// <param name="accountName">The account's name, or <see langword="null"/> for an anonymous caller.</param>
    /// <returns>The caller.</returns>
    public static ContentCaller ForExchange(ExchangeContext exchange, string? accountName)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        return new ContentCaller(accountName, exchange.Scheme, exchange.ExchangeId, ReportedListenUrl.Format(exchange.ListenUrl));
    }

    /// <summary>
    /// The logged-in account's name, compared ordinally; <see langword="null"/> for an anonymous
    /// caller.
    /// </summary>
    public string? AccountName { get; }

    /// <summary>
    /// The calling server's scheme, such as <c>ftp</c>; <see langword="null"/> only for the
    /// lookups that name no scheme (<see cref="ContentStore.MapRequestPath(string)"/>).
    /// </summary>
    public string? Scheme { get; }

    /// <summary>Whether the caller carries no account name.</summary>
    public bool IsAnonymous => AccountName is null;

    /// <summary>
    /// A caller logged in as <paramref name="accountName"/> on <paramref name="scheme"/>.
    /// </summary>
    /// <param name="accountName">The account's name, as the server's login checked it.</param>
    /// <param name="scheme">The calling server's scheme, such as <c>sftp</c>.</param>
    /// <returns>The caller.</returns>
    /// <exception cref="ArgumentException"><paramref name="accountName"/> or
    /// <paramref name="scheme"/> is empty.</exception>
    public static ContentCaller ForAccount(string accountName, string scheme)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountName);
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        return new ContentCaller(accountName, scheme);
    }

    /// <summary>
    /// An anonymous caller on <paramref name="scheme"/>.
    /// </summary>
    /// <param name="scheme">The calling server's scheme, such as <c>tftp</c>.</param>
    /// <returns>The caller.</returns>
    /// <exception cref="ArgumentException"><paramref name="scheme"/> is empty.</exception>
    public static ContentCaller Anonymous(string scheme)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        return new ContentCaller(null, scheme);
    }

    /// <summary>
    /// The anonymous caller a lookup that names no caller stands for, on <paramref name="scheme"/>,
    /// which may be null.
    /// </summary>
    internal static ContentCaller AnonymousOn(string? scheme) => new(null, scheme);
}
