using System.Numerics;

namespace Surl.Quic;

/// <summary>
/// QUIC's variable-length integer encoding (RFC 9000 section 16): the two high bits of the first
/// byte give the length, 1, 2, 4 or 8 bytes, and the remaining bits carry the value big-endian,
/// so a value is at most 2^62-1. Writing always uses the shortest encoding; reading accepts any.
/// </summary>
public static class QuicVariableLengthInteger
{
    /// <summary>The largest value the encoding carries, 2^62-1.</summary>
    public const ulong MaxValue = (1UL << 62) - 1;

    /// <summary>The longest encoding, in bytes.</summary>
    public const int MaxLength = 8;

    /// <summary>Returns the length of the shortest encoding of <paramref name="value"/>.</summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>1, 2, 4 or 8.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is above <see cref="MaxValue"/>.</exception>
    public static int EncodedLength(ulong value) => value switch
    {
        <= 0x3f => 1,
        <= 0x3fff => 2,
        <= 0x3fff_ffff => 4,
        <= MaxValue => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "A QUIC variable-length integer is at most 2^62-1."),
    };

    /// <summary>Returns the length of the encoding whose first byte is <paramref name="firstByte"/>.</summary>
    /// <param name="firstByte">The encoding's first byte.</param>
    /// <returns>1, 2, 4 or 8.</returns>
    public static int LengthFromFirstByte(byte firstByte) => 1 << (firstByte >> 6);

    /// <summary>Reads one integer from the start of <paramref name="source"/>.</summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="value">The value read, or 0 when the encoding is truncated.</param>
    /// <param name="bytesRead">The encoding's length, or 0 when it is truncated.</param>
    /// <returns><see langword="false"/> when <paramref name="source"/> ends inside the encoding.</returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out ulong value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;
        if (source.IsEmpty || source.Length < LengthFromFirstByte(source[0]))
        {
            return false;
        }

        bytesRead = LengthFromFirstByte(source[0]);
        value = source[0] & 0x3fUL;
        for (var index = 1; index < bytesRead; index++)
        {
            value = (value << 8) | source[index];
        }

        return true;
    }

    /// <summary>Writes the shortest encoding of <paramref name="value"/> at the start of <paramref name="destination"/>.</summary>
    /// <param name="destination">Where to write; at least <see cref="EncodedLength"/> bytes long.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is above <see cref="MaxValue"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too short.</exception>
    public static int Write(Span<byte> destination, ulong value)
    {
        var length = EncodedLength(value);
        if (destination.Length < length)
        {
            throw new ArgumentException($"{length} bytes are needed to encode {value}.", nameof(destination));
        }

        var remaining = value;
        for (var index = length - 1; index >= 0; index--)
        {
            destination[index] = (byte)remaining;
            remaining >>= 8;
        }

        destination[0] |= (byte)(BitOperations.Log2((uint)length) << 6);
        return length;
    }

    /// <summary>Returns the shortest encoding of <paramref name="value"/>.</summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>1, 2, 4 or 8 bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is above <see cref="MaxValue"/>.</exception>
    public static byte[] Encode(ulong value)
    {
        var encoding = new byte[EncodedLength(value)];
        Write(encoding, value);
        return encoding;
    }
}
