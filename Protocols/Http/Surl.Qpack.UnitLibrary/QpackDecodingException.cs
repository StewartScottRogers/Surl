namespace Surl.Qpack;

/// <summary>
/// Raised inside this library when bytes break a rule of RFC 9204, and caught where a result is made.
/// It never leaves this library.
/// </summary>
/// <param name="error">The rule the bytes broke.</param>
internal sealed class QpackDecodingException(QpackDecodingError error)
    : Exception($"QPACK decoding failed: {error}.")
{
    /// <summary>Gets the rule the bytes broke.</summary>
    public QpackDecodingError Error { get; } = error;

    /// <summary>Gets whether the bytes simply stopped short, so more bytes may complete them.</summary>
    public bool IsTruncation => Error is QpackDecodingError.TruncatedInteger or QpackDecodingError.TruncatedString;

    /// <summary>
    /// Raises a <see cref="QpackDecodingException" /> with <paramref name="error" /> when
    /// <paramref name="condition" /> holds.
    /// </summary>
    /// <param name="condition">Whether the bytes broke the rule.</param>
    /// <param name="error">The rule.</param>
    public static void ThrowIf(bool condition, QpackDecodingError error)
    {
        if (condition)
        {
            throw new QpackDecodingException(error);
        }
    }
}
