using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Reads the Kafka protocol guide's primitive types ("Protocol Primitive Types") from one
/// request body, front to back. Integers are big-endian; VARINT and VARLONG are zigzag-encoded
/// variable-length integers; the COMPACT forms carry their length as an UNSIGNED_VARINT one
/// more than the length, 0 meaning null.
/// </summary>
/// <remarks>
/// Every read that cannot be completed throws <see cref="KafkaMalformedRequestException"/>: a
/// field that runs past the end, a negative length where null is not allowed, a length below
/// -1, an UNSIGNED_VARINT over five bytes or past 32 bits, a VARLONG over ten bytes, and a
/// string that is not well-formed UTF-8. An array's element count is never more than the bytes
/// left, since every element the served schemas use takes at least one byte, so a count cannot
/// make a caller allocate more than the request already holds.
/// </remarks>
/// <param name="bytes">The bytes to read.</param>
public sealed class KafkaPrimitiveReader(ReadOnlyMemory<byte> bytes)
{
    private const int MaxUnsignedVarIntBytes = 5;
    private const int MaxVarLongBytes = 10;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>How many bytes have been read.</summary>
    public int Position { get; private set; }

    /// <summary>How many bytes are left to read.</summary>
    public int Remaining => bytes.Length - Position;

    /// <summary>Reads a BOOLEAN: one byte, 0 false and anything else true.</summary>
    /// <returns>The value.</returns>
    public bool ReadBoolean() => Take(1)[0] != 0;

    /// <summary>Reads an INT8.</summary>
    /// <returns>The value.</returns>
    public sbyte ReadInt8() => (sbyte)Take(1)[0];

    /// <summary>Reads an INT16.</summary>
    /// <returns>The value.</returns>
    public short ReadInt16() => BinaryPrimitives.ReadInt16BigEndian(Take(2));

    /// <summary>Reads an INT32.</summary>
    /// <returns>The value.</returns>
    public int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(Take(4));

    /// <summary>Reads an INT64.</summary>
    /// <returns>The value.</returns>
    public long ReadInt64() => BinaryPrimitives.ReadInt64BigEndian(Take(8));

    /// <summary>Reads a UINT32.</summary>
    /// <returns>The value.</returns>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    /// <summary>Reads a FLOAT64, an IEEE 754 binary64 value.</summary>
    /// <returns>The value.</returns>
    public double ReadFloat64() => BinaryPrimitives.ReadDoubleBigEndian(Take(8));

    /// <summary>Reads a UUID: sixteen bytes, the most significant first.</summary>
    /// <returns>The value.</returns>
    public Guid ReadUuid() => new(Take(16), bigEndian: true);

    /// <summary>Reads an UNSIGNED_VARINT: at most five bytes, seven bits each, low first.</summary>
    /// <returns>The value.</returns>
    public uint ReadUnsignedVarInt()
    {
        var value = ReadVariableLength(MaxUnsignedVarIntBytes, "UNSIGNED_VARINT");
        return value > uint.MaxValue
            ? throw new KafkaMalformedRequestException("An UNSIGNED_VARINT is larger than 32 bits.")
            : (uint)value;
    }

    /// <summary>Reads a VARINT: a zigzag-encoded UNSIGNED_VARINT.</summary>
    /// <returns>The value.</returns>
    public int ReadVarInt()
    {
        var zigzag = ReadUnsignedVarInt();
        return (int)(zigzag >> 1) ^ -(int)(zigzag & 1);
    }

    /// <summary>Reads a VARLONG: a zigzag-encoded unsigned value of at most ten bytes.</summary>
    /// <returns>The value.</returns>
    public long ReadVarLong()
    {
        var zigzag = ReadVariableLength(MaxVarLongBytes, "VARLONG");
        return (long)(zigzag >> 1) ^ -(long)(zigzag & 1);
    }

    /// <summary>Reads a STRING: an INT16 length, never negative, then UTF-8.</summary>
    /// <returns>The value.</returns>
    public string ReadString() => ReadNullableString() ?? throw NullWhereNotAllowed("STRING");

    /// <summary>Reads a NULLABLE_STRING: an INT16 length, -1 for null, then UTF-8.</summary>
    /// <returns>The value, or null.</returns>
    public string? ReadNullableString() => DecodeUtf8OrNull(ReadLength(ReadInt16(), "NULLABLE_STRING"));

    /// <summary>Reads a COMPACT_STRING: an UNSIGNED_VARINT length plus one, then UTF-8.</summary>
    /// <returns>The value.</returns>
    public string ReadCompactString() => ReadCompactNullableString() ?? throw NullWhereNotAllowed("COMPACT_STRING");

    /// <summary>Reads a COMPACT_NULLABLE_STRING: an UNSIGNED_VARINT length plus one, 0 for null, then UTF-8.</summary>
    /// <returns>The value, or null.</returns>
    public string? ReadCompactNullableString() => DecodeUtf8OrNull(ReadCompactLength("COMPACT_NULLABLE_STRING"));

