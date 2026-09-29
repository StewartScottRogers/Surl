namespace Surl.Authentication;

/// <summary>
/// Compares two secrets, or two values computed from secrets, in time that depends only on
/// their length (ADR-0032, section 8). It is a seam so tests can count the comparisons.
/// </summary>
internal interface ISecretComparer
{
    /// <summary>
    /// Whether <paramref name="left"/> and <paramref name="right"/> hold the same bytes.
    /// </summary>
    bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);
}
