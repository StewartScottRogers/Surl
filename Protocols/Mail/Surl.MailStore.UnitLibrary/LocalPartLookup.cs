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
    /// Every key held, in no particular order.
    /// </summary>
    public IReadOnlyList<string> Keys => [.. exact.Keys];

    /// <summary>
    /// Removes the value under <paramref name="key"/>, which a value has, and lets a key it shared
    /// ignoring case with another find that other again.
    /// </summary>
    public void Remove(string key)
    {
        var remaining = exact.Where(pair => pair.Key != key).ToList();
        Clear();
        remaining.ForEach(pair => Add(pair.Key, pair.Value));
    }

    /// <summary>
    /// Removes every key.
    /// </summary>
    public void Clear()
    {
        exact.Clear();
        ignoringCase.Clear();
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
