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
    private ContentCaller(string? accountName, string? scheme, long? exchangeId = null)
    {
        AccountName = accountName;
        Scheme = scheme;
        ExchangeId = exchangeId;
    }

    /// <summary>
    /// The exchange that makes the call, so a store change and a fault point name it; <see langword="null"/>
    /// when the caller was not built for one (BL-872).
    /// </summary>
    public long? ExchangeId { get; }

    /// <summary>The same caller, on the exchange <paramref name="exchangeId"/>.</summary>
    /// <param name="exchangeId">The exchange's ID.</param>
    /// <returns>A new caller; this one is unchanged.</returns>
    public ContentCaller WithExchange(long exchangeId) => new(AccountName, Scheme, exchangeId);

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