    /// <summary>Reads BYTES: an INT32 length, never negative, then the bytes.</summary>
    /// <returns>The value.</returns>
    public ReadOnlyMemory<byte> ReadBytes() => ReadNullableBytes() ?? throw NullWhereNotAllowed("BYTES");

    /// <summary>
    /// Reads NULLABLE_BYTES, and RECORDS in a non-flexible version: an INT32 length, -1 for
    /// null, then the bytes.
    /// </summary>
    /// <returns>The value, or null.</returns>
    public ReadOnlyMemory<byte>? ReadNullableBytes() => TakeOrNull(ReadLength(ReadInt32(), "NULLABLE_BYTES"));

    /// <summary>Reads COMPACT_BYTES: an UNSIGNED_VARINT length plus one, then the bytes.</summary>
    /// <returns>The value.</returns>
    public ReadOnlyMemory<byte> ReadCompactBytes() => ReadCompactNullableBytes() ?? throw NullWhereNotAllowed("COMPACT_BYTES");

    /// <summary>
    /// Reads COMPACT_NULLABLE_BYTES, and RECORDS in a flexible version: an UNSIGNED_VARINT
    /// length plus one, 0 for null, then the bytes.
    /// </summary>
    /// <returns>The value, or null.</returns>
    public ReadOnlyMemory<byte>? ReadCompactNullableBytes() => TakeOrNull(ReadCompactLength("COMPACT_NULLABLE_BYTES"));

    /// <summary>Reads an ARRAY's element count: an INT32, -1 for a null array.</summary>
    /// <returns>The count, or null.</returns>
    public int? ReadArrayLength() => CountOrNull(ReadLength(ReadInt32(), "ARRAY"));

    /// <summary>Reads a COMPACT_ARRAY's element count: an UNSIGNED_VARINT count plus one, 0 for null.</summary>
    /// <returns>The count, or null.</returns>
    public int? ReadCompactArrayLength() => CountOrNull(ReadCompactLength("COMPACT_ARRAY"));

    /// <summary>
    /// Reads a tagged-field section and skips every field in it: an UNSIGNED_VARINT count, then
    /// for each field its UNSIGNED_VARINT tag, UNSIGNED_VARINT size and that many bytes. No tag
    /// the broker serves has a meaning yet, so every field is read and ignored.
    /// </summary>
    /// <returns>How many fields were skipped.</returns>
    public int SkipTaggedFields()
    {
        var count = ReadUnsignedVarInt();
        for (var index = 0u; index < count; index++)
        {
            ReadUnsignedVarInt();
            Take(ReadSize(ReadUnsignedVarInt(), "tagged field"));
        }

        return (int)count;
    }

    private static KafkaMalformedRequestException NullWhereNotAllowed(string type) =>
        new($"A {type} is null, which its type does not allow.");

    private static int ReadLength(int length, string type) =>
        length < -1 ? throw new KafkaMalformedRequestException($"A {type} length is {length}.") : length;

    private static int ReadSize(uint size, string what) =>
        size > int.MaxValue ? throw new KafkaMalformedRequestException($"A {what} size is {size}.") : (int)size;

    private int ReadCompactLength(string type) => ReadSize(ReadUnsignedVarInt(), type) - 1;

    private int? CountOrNull(int length) =>
        length < 0 ? null
        : length > Remaining ? throw new KafkaMalformedRequestException($"An array holds {length} elements and {Remaining} bytes are left.")
        : length;

    private string? DecodeUtf8OrNull(int length)
    {
        if (TakeOrNull(length) is not { } utf8)
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(utf8.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new KafkaMalformedRequestException("A string is not well-formed UTF-8.", exception);
        }
    }

    private ReadOnlyMemory<byte>? TakeOrNull(int length) => length < 0 ? (ReadOnlyMemory<byte>?)null : TakeMemory(length);

    private ulong ReadVariableLength(int maxBytes, string type)
    {
        ulong value = 0;
        for (var index = 0; index < maxBytes; index++)
        {
            var encodedByte = Take(1)[0];
            value |= (ulong)(encodedByte & 0x7F) << (7 * index);
            if ((encodedByte & 0x80) == 0)
            {
                return value;
            }
        }

        throw new KafkaMalformedRequestException($"A {type} runs past {maxBytes} bytes.");
    }

    private ReadOnlySpan<byte> Take(int count) => TakeMemory(count).Span;

    private ReadOnlyMemory<byte> TakeMemory(int count)
    {
        if (count > Remaining)
        {
            throw new KafkaMalformedRequestException($"A field needs {count} bytes and {Remaining} are left.");
        }

        var taken = bytes.Slice(Position, count);
        Position += count;
        return taken;
    }
}
