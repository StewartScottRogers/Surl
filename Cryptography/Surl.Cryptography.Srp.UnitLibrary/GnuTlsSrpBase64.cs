using System.Numerics;

namespace Surl.Cryptography.Srp;

/// <summary>
/// Reads GnuTLS's SRP base64 (<c>lib/auth/srp_sb64.c</c>), the encoding of the numbers and salts
/// in a <c>tpasswd</c> and <c>tpasswd.conf</c> file (ADR-0122 decision 4). It is not RFC 4648's:
/// the alphabet is <c>0-9A-Za-z./</c>, there is no <c>=</c> padding, and the value is
/// left-aligned, so a leading group shorter than four characters holds the bytes that did not
/// fill a group of three.
/// </summary>
public static class GnuTlsSrpBase64
{
    /// <summary>The 64 digits in order of value.</summary>
    public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz./";

    private const int BitsPerDigit = 6;

    // The bytes a leading group of 0, 1, 2 or 3 digits stands for.
    private static readonly int[] LeadingGroupByteCounts = [0, 1, 1, 2];

    /// <summary>
    /// Decodes <paramref name="text" />, or returns null when it is empty, holds a character outside
    /// <see cref="Alphabet" />, or its leading group holds more bits than its bytes can carry.
    /// </summary>
    /// <param name="text">The encoded text, with no surrounding whitespace.</param>
    public static byte[]? Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var value = BigInteger.Zero;
        foreach (var character in text)
        {
            var digit = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (digit < 0)
            {
                return null;
            }

            value = (value << BitsPerDigit) | digit;
        }

        var length = (3 * (text.Length / 4)) + LeadingGroupByteCounts[text.Length % 4];
        return length == 0 || value.GetByteCount(isUnsigned: true) > length ? null : ToBytes(value, length);
    }

    private static byte[] ToBytes(BigInteger value, int length)
    {
        var bytes = new byte[length];
        var valueLength = value.GetByteCount(isUnsigned: true);
        value.TryWriteBytes(bytes.AsSpan(length - valueLength), out _, isUnsigned: true, isBigEndian: true);
        return bytes;
    }
}
