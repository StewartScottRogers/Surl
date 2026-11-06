using System.Globalization;

namespace Surl.Control;

/// <summary>
/// A request's query, read strictly: each key at most once, only the keys the request takes, and each number in its
/// range; anything else is <c>400 bad_request</c> naming the key.
/// </summary>
internal sealed class ControlQuery
{
    private readonly Dictionary<string, string> values;

    private ControlQuery(Dictionary<string, string> values)
    {
        this.values = values;
    }

    /// <summary>Reads the query of a request target: the text after its first <c>?</c>.</summary>
    /// <param name="query">The query, without the <c>?</c>; empty when there is none.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ControlErrorException">A key is given twice.</exception>
    public static ControlQuery Parse(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            var key = Decode(separator < 0 ? part : part[..separator]);
            if (!values.TryAdd(key, separator < 0 ? string.Empty : Decode(part[(separator + 1)..])))
            {
                throw new ControlErrorException(ControlError.BadRequest($"query key {key} is given more than once"));
            }
        }

        return new ControlQuery(values);
    }

    /// <summary>Refuses a key the request does not take.</summary>
    /// <param name="allowed">The keys the request takes.</param>
    /// <exception cref="ControlErrorException">A key is not among them.</exception>
    public void AllowOnly(params string[] allowed)
    {
        var unknown = values.Keys.FirstOrDefault(key => !allowed.Contains(key, StringComparer.Ordinal));
        if (unknown is not null)
        {
            throw new ControlErrorException(ControlError.BadRequest($"query key {unknown} is not one this request takes"));
        }
    }

    /// <summary>A text value, or <see langword="null"/> when the key is absent.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value.</returns>
    public string? Text(string key) => values.GetValueOrDefault(key);

    /// <summary>A whole number from <paramref name="lowest"/> to <paramref name="highest"/>, or the default.</summary>
    /// <param name="key">The key.</param>
    /// <param name="lowest">The lowest value allowed.</param>
    /// <param name="highest">The highest value allowed.</param>
    /// <param name="absent">The value when the key is absent.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ControlErrorException">The value is not a whole number in the range.</exception>
    public long Number(string key, long lowest, long highest, long absent)
    {
        if (!values.TryGetValue(key, out var text))
        {
            return absent;
        }

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < lowest || number > highest)
        {
            throw new ControlErrorException(ControlError.BadRequest($"query key {key} must be a whole number from {lowest} to {highest}"));
        }

        return number;
    }

    // A percent sign not followed by two hex digits is kept as it is, as Uri.UnescapeDataString leaves it.
    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
