using System.Globalization;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Reads an integer argument as Redis does (ADR-0106 decision 1): an optional minus sign, then
/// <c>0</c> alone or digits not starting with <c>0</c>, within a signed 64-bit integer; no plus
/// sign, no space.
/// </summary>
internal static class RedisIntegers
{
    // long.MinValue has 19 digits after its sign.
    private const int MostDigits = 19;

    /// <summary>Reads <paramref name="bytes"/> as an integer.</summary>
    /// <param name="bytes">The argument.</param>
    /// <param name="value">The integer, when read.</param>
    /// <returns>Whether the argument is an integer within 64 bits.</returns>
    public static bool TryParse(byte[] bytes, out long value)
    {
        value = 0;
        return IsCanonical(bytes)
            && long.TryParse(bytes, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsCanonical(ReadOnlySpan<byte> bytes)
    {
        var isNegative = bytes.StartsWith("-"u8);
        var digits = isNegative ? bytes[1..] : bytes;
        return digits.Length is > 0 and <= MostDigits
            && digits.IndexOfAnyExceptInRange((byte)'0', (byte)'9') < 0
            && (digits[0] != (byte)'0' || bytes.Length == 1);
    }
}
