namespace Surl.Hpack;

/// <summary>
/// Raised inside the decoder when a header block breaks a rule of RFC 7541, and caught by
/// <see cref="HpackDecoder.Decode" />, which returns <see cref="Error" /> as its result. It never
/// leaves this library.
/// </summary>
/// <param name="error">The rule the block broke.</param>
internal sealed class HpackDecodingException(HpackDecodingError error)
    : Exception($"HPACK decoding failed: {error}.")
{
    /// <summary>Gets the rule the block broke.</summary>
    public HpackDecodingError Error { get; } = error;

    /// <summary>
    /// Raises an <see cref="HpackDecodingException" /> with <paramref name="error" /> when
    /// <paramref name="condition" /> holds.
    /// </summary>
    /// <param name="condition">Whether the block broke the rule.</param>
    /// <param name="error">The rule.</param>
    public static void ThrowIf(bool condition, HpackDecodingError error)
    {
        if (condition)
        {
            throw new HpackDecodingException(error);
        }
    }
}
