namespace Surl.MailStore;

/// <summary>
/// Values keyed by a name holding a mail local part, found as ADR-0050 decision 5 matches local
/// parts: the key equal ordinally, or else the one key equal ignoring case, if exactly one is.
/// </summary>
/// <typeparam name="TValue">The value a key finds.</typeparam>
internal sealed class LocalPartLookup<TValue>
    where TValue : class
{
    private readonly Dictionary<string, TValue> exact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TValue?> ignoringCase = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds <paramref name="value"/> under <paramref name="key"/>, which no value has yet.
    /// </summary>
    public void Add(string key, TValue value)
    {
        exact.Add(key, value);

        // A key two values share ignoring case finds neither of them that way.
        ignoringCase[key] = ignoringCase.ContainsKey(key) ? null : value;
    }

    /// <summary>
    /// The value whose key equals <paramref name="key"/> ordinally, or <see langword="null"/>.
    /// </summary>
    public TValue? FindExact(string key) => exact.GetValueOrDefault(key);

    /// <summary>
    /// The value whose key equals <paramref name="key"/> ordinally, or else the one whose key
    /// equals it ignoring case, or <see langword="null"/>.
    /// </summary>
    public TValue? Find(string key) => exact.GetValueOrDefault(key) ?? ignoringCase.GetValueOrDefault(key);
}
