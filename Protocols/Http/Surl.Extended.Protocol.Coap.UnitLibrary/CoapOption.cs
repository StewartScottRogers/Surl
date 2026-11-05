using System.Text;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// One option of a CoAP message: its number and its value's bytes (RFC 7252 sections 3.1 and 5.4).
/// </summary>
public sealed class CoapOption
{
    /// <summary>The largest option number, 65535 (section 12.2).</summary>
    public const int MaxNumber = ushort.MaxValue;

    /// <summary>
    /// The longest value the option format can carry: 269 plus the largest two-byte extension,
    /// 65535 (section 3.1).
    /// </summary>
    public const int MaxValueLength = 269 + ushort.MaxValue;

    /// <summary>
    /// Creates an option.
    /// </summary>
    /// <param name="number">The option number, 0 to <see cref="MaxNumber"/>.</param>
    /// <param name="value">The value's bytes, at most <see cref="MaxValueLength"/> of them.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either is outside its range.</exception>
    public CoapOption(int number, ReadOnlyMemory<byte> value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxValueLength, nameof(value));

        Number = number;
        Value = value;
    }

    /// <summary>The option number.</summary>
    public int Number { get; }

    /// <summary>The value's bytes.</summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <summary>
    /// Whether the option is critical: an odd number (section 5.4.6). An even one is elective.
    /// </summary>
    public bool IsCritical => (Number & 1) == 1;

    /// <summary>
    /// Makes a uint option written in the fewest bytes, 0 as an empty value (section 3.2).
    /// </summary>
    /// <param name="number">The option number.</param>
    /// <param name="value">The unsigned integer.</param>
    /// <returns>The option.</returns>
    public static CoapOption FromUInt(int number, uint value)
    {
        var bytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        var leadingZeros = 0;

        while (leadingZeros < bytes.Length && bytes[leadingZeros] == 0)
        {
            leadingZeros++;
        }

        return new CoapOption(number, bytes[leadingZeros..]);
    }

    /// <summary>
    /// Makes a string option, its value the UTF-8 bytes of <paramref name="value"/> (section 3.2).
    /// </summary>
    /// <param name="number">The option number.</param>
    /// <param name="value">The text.</param>
    /// <returns>The option.</returns>
    public static CoapOption FromString(int number, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new CoapOption(number, Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    /// Reads the value as a uint (section 3.2): big-endian, leading zero bytes read and skipped,
    /// an empty value 0.
    /// </summary>
    /// <returns>The unsigned integer.</returns>
    /// <exception cref="FormatException">More than four bytes remain after the leading zeros.</exception>
    public uint ReadUInt()
    {
        var bytes = Value.Span;
        var start = 0;

        while (start < bytes.Length && bytes[start] == 0)
        {
            start++;
        }

        if (bytes.Length - start > 4)
        {
            throw new FormatException($"Option {Number}'s value is too long for a uint.");
        }

        uint value = 0;

        foreach (var octet in bytes[start..])
        {
            value = (value << 8) | octet;
        }

        return value;
    }
}
